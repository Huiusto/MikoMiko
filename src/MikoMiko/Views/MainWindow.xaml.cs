using MikoMiko.Models;
using MikoMiko.Services;
using MikoMiko.ViewModels;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace MikoMiko.Views;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _shell;
    private readonly AppSettings _settings;
    private Forms.NotifyIcon? _tray;
    private bool _allowClose;

    public MainWindow(ShellViewModel shell, SettingsService service)
    {
        InitializeComponent();
        _shell = shell;
        _settings = service.Current;
        DataContext = shell;

        RestoreWindowBounds();
        StateChanged += (_, _) =>
        {
            UpdateMaximizeVisual();
            if (WindowState == WindowState.Minimized) _shell.PauseAllPlayback();
        };
    }

    // ---------------------------------------------------------------- 窗口尺寸

    private void RestoreWindowBounds()
    {
        if (_settings.WindowWidth >= MinWidth) Width = _settings.WindowWidth;
        if (_settings.WindowHeight >= MinHeight) Height = _settings.WindowHeight;
        if (_settings.WindowLeft >= 0 && _settings.WindowTop >= 0)
        {
            Left = _settings.WindowLeft;
            Top = _settings.WindowTop;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (_settings.IsMaximized) WindowState = WindowState.Maximized;
    }

    private void UpdateMaximizeVisual()
    {
        var maximized = WindowState == WindowState.Maximized;
        MaxIcon.Data = Geometry.Parse(maximized
            ? "M0.5 3.5 H6.5 V9.5 H4.5 M3.5 0.5 H9.5 V6.5 H7.5"
            : "M0.5 0.5 H9.5 V9.5 H0.5 Z");
        RootBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(10);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        ApplyWindowCorners(handle);
    }

    private void ApplyWindowCorners(IntPtr handle)
    {
        try
        {
            var preference = _settings.RoundedWindow ? 2 : 1;
            DwmSetWindowAttribute(handle, 33, ref preference, sizeof(int));
        }
        catch
        {
            // Windows 10 不支持圆角偏好
        }
    }

    // ---------------------------------------------------------------- 页头交互

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // 松开鼠标过快时忽略
        }
    }

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private WindowState _stateBeforeVideoFullscreen = WindowState.Normal;

    /// <summary>视频页最大化：隐藏左侧导航、页头与窗口边距，让画面铺满整个窗口。</summary>
    public void ToggleVideoFullscreen()
    {
        if (IsVideoFullscreen) ExitVideoFullscreen();
        else EnterVideoFullscreen();
    }

    public bool IsVideoFullscreen { get; private set; }

    public void EnterVideoFullscreen()
    {
        if (IsVideoFullscreen) return;
        _stateBeforeVideoFullscreen = WindowState;
        IsVideoFullscreen = true;
        NavColumn.Width = new GridLength(0);
        HeaderRow.Height = new GridLength(0);
        RootBorder.BorderThickness = new Thickness(0);
        RootBorder.CornerRadius = new CornerRadius(0);
        WindowState = WindowState.Maximized;
    }

    public void ExitVideoFullscreen()
    {
        if (!IsVideoFullscreen) return;
        IsVideoFullscreen = false;
        NavColumn.Width = new GridLength(72);
        HeaderRow.Height = new GridLength(64);
        RootBorder.BorderThickness = new Thickness(1);
        RootBorder.CornerRadius = new CornerRadius(
            WindowState == WindowState.Maximized ? 0 : 10);
        WindowState = _stateBeforeVideoFullscreen;
        UpdateMaximizeVisual();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_shell.GoBackCommand.CanExecute(null)) _shell.GoBackCommand.Execute(null);
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e) => _shell.ClearSearch();

    private void SearchInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        _ = _shell.SearchAsync();
    }

    // ---------------------------------------------------------------- 关闭 / 托盘

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_allowClose)
        {
            base.OnClosing(e);
            return;
        }

        SaveWindowBounds();

        switch (_settings.CloseAction)
        {
            case "Tray":
                e.Cancel = true;
                HideToTray();
                return;
            case "Ask":
                var result = MessageBox.Show(
                    "要最小化到系统托盘吗？\n选择「否」将退出 MikoMiko。",
                    "MikoMiko",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (result == MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    HideToTray();
                    return;
                }
                break;
        }

        _allowClose = true;
        _tray?.Dispose();
        _tray = null;
        Application.Current.Shutdown();
    }

    private void SaveWindowBounds()
    {
        _settings.IsMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
        }
    }

    private void HideToTray()
    {
        _shell.PauseAllPlayback();
        _tray ??= CreateTray();
        _tray.Visible = true;
        Hide();
    }

    private Forms.NotifyIcon CreateTray()
    {
        var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/icon.ico"))?.Stream;
        var icon = stream is null ? SystemIcons.Application : new Icon(stream);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示主窗口", null, (_, _) => ShowFromTray());
        menu.Items.Add("退出 MikoMiko", null, (_, _) =>
        {
            _allowClose = true;
            if (_tray is not null) _tray.Visible = false;
            Application.Current.Shutdown();
        });

        var notify = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = "MikoMiko",
            ContextMenuStrip = menu,
            Visible = true,
        };
        notify.DoubleClick += (_, _) => ShowFromTray();
        notify.BalloonTipTitle = "MikoMiko";
        notify.BalloonTipText = "已最小化到系统托盘，双击图标可恢复窗口。";
        notify.ShowBalloonTip(1500);
        return notify;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = _settings.IsMaximized ? WindowState.Maximized : WindowState.Normal;
        Activate();
    }

    // ---------------------------------------------------------------- Win32

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0024)
        {
            AdjustMaximizedBounds(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void AdjustMaximizedBounds(IntPtr hwnd, IntPtr lParam)
    {
        var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var monitor = MonitorFromWindow(hwnd, 2);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref monitorInfo))
            {
                var work = monitorInfo.rcWork;
                var area = monitorInfo.rcMonitor;
                info.ptMaxPosition.x = Math.Abs(work.left - area.left);
                info.ptMaxPosition.y = Math.Abs(work.top - area.top);
                info.ptMaxSize.x = Math.Abs(work.right - work.left);
                info.ptMaxSize.y = Math.Abs(work.bottom - work.top);
                info.ptMaxTrackSize.x = info.ptMaxSize.x;
                info.ptMaxTrackSize.y = info.ptMaxSize.y;
            }
        }

        Marshal.StructureToPtr(info, lParam, true);
    }
}
