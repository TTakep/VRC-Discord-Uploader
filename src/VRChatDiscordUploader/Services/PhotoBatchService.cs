using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VRChatDiscordUploader.Models;

namespace VRChatDiscordUploader.Services;

public class UploadCompletedEventArgs : EventArgs
{
    public UploadHistoryItem HistoryItem { get; }

    public UploadCompletedEventArgs(UploadHistoryItem historyItem)
    {
        HistoryItem = historyItem;
    }
}

public class BatchStatusInfo
{
    public bool IsActive { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public int QueuedCount { get; set; }
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public double ProgressPercentage => TotalCount > 0 ? ((double)ProcessedCount / TotalCount) * 100 : 0;
}

public class PhotoBatchService : IDisposable
{
    private readonly ConfigurationService _configService;
    private readonly VRCLogParserService _logParserService;
    private readonly DiscordWebhookService _discordService;

    private readonly List<VRCPhotoInfo> _batchBuffer = new();
    private readonly object _bufferLock = new();
    private Timer? _batchTimer;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public ObservableCollection<QueueItem> QueueItems { get; } = new();

    public event EventHandler<UploadCompletedEventArgs>? UploadCompleted;
    public event EventHandler<BatchStatusInfo>? StatusChanged;
    public BatchStatusInfo CurrentStatus { get; private set; } = new();

    public PhotoBatchService(
        ConfigurationService configService,
        VRCLogParserService logParserService,
        DiscordWebhookService discordService)
    {
        _configService = configService;
        _logParserService = logParserService;
        _discordService = discordService;
    }

    public void UpdateStatus(bool isActive, string statusText, int processed = 0, int total = 0, int queued = 0)
    {
        CurrentStatus = new BatchStatusInfo
        {
            IsActive = isActive,
            StatusText = statusText,
            ProcessedCount = processed,
            TotalCount = total,
            QueuedCount = queued
        };
        StatusChanged?.Invoke(this, CurrentStatus);
    }

    public void ClearCompletedQueue()
    {
        App.CurrentWindowDispatcher?.TryEnqueue(() =>
        {
            var toRemove = QueueItems.Where(q => q.Status is QueueItemStatus.Completed or QueueItemStatus.Failed).ToList();
            foreach (var item in toRemove)
            {
                QueueItems.Remove(item);
            }
        });
    }

    private QueueItem CreateQueueItem(string filePath)
    {
        var fi = new FileInfo(filePath);
        return new QueueItem
        {
            FilePath = filePath,
            FileName = fi.Name,
            CapturedAt = fi.Exists ? fi.CreationTime : DateTime.Now,
            OriginalSizeBytes = fi.Exists ? fi.Length : 0,
            Status = QueueItemStatus.Queued,
            StatusMessage = "送信待機中"
        };
    }

    /// <summary>
    /// リアルタイムに撮影された写真を受け付けます。
    /// </summary>
    public void EnqueueRealtimePhoto(string filePath)
    {
        var photoInfo = _logParserService.ExtractPhotoMetadata(filePath, isRealtime: true);
        var config = _configService.CurrentConfig;

        var qItem = CreateQueueItem(filePath);
        App.CurrentWindowDispatcher?.TryEnqueue(() =>
        {
            QueueItems.Insert(0, qItem);
        });

        if (config.UploadBehavior.BatchMode == BatchMode.Single)
        {
            _ = Task.Run(async () =>
            {
                UpdateStatus(true, "写真送信中...", 0, 1, 1);
                qItem.Status = QueueItemStatus.Uploading;
                qItem.StatusMessage = "Discordへ送信中...";
                
                var history = await ProcessSingleUploadAsync(
                    new List<VRCPhotoInfo> { photoInfo }, 
                    null, 
                    msg => UpdateStatus(true, msg, 0, 1, 1));

                qItem.Status = history.Status == UploadStatus.Success ? QueueItemStatus.Completed : QueueItemStatus.Failed;
                qItem.StatusMessage = history.Status == UploadStatus.Success ? "送信完了" : $"失敗: {history.ErrorMessage}";
                UpdateStatus(false, "送信完了", 1, 1, 0);
            });
        }
        else
        {
            lock (_bufferLock)
            {
                _batchBuffer.Add(photoInfo);
                int waitSec = config.UploadBehavior.BatchWaitSeconds;
                UpdateStatus(true, $"連射バッチ待機中... ({_batchBuffer.Count}枚蓄積, 残り{waitSec}秒)", 0, _batchBuffer.Count, _batchBuffer.Count);

                if (_batchBuffer.Count >= 10)
                {
                    FlushBatchImmediately();
                    return;
                }

                int waitMs = Math.Max(1000, waitSec * 1000);
                _batchTimer?.Dispose();
                _batchTimer = new Timer(_ => FlushBatchImmediately(), null, waitMs, Timeout.Infinite);
            }
        }
    }

