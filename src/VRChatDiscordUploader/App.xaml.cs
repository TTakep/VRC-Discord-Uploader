using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using VRChatDiscordUploader.Services;
using VRChatDiscordUploader.ViewModels;

namespace VRChatDiscordUploader;

public partial class App : Application
{
    private Window? _window;
    public static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VRCDiscordUploader",
        "app.log"
    );

    public static IServiceProvider Services { get; private set; } = null!;
    public static DispatcherQueue? CurrentWindowDispatcher { get; private set; }
    public static App CurrentApp => (App)Current;
    public Window? MainWindowInstance => _window;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    public App()
    {
        Log("App コンストラクタ開始");

        this.UnhandledException += (sender, e) =>
        {
            Log($"[UnhandledException] {e.Message}\n{e.Exception}");
            MessageBoxW(IntPtr.Zero, $"エラーが発生しました:\n{e.Message}\n\nログ: {LogFilePath}", "VRChat Discord Uploader", 0x10);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            Log($"[AppDomain UnhandledException] {e.ExceptionObject}");
        };

        try
        {
            InitializeComponent();
            Log("InitializeComponent 完了");

            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();
            Log("サービスプロバイダー構築完了");
        }
        catch (Exception ex)
        {
            Log($"[App 初期化エラー] {ex}");
            MessageBoxW(IntPtr.Zero, $"初期化エラー:\n{ex.Message}\n\nログ: {LogFilePath}", "VRChat Discord Uploader", 0x10);
            throw;
        }
    }

    public static void Log(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ConfigurationService>();
        services.AddSingleton<ImageProcessingService>();
        services.AddSingleton<VRCLogParserService>();
        services.AddSingleton<DiscordWebhookService>();
        services.AddSingleton<HistoryManagerService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<AnnouncementService>();
        services.AddSingleton<FileWatcherService>();
        services.AddSingleton<PhotoBatchService>();

        services.AddTransient<HomeViewModel>();
        services.AddTransient<GalleryViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<AnnouncementsViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Log("OnLaunched 開始");
        try
        {
            _window = new MainWindow();
            Log("MainWindow インスタンス生成完了");

            CurrentWindowDispatcher = _window.DispatcherQueue;

            var logParser = Services.GetRequiredService<VRCLogParserService>();
            var fileWatcher = Services.GetRequiredService<FileWatcherService>();
            var photoBatch = Services.GetRequiredService<PhotoBatchService>();
            var notificationService = Services.GetRequiredService<NotificationService>();

            logParser.StartLiveMonitoring();
            fileWatcher.Start();
            Log("バックグラウンドサービス監視開始");

            photoBatch.UploadCompleted += (s, e) =>
            {
                notificationService.NotifyUploadResult(e.HistoryItem);
            };

            _window.Activate();
            Log("MainWindow Activate 完了");
        }
        catch (Exception ex)
        {
            Log($"[OnLaunched エラー] {ex}");
            MessageBoxW(IntPtr.Zero, $"起動エラー:\n{ex.Message}\n\nログ: {LogFilePath}", "VRChat Discord Uploader", 0x10);
            throw;
        }
    }
}
