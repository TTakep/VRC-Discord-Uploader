using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VRChatDiscordUploader.Models;
using VRChatDiscordUploader.Services;

namespace VRChatDiscordUploader.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigurationService _configService;
    private readonly DiscordWebhookService _discordService;

    // Discord設定
    [ObservableProperty]
    private string _webhookUrl = string.Empty;

    [ObservableProperty]
    private int _destinationTypeIndex = 2; // 0: Channel, 1: Thread, 2: Forum

    [ObservableProperty]
    private string _threadId = string.Empty;

    [ObservableProperty]
    private string _forumTitleTemplate = "{Year}年{Month}月の写真";

    [ObservableProperty]
    private string _testResultText = string.Empty;

    [ObservableProperty]
    private bool _isTesting;

    // メタデータ設定
    [ObservableProperty]
    private bool _imageOnly;

    [ObservableProperty]
    private bool _includeDateTime;

    [ObservableProperty]
    private bool _includeWorldName;

    [ObservableProperty]
    private bool _includeWorldId;

    [ObservableProperty]
    private bool _includeUsers;

    [ObservableProperty]
    private double _maxUsersCount = 15;

    [ObservableProperty]
    private bool _includeResolutionAndSize = true;

    // 保存状態フィードバック
    [ObservableProperty]
    private bool _isSavedMessageOpen;

    [ObservableProperty]
    private string _savedMessageText = "設定を正常に保存しました。";

    // アップロード設定
    [ObservableProperty]
    private int _batchModeIndex = 1; // 0: Single, 1: Batch

    [ObservableProperty]
    private double _batchWaitSeconds = 3;

    [ObservableProperty]
    private double _maxFileSizeMB = 8.0;

    // 一般設定
    [ObservableProperty]
    private string _customWatchDirectory = string.Empty;

    [ObservableProperty]
    private int _notificationLevelIndex = 0; // 0: ErrorOnly, 1: All, 2: None

    [ObservableProperty]
    private int _closeBehaviorIndex = 0; // 0: MinimizeToTray, 1: ExitApplication

    [ObservableProperty]
    private bool _autoStartWithWindows;

    public SettingsViewModel(ConfigurationService configService, DiscordWebhookService discordService)
    {
        _configService = configService;
        _discordService = discordService;
        LoadSettings();
    }

    public void LoadSettings()
    {
        var cfg = _configService.CurrentConfig;

        WebhookUrl = _configService.GetWebhookUrl();
        DestinationTypeIndex = (int)cfg.Discord.DestinationType;
        ThreadId = cfg.Discord.ThreadId;
        ForumTitleTemplate = cfg.Discord.ForumThreadTitleTemplate;

        ImageOnly = cfg.UploadBehavior.ImageOnly;
        IncludeDateTime = cfg.Metadata.IncludeDateTime;
        IncludeWorldName = cfg.Metadata.IncludeWorldName;
        IncludeWorldId = cfg.Metadata.IncludeWorldId;
        IncludeUsers = cfg.Metadata.IncludeUsers;
        MaxUsersCount = cfg.Metadata.MaxUsersCount;
        IncludeResolutionAndSize = cfg.Metadata.IncludeResolutionAndSize;

        BatchModeIndex = (int)cfg.UploadBehavior.BatchMode;
        BatchWaitSeconds = cfg.UploadBehavior.BatchWaitSeconds;
        MaxFileSizeMB = cfg.UploadBehavior.MaxFileSizeMB;

        CustomWatchDirectory = cfg.General.CustomWatchDirectory;
        NotificationLevelIndex = (int)cfg.General.NotificationLevel;
        CloseBehaviorIndex = (int)cfg.General.CloseBehavior;
        AutoStartWithWindows = cfg.General.AutoStartWithWindows;
    }

    [RelayCommand]
    public void SaveSettings()
    {
        var cfg = _configService.CurrentConfig;

        _configService.SetWebhookUrl(WebhookUrl);
        cfg.Discord.DestinationType = (DestinationType)DestinationTypeIndex;
        cfg.Discord.ThreadId = ThreadId.Trim();
        cfg.Discord.ForumThreadTitleTemplate = ForumTitleTemplate.Trim();

        cfg.UploadBehavior.ImageOnly = ImageOnly;
        cfg.Metadata.IncludeDateTime = IncludeDateTime;
        cfg.Metadata.IncludeWorldName = IncludeWorldName;
        cfg.Metadata.IncludeWorldId = IncludeWorldId;
        cfg.Metadata.IncludeUsers = IncludeUsers;
        cfg.Metadata.MaxUsersCount = (int)MaxUsersCount;
        cfg.Metadata.IncludeResolutionAndSize = IncludeResolutionAndSize;

        cfg.UploadBehavior.BatchMode = (BatchMode)BatchModeIndex;
        cfg.UploadBehavior.BatchWaitSeconds = (int)BatchWaitSeconds;
        cfg.UploadBehavior.MaxFileSizeMB = MaxFileSizeMB;

        cfg.General.CustomWatchDirectory = CustomWatchDirectory.Trim();
        cfg.General.NotificationLevel = (NotificationLevel)NotificationLevelIndex;
        cfg.General.CloseBehavior = (CloseWindowBehavior)CloseBehaviorIndex;
        cfg.General.AutoStartWithWindows = AutoStartWithWindows;

        UpdateStartupRegistry(AutoStartWithWindows);
        _configService.SaveConfig();

        SavedMessageText = "設定を正常に保存しました。";
        IsSavedMessageOpen = true;

        Task.Delay(3500).ContinueWith(_ =>
        {
            App.CurrentWindowDispatcher?.TryEnqueue(() =>
            {
                IsSavedMessageOpen = false;
            });
        });
    }

    [RelayCommand]
    public async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(WebhookUrl))
        {
            TestResultText = "Webhook URLを入力してください。";
            return;
        }

        IsTesting = true;
        TestResultText = "テスト送信中...";

        try
        {
            var (success, message) = await _discordService.TestWebhookAsync(WebhookUrl.Trim());
            TestResultText = success ? $"✅ {message}" : $"❌ {message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    private static void UpdateStartupRegistry(bool enable)
    {
        try
        {
            const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            const string appName = "VRChatDiscordUploader";
            using var key = Registry.CurrentUser.OpenSubKey(runKeyPath, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(appName, $"\"{exePath}\" --minimized");
                }
            }
            else
            {
                key.DeleteValue(appName, false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"スタートアップ登録の更新に失敗しました: {ex.Message}");
        }
    }
}
