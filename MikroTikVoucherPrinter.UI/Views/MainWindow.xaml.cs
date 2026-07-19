using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MikroTikVoucherPrinter.UI.Services;
using MikroTikVoucherPrinter.UI.ViewModels;

namespace MikroTikVoucherPrinter.UI.Views;

/// <summary>
/// النافذة الرئيسية - تربط NavigationService مع ContentControl
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly NavigationService _navigationService;

    public MainWindow(MainViewModel viewModel, NavigationService navigationService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _navigationService = navigationService;

        DataContext = _viewModel;

        // ربط تغيير ViewModel مع الخاصية في MainViewModel
        _navigationService.ViewModelChanged += vm =>
        {
            _viewModel.CurrentViewModel = vm;
        };

        Closing += MainWindow_OnClosing;
        Closed += MainWindow_OnClosed;
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        var result = MessageBox.Show(
            "هل تريد إغلاق لوكس كارد؟ سيتم إنهاء البرنامج بالكامل ولن يبقى يعمل في الخلفية.",
            "تأكيد الخروج",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

        if (result != MessageBoxResult.OK)
            e.Cancel = true;
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        _viewModel.StopConnectionMonitoring();
        // مع OnExplicitShutdown لا يُغلق التطبيق تلقائياً — ننهيه صراحةً بعد إغلاق النافذة الرئيسية
        System.Windows.Application.Current.Shutdown();
    }

    // Custom Window Chrome Button Handlers
    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    // ════════════════════════════════════════════════════════════════════
    // حل مشكلة WindowStyle="None" وتغطية الـ Taskbar عبر Windows API Hooking
    // ════════════════════════════════════════════════════════════════════
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            hwndSource.AddHook(WindowProc);
        }
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0024) // WM_GETMINMAXINFO
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;

        int MONITOR_DEFAULTTONEAREST = 0x00000002;
        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO();
            GetMonitorInfo(monitor, monitorInfo);

            RECT rcWorkArea = monitorInfo.rcWork;
            RECT rcMonitorArea = monitorInfo.rcMonitor;

            mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
            mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
            mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
            mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
        }

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class MONITORINFO
    {
        public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        public RECT rcMonitor = new RECT();
        public RECT rcWork = new RECT();
        public int dwFlags = 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [DllImport("user32")]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

    [DllImport("User32")]
    internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);
}