    /// <summary>
    /// 手動アップロード（D&D、ファイル選択、アルバムからの送信）を受け付けます。
    /// </summary>
    public Task EnqueueManualPhotosAsync(IEnumerable<string> filePaths, bool? imageOnlyOverride = null)
    {
        var pathsList = filePaths.ToList();
        if (pathsList.Count == 0) return Task.CompletedTask;

        var itemsMap = new Dictionary<string, QueueItem>();
        foreach (var path in pathsList)
        {
            var qItem = CreateQueueItem(path);
            itemsMap[path] = qItem;
        }

        App.CurrentWindowDispatcher?.TryEnqueue(() =>
        {
            foreach (var kvp in itemsMap)
            {
                QueueItems.Insert(0, kvp.Value);
            }
        });

        UpdateStatus(true, $"{pathsList.Count}枚の写真を準備中...", 0, pathsList.Count, pathsList.Count);

        _ = Task.Run(async () =>
        {
            try
            {
                var photoInfos = new List<VRCPhotoInfo>();
                for (int i = 0; i < pathsList.Count; i++)
                {
                    var path = pathsList[i];
                    UpdateStatus(true, $"メタデータ取得中 ({i + 1}/{pathsList.Count}): {Path.GetFileName(path)}", i, pathsList.Count, pathsList.Count - i);
                    var info = _logParserService.ExtractPhotoMetadata(path, isRealtime: false);
                    photoInfos.Add(info);
                }

                var chunks = photoInfos
                    .Select((p, idx) => new { Photo = p, Index = idx })
                    .GroupBy(x => x.Index / 10)
                    .Select(g => g.Select(x => x.Photo).ToList())
                    .ToList();

                int totalProcessed = 0;
                foreach (var chunk in chunks)
                {
                    // チャンク内アイテムを処理中に設定
                    App.CurrentWindowDispatcher?.TryEnqueue(() =>
                    {
                        foreach (var photo in chunk)
                        {
                            if (itemsMap.TryGetValue(photo.FilePath, out var item))
                            {
                                item.Status = QueueItemStatus.Optimizing;
                                item.StatusMessage = "画像最適化・準備中...";
                            }
                        }
                    });

                    var historyItem = await ProcessSingleUploadAsync(chunk, imageOnlyOverride, msg =>
                    {
                        UpdateStatus(true, msg, totalProcessed, pathsList.Count, pathsList.Count - totalProcessed);
                    });

                    // チャンク内アイテムを完了に更新
                    App.CurrentWindowDispatcher?.TryEnqueue(() =>
                    {
                        foreach (var photo in chunk)
                        {
                            if (itemsMap.TryGetValue(photo.FilePath, out var item))
                            {
                                item.Status = historyItem.Status == UploadStatus.Success ? QueueItemStatus.Completed : QueueItemStatus.Failed;
                                item.StatusMessage = historyItem.Status == UploadStatus.Success ? "送信完了" : $"エラー: {historyItem.ErrorMessage}";
                            }
                        }
                    });

                    totalProcessed += chunk.Count;
                    UpdateStatus(true, $"送信済み ({totalProcessed}/{pathsList.Count})", totalProcessed, pathsList.Count, pathsList.Count - totalProcessed);
                    await Task.Delay(1500);
                }

                UpdateStatus(false, $"{pathsList.Count}枚の写真の送信が完了しました", pathsList.Count, pathsList.Count, 0);
            }
            catch (Exception ex)
            {
                App.Log($"手動写真送信バックグラウンドエラー: {ex.Message}");
                UpdateStatus(false, $"エラーが発生しました: {ex.Message}", 0, pathsList.Count, 0);

                App.CurrentWindowDispatcher?.TryEnqueue(() =>
                {
                    foreach (var kvp in itemsMap)
                    {
                        if (kvp.Value.Status != QueueItemStatus.Completed)
                        {
                            kvp.Value.Status = QueueItemStatus.Failed;
                            kvp.Value.StatusMessage = $"エラー: {ex.Message}";
                        }
                    }
                });
            }
        });

        return Task.CompletedTask;
    }

    private void FlushBatchImmediately()
    {
        List<VRCPhotoInfo> photosToSend;
        lock (_bufferLock)
        {
            _batchTimer?.Dispose();
            _batchTimer = null;

            if (_batchBuffer.Count == 0) return;
            photosToSend = new List<VRCPhotoInfo>(_batchBuffer);
            _batchBuffer.Clear();
        }

        UpdateStatus(true, $"バッチ送信準備中 ({photosToSend.Count}枚)...", 0, photosToSend.Count, photosToSend.Count);

        // バッチ対象アイテムを処理中に更新
        var targetPaths = new HashSet<string>(photosToSend.Select(p => p.FilePath), StringComparer.OrdinalIgnoreCase);
        App.CurrentWindowDispatcher?.TryEnqueue(() =>
        {
            foreach (var q in QueueItems)
            {
                if (targetPaths.Contains(q.FilePath) && q.Status == QueueItemStatus.Queued)
                {
                    q.Status = QueueItemStatus.Uploading;
                    q.StatusMessage = "Discordへバッチ送信中...";
                }
            }
        });

        _ = Task.Run(async () =>
        {
            var history = await ProcessSingleUploadAsync(photosToSend, null, msg => UpdateStatus(true, msg, 0, photosToSend.Count, 0));

            App.CurrentWindowDispatcher?.TryEnqueue(() =>
            {
                foreach (var q in QueueItems)
                {
                    if (targetPaths.Contains(q.FilePath) && q.Status == QueueItemStatus.Uploading)
                    {
                        q.Status = history.Status == UploadStatus.Success ? QueueItemStatus.Completed : QueueItemStatus.Failed;
                        q.StatusMessage = history.Status == UploadStatus.Success ? "送信完了" : $"失敗: {history.ErrorMessage}";
                    }
                }
            });

            UpdateStatus(false, $"{photosToSend.Count}枚の送信が完了しました", photosToSend.Count, photosToSend.Count, 0);
        });
    }

    private async Task<UploadHistoryItem> ProcessSingleUploadAsync(
        List<VRCPhotoInfo> photos, 
        bool? imageOnlyOverride = null, 
        Action<string>? progress = null)
    {
        await _sendLock.WaitAsync();
        try
        {
            var historyItem = await _discordService.UploadPhotosAsync(photos, imageOnlyOverride, progress);
            UploadCompleted?.Invoke(this, new UploadCompletedEventArgs(historyItem));
            return historyItem;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Dispose()
    {
        _batchTimer?.Dispose();
        _sendLock.Dispose();
    }
}
