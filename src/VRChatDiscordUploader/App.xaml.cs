using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using VRChatDiscordUploader.Services;
using VRChatDiscordUploader.ViewModels;

namespace VRChatDiscordUploader;

public partial class App : Application
{
    private Window? _window;

    public static IServiceProvider Services { get; private set; } = null!;
    public static DispatcherQueue? CurrentWindowDispatcher { get; private set; }
    public static App CurrentApp => (App)Current;
    public Window? MainWindowInstance => _window;

    public App()
    {
        InitializeComponent();

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // サービス
        services.AddSingleton<ConfigurationService>();
        services.AddSingleton<ImageProcessingService>();
        services.AddSingleton<VRCLogParserService>();
        services.AddSingleton<DiscordWebhookService>();
        services.AddSingleton<HistoryManagerService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<AnnouncementService>();
        services.AddSingleton<FileWatcherService>();
        services.AddSingleton<PhotoBatchService>();

        // ViewModels
        services.AddTransient<HomeViewModel>();
        services.AddTransient<GalleryViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<AnnouncementsViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        CurrentWindowDispatcher = _window.DispatcherQueue;

        // サービスのバックグラウンド開始
        var configService = Services.GetRequiredService<ConfigurationService>();
        var logParser = Services.GetRequiredService<VRCLogParserService>();
        var fileWatcher = Services.GetRequiredService<FileWatcherService>();
        var photoBatch = Services.GetRequiredService<PhotoBatchService>();
        var notificationService = Services.GetRequiredService<NotificationService>();

        logParser.StartLiveMonitoring();
        fileWatcher.Start();

        // 写真送信完了時の通知連動
        photoBatch.UploadCompleted += (s, e) =>
        {
            notificationService.NotifyUploadResult(e.HistoryItem);
        };

        _window.Activate();
    }
}
