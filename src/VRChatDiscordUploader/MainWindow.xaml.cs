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

        // タスクトレイアイコンの左クリックでウィンドウ復帰
        AppTrayIcon.LeftClickCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ShowWindow);
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

    private void TrayOpen_Click(object sender, RoutedEventArgs e)
    {
        ShowWindow();
    }

    private void ShowWindow()
    {
        _appWindow?.Show();
        this.Activate();
    }

    private void TrayToggleWatch_Click(object sender, RoutedEventArgs e)
    {
        var watcher = App.Services.GetRequiredService<FileWatcherService>();
        if (watcher.IsWatching)
        {
            watcher.Pause();
        }
        else
        {
            watcher.Resume();
        }
    }

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        _isExplicitExit = true;
        AppTrayIcon.Dispose();
        this.Close();
    }
}
