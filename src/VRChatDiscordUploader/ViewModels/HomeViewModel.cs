using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VRChatDiscordUploader.Models;
using VRChatDiscordUploader.Services;

namespace VRChatDiscordUploader.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly FileWatcherService _fileWatcher;
    private readonly PhotoBatchService _photoBatch;
    private readonly HistoryManagerService _historyManager;
    private readonly ConfigurationService _configService;

    [ObservableProperty]
    private bool _isWatching;

    [ObservableProperty]
    private string _statusText = "監視待機中";

    [ObservableProperty]
    private string _watchDirectoryText = string.Empty;

    [ObservableProperty]
    private UploadHistoryItem? _lastUploadItem;

    [ObservableProperty]
    private bool _isUploading;

    [ObservableProperty]
    private string _manualUploadStatus = string.Empty;

    public HomeViewModel(
        FileWatcherService fileWatcher,
        PhotoBatchService photoBatch,
        HistoryManagerService historyManager,
        ConfigurationService configService)
    {
        _fileWatcher = fileWatcher;
        _photoBatch = photoBatch;
        _historyManager = historyManager;
        _configService = configService;

        UpdateWatchStatus();
        LoadLastHistory();

        _historyManager.HistoryItemAdded += OnHistoryItemAdded;

        _photoBatch.StatusChanged += (s, e) =>
        {
            App.CurrentWindowDispatcher?.TryEnqueue(() =>
            {
                ManualUploadStatus = e.StatusText;
                if (!e.IsActive)
                {
                    UpdateWatchStatus();
                }
            });
        };
    }

    private void UpdateWatchStatus()
    {
        IsWatching = _fileWatcher.IsWatching;
        StatusText = IsWatching ? "監視中（撮影待機中）" : "監視停止中";
        WatchDirectoryText = _configService.GetEffectiveWatchDirectory();
    }

    private void LoadLastHistory()
    {
        var history = _historyManager.GetHistory();
        LastUploadItem = history.FirstOrDefault();
    }

    [RelayCommand]
    public void ToggleWatching()
    {
        if (_fileWatcher.IsWatching)
        {
            _fileWatcher.Pause();
        }
        else
        {
            _fileWatcher.Resume();
        }
        UpdateWatchStatus();
    }

    /// <summary>
    /// ホーム画面へのドラッグ＆ドロップまたはファイル選択ダイアログで指定された写真を手動送信します。
    /// </summary>
    [RelayCommand]
    public async Task UploadManualFilesAsync(IEnumerable<string> filePaths)
    {
        var validFiles = filePaths
            .Where(f =>
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return ext is ".png" or ".jpg" or ".jpeg";
            })
            .ToList();

        if (validFiles.Count == 0) return;

        IsUploading = true;
        ManualUploadStatus = $"{validFiles.Count}枚の写真を送信処理中...";

        try
        {
            await _photoBatch.EnqueueManualPhotosAsync(validFiles);
            ManualUploadStatus = "送信キューに投入しました。";
        }
        catch (Exception ex)
        {
            ManualUploadStatus = $"エラー: {ex.Message}";
        }
        finally
        {
            IsUploading = false;
        }
    }

    private void OnHistoryItemAdded(object? sender, UploadHistoryItem item)
    {
        App.CurrentWindowDispatcher?.TryEnqueue(() =>
        {
            LastUploadItem = item;
        });
    }
}
