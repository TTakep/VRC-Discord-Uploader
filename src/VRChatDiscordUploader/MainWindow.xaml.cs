using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VRChatDiscordUploader.Services;
using VRChatDiscordUploader.Views;
using WinRT.Interop;

namespace VRChatDiscordUploader;

public sealed partial class MainWindow : Window
{
    private AppWindow? _appWindow;
    private TrayIconService? _trayIconService;
    private bool _isExplicitExit = false;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindow();
    }

    private void ConfigureWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // 初期ウィンドウサイズと中央配置
        _appWindow.Resize(new Windows.Graphics.SizeInt32(1000, 720));

        // ウィンドウクローズ時にタスクトレイに最小化格納
        _appWindow.Closing += AppWindow_Closing;

        // タスクトレイ常駐サービスの初期化
        var fileWatcher = App.Services.GetRequiredService<FileWatcherService>();
        _trayIconService = new TrayIconService(
            hwnd,
            _appWindow,
            fileWatcher,
            this.DispatcherQueue,
            onExitAction: ExitApplication
        );

        // 起動引数の確認（スタートアップ起動時は最小化トレイ格納）
        var args = Environment.GetCommandLineArgs();
        bool startMinimized = false;
        foreach (var arg in args)
        {
            if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase))
            {
                startMinimized = true;
                break;
            }
        }

        if (!startMinimized)
        {
            _appWindow.Show();
        }
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_isExplicitExit)
        {
            args.Cancel = true;
            _appWindow?.Hide();
        }
    }

    private void MainNavView_Loaded(object sender, RoutedEventArgs e)
    {
        if (MainNavView.MenuItems.Count > 0)
        {
            MainNavView.SelectedItem = MainNavView.MenuItems[0];
            NavigateTo("Home");
        }
    }

    private void MainNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private void NavigateTo(string tag)
    {
        Type pageType = tag switch
        {
            "Home" => typeof(HomePage),
            "Gallery" => typeof(GalleryPage),
            "History" => typeof(HistoryPage),
            "Announcements" => typeof(AnnouncementsPage),
            "Settings" => typeof(SettingsPage),
            _ => typeof(HomePage)
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }

    private void ExitApplication()
    {
        _isExplicitExit = true;
        _trayIconService?.Dispose();
        this.Close();
    }
}
