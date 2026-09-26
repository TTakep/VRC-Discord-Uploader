using System;
using System.Collections.Generic;

namespace VRChatDiscordUploader.Models;

public enum DestinationType
{
    Channel,
    Thread,
    Forum
}

public enum BatchMode
{
    Single, // 1枚ずつ送信
    Batch   // 短時間の連続撮影をまとめて送信
}

public enum NotificationLevel
{
    ErrorOnly, // エラー時のみ通知
    All,       // すべて通知
    None       // 通知しない
}

public class AppConfig
{
    public GeneralSettings General { get; set; } = new();
    public DiscordSettings Discord { get; set; } = new();
    public UploadBehaviorSettings UploadBehavior { get; set; } = new();
    public MetadataSettings Metadata { get; set; } = new();
}

public class GeneralSettings
{
    public bool AutoStartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public string CustomWatchDirectory { get; set; } = string.Empty;
    public NotificationLevel NotificationLevel { get; set; } = NotificationLevel.ErrorOnly;
}

public class DiscordSettings
{
    // DPAPIで暗号化されたBase64文字列
    public string EncryptedWebhookUrl { get; set; } = string.Empty;
    public DestinationType DestinationType { get; set; } = DestinationType.Forum;
    public string ThreadId { get; set; } = string.Empty;
    public string ForumThreadTitleTemplate { get; set; } = "{Year}年{Month}月の写真";
    
    // フォーラム月別スレッドのキャッシュ
    public string ForumCachedMonth { get; set; } = string.Empty; // 例: "2026-09"
    public string ForumCachedThreadId { get; set; } = string.Empty;
}

public class UploadBehaviorSettings
{
    public BatchMode BatchMode { get; set; } = BatchMode.Batch;
    public int BatchWaitSeconds { get; set; } = 3;
    public bool ImageOnly { get; set; } = false; // メタデータを一切含めず画像のみ送信
    public double MaxFileSizeMB { get; set; } = 8.0; // Discord上限（MB）
}

public class MetadataSettings
{
    public bool IncludeDateTime { get; set; } = true;
    public bool IncludeWorldName { get; set; } = true;
    public bool IncludeWorldId { get; set; } = false;
    public bool IncludeUsers { get; set; } = true;
    public int MaxUsersCount { get; set; } = 15;
}
