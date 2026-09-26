using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;

namespace VRChatDiscordUploader.Services;

public class TrayIconService : IDisposable
{
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 100;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;

    private const uint TPM_BOTTOMALIGN = 0x0020;
    private const uint TPM_RETURNCMD = 0x0100;

    private const int CMD_OPEN = 1001;
    private const int CMD_TOGGLE = 1002;
    private const int CMD_EXIT = 1003;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    private readonly FileWatcherService _fileWatcher;
    private readonly Action _onExitAction;
    private readonly DispatcherQueue _dispatcher;

    private NOTIFYICONDATA _nid;
    private IntPtr _hIcon = IntPtr.Zero;
    private IntPtr _prevWndProc = IntPtr.Zero;
    private WndProcDelegate? _wndProcDelegate;
    private bool _isDisposed = false;

    public TrayIconService(
        IntPtr hwnd,
        AppWindow appWindow,
        FileWatcherService fileWatcher,
        DispatcherQueue dispatcher,
        Action onExitAction)
    {
        _hwnd = hwnd;
        _appWindow = appWindow;
        _fileWatcher = fileWatcher;
        _dispatcher = dispatcher;
        _onExitAction = onExitAction;

        InitializeTrayIcon();
        HookWndProc();
    }

    private void InitializeTrayIcon()
    {
        // アイコンのロード
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            _hIcon = LoadImage(IntPtr.Zero, iconPath, 1 /*IMAGE_ICON*/, 16, 16, 0x00000010 /*LR_LOADFROMFILE*/);
        }

        _nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "VRChat Discord Uploader"
        };

        Shell_NotifyIcon(NIM_ADD, ref _nid);
    }

    private void HookWndProc()
    {
        _wndProcDelegate = SubclassWndProc;
        var pWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
        _prevWndProc = SetWindowLongPtr(_hwnd, -4 /*GWLP_WNDPROC*/, pWndProc);
    }

    private IntPtr SubclassWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAYICON)
        {
            int eventId = lParam.ToInt32();
            if (eventId == WM_LBUTTONUP || eventId == WM_LBUTTONDBLCLK)
            {
                _dispatcher.TryEnqueue(ShowWindow);
            }
            else if (eventId == WM_RBUTTONUP)
            {
                ShowContextMenu();
            }
            return IntPtr.Zero;
        }

        return CallWindowProc(_prevWndProc, hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var hMenu = CreatePopupMenu();
        AppendMenu(hMenu, 0, CMD_OPEN, "開く");
        var watchText = _fileWatcher.IsWatching ? "監視を一時停止" : "監視を再開";
        AppendMenu(hMenu, 0, CMD_TOGGLE, watchText);
        AppendMenu(hMenu, 0x0800 /*MF_SEPARATOR*/, 0, string.Empty);
        AppendMenu(hMenu, 0, CMD_EXIT, "終了");

        GetCursorPos(out var pt);
        SetForegroundWindow(_hwnd);
        int cmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_BOTTOMALIGN, pt.X, pt.Y, _hwnd, IntPtr.Zero);
        DestroyMenu(hMenu);

        if (cmd == CMD_OPEN)
        {
            _dispatcher.TryEnqueue(ShowWindow);
        }
        else if (cmd == CMD_TOGGLE)
        {
            if (_fileWatcher.IsWatching)
            {
                _fileWatcher.Pause();
            }
            else
            {
                _fileWatcher.Resume();
            }
        }
        else if (cmd == CMD_EXIT)
        {
            _dispatcher.TryEnqueue(() => _onExitAction());
        }
    }

    public void ShowWindow()
    {
        _appWindow.Show();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            Shell_NotifyIcon(NIM_DELETE, ref _nid);
            if (_prevWndProc != IntPtr.Zero)
            {
                SetWindowLongPtr(_hwnd, -4 /*GWLP_WNDPROC*/, _prevWndProc);
            }
        }
    }
}
