using System.Windows;
using System.Windows.Input;
using MikroTikVoucherPrinter.UI.ViewModels;

namespace MikroTikVoucherPrinter.UI.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // الاستجابة لنجاح تسجيل الدخول من ViewModel
        _viewModel.OnLoginSucceeded += () =>
        {
            Dispatcher.Invoke(() => { DialogResult = true; });
        };

        // إذا كانت هناك بيانات محفوظة، تعبئة كلمة المرور تلقائياً
        if (!string.IsNullOrEmpty(_viewModel.Password))
            PasswordBox.Password = _viewModel.Password;

        // مع OnExplicitShutdown: إغلاق نافذة الدخول دون نجاح يجب أن ينهي التطبيق (X / Alt+F4)
        Closed += LoginWindow_OnClosed;
    }

    private void LoginWindow_OnClosed(object? sender, EventArgs e)
    {
        if (DialogResult == true)
            return;
        System.Windows.Application.Current.Shutdown();
    }

    // السماح بسحب النافذة من أي مكان
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    // إغلاق النافذة
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    // تصغير النافذة
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    // مزامنة كلمة السر مع ViewModel (PasswordBox لا يدعم Binding مباشر)
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordBox.Password;
    }
}
