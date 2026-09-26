using System;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using VRChatDiscordUploader.Models;

namespace VRChatDiscordUploader.Services;

public class NotificationService
{
    private readonly ConfigurationService _configService;
    private bool _isAppNotificationSupported = false;

    public NotificationService(ConfigurationService configService)
    {
        _configService = configService;
        try
        {
            AppNotificationManager.Default.Register();
            _isAppNotificationSupported = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppNotificationManager初期化エラー: {ex.Message}");
            _isAppNotificationSupported = false;
        }
    }

    public void NotifyUploadResult(UploadHistoryItem item)
    {
        var level = _configService.CurrentConfig.General.NotificationLevel;
        if (level == NotificationLevel.None)
        {
            return;
        }

        if (item.Status == UploadStatus.Failed)
        {
            // エラー時は All または ErrorOnly の両方で通知
            ShowToast("送信に失敗しました", item.ErrorMessage, isError: true);
        }
        else if (item.Status == UploadStatus.Success && level == NotificationLevel.All)
        {
            var msg = item.PhotoCount > 1
                ? $"{item.PhotoCount}枚の写真をDiscordに送信しました。"
                : "写真をDiscordに送信しました。";

            if (!string.IsNullOrWhiteSpace(item.WorldName))
            {
                msg += $"\nワールド: {item.WorldName}";
            }

            ShowToast("送信完了", msg, isError: false);
        }
    }

    public void ShowToast(string title, string message, bool isError = false)
    {
        if (!_isAppNotificationSupported)
            return;

        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(title)
                .AddText(message);

            var notification = builder.BuildNotification();
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"通知表示エラー: {ex.Message}");
        }
    }
}
