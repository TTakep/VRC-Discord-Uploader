using System;
using System.Collections.Generic;
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

public class PhotoBatchService : IDisposable
{
    private readonly ConfigurationService _configService;
    private readonly VRCLogParserService _logParserService;
    private readonly DiscordWebhookService _discordService;

    private readonly List<VRCPhotoInfo> _batchBuffer = new();
    private readonly object _bufferLock = new();
    private Timer? _batchTimer;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public event EventHandler<UploadCompletedEventArgs>? UploadCompleted;

    public PhotoBatchService(
        ConfigurationService configService,
        VRCLogParserService logParserService,
        DiscordWebhookService discordService)
    {
        _configService = configService;
        _logParserService = logParserService;
        _discordService = discordService;
    }

    /// <summary>
    /// リアルタイムに撮影された写真を受け付けます。
    /// </summary>
    public void EnqueueRealtimePhoto(string filePath)
    {
        var photoInfo = _logParserService.ExtractPhotoMetadata(filePath, isRealtime: true);
        var config = _configService.CurrentConfig;

        if (config.UploadBehavior.BatchMode == BatchMode.Single)
        {
            _ = Task.Run(() => ProcessSingleUploadAsync(new List<VRCPhotoInfo> { photoInfo }));
        }
        else
        {
            lock (_bufferLock)
            {
                _batchBuffer.Add(photoInfo);

                // 最大添付数（10枚）に達した場合は即座にフラッシュ
                if (_batchBuffer.Count >= 10)
                {
                    FlushBatchImmediately();
                    return;
                }

                // タイマーをリセットまたは開始
                int waitMs = Math.Max(1000, config.UploadBehavior.BatchWaitSeconds * 1000);
                _batchTimer?.Dispose();
                _batchTimer = new Timer(_ => FlushBatchImmediately(), null, waitMs, Timeout.Infinite);
            }
        }
    }

    /// <summary>
    /// 手動アップロード（D&D、ファイル選択、アルバムからの送信）を受け付けます。
    /// </summary>
    public async Task EnqueueManualPhotosAsync(IEnumerable<string> filePaths)
    {
        var photoInfos = new List<VRCPhotoInfo>();
        foreach (var path in filePaths)
        {
            var info = _logParserService.ExtractPhotoMetadata(path, isRealtime: false);
            photoInfos.Add(info);
        }

        if (photoInfos.Count == 0) return;

        // 10枚単位のチャンクに分割して送信
        var chunks = photoInfos
            .Select((p, idx) => new { Photo = p, Index = idx })
            .GroupBy(x => x.Index / 10)
            .Select(g => g.Select(x => x.Photo).ToList())
            .ToList();

        foreach (var chunk in chunks)
        {
            await ProcessSingleUploadAsync(chunk);
            // チャンク間のインターバル
            await Task.Delay(1500);
        }
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

        _ = Task.Run(() => ProcessSingleUploadAsync(photosToSend));
    }

    private async Task ProcessSingleUploadAsync(List<VRCPhotoInfo> photos)
    {
        await _sendLock.WaitAsync();
        try
        {
            var historyItem = await _discordService.UploadPhotosAsync(photos);
            UploadCompleted?.Invoke(this, new UploadCompletedEventArgs(historyItem));
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
