using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VRChatDiscordUploader.Models;

public enum QueueItemStatus
{
    Queued,       // 待機中
    Optimizing,   // 画像最適化（リサイズ）中
    Uploading,    // Discord送信中
    Completed,    // 完了
    Failed        // 失敗
}

public partial class QueueItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
    public long OriginalSizeBytes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusBadgeText))]
    [NotifyPropertyChangedFor(nameof(IsProcessing))]
    [NotifyPropertyChangedFor(nameof(IsCompleted))]
    [NotifyPropertyChangedFor(nameof(IsFailed))]
    private QueueItemStatus _status = QueueItemStatus.Queued;

    [ObservableProperty]
    private string _statusMessage = "待機中";

    [ObservableProperty]
    private string _resolutionText = string.Empty;

    [ObservableProperty]
    private string _sizeText = string.Empty;

    [ObservableProperty]
    private bool _wasOptimized;

    public string FormattedOriginalSize => OriginalSizeBytes switch
    {
        >= 1024 * 1024 => $"{(double)OriginalSizeBytes / (1024 * 1024):F2} MB",
        >= 1024 => $"{(double)OriginalSizeBytes / 1024:F1} KB",
        _ => $"{OriginalSizeBytes} B"
    };

    public string FormattedDate => CapturedAt.ToString("yyyy/MM/dd HH:mm:ss");

    public string StatusBadgeText => Status switch
    {
        QueueItemStatus.Queued => "待機中",
        QueueItemStatus.Optimizing => "画像最適化中...",
        QueueItemStatus.Uploading => "Discord送信中...",
        QueueItemStatus.Completed => "送信完了",
        QueueItemStatus.Failed => "エラー",
        _ => "待機中"
    };

    public bool IsProcessing => Status is QueueItemStatus.Optimizing or QueueItemStatus.Uploading;
    public bool IsCompleted => Status == QueueItemStatus.Completed;
    public bool IsFailed => Status == QueueItemStatus.Failed;
}
