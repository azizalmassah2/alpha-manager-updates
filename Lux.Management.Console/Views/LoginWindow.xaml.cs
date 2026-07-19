using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using Lux.Management.Console.ViewModels;
using Lux.Management.Console.Modules.MikroTik.Connections.Services;

namespace Lux.Management.Console.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose = () => Close();
    }

    private void PasswordBoxControl_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Password = PasswordBoxControl.Password;
        }
    }

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown(0);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            this.DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown(0);
    }

    private void AddDeviceButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var item = button?.DataContext as DiscoveredDevice;
        if (item != null && DataContext is LoginViewModel vm)
        {
            vm.SelectedDevice = item;
            // focus password box
            PasswordBoxControl.Focus();
        }
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button != null && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"عذراً، تعذر فتح الرابط: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DocumentationButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://alphamanager.app/docs");
    }

    private void SupportButton_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://alphamanager.app/support");
    }
}
