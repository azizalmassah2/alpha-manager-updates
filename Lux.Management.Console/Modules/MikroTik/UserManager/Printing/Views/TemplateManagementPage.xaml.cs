using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Lux.Management.Console.Modules.MikroTik.UserManager.Printing.ViewModels;
using MikroTikVoucherPrinter.Domain.Entities;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Printing.Views
{
    public partial class TemplateManagementPage : UserControl
    {
        private const double ScaleFactor = 15.0;

        public TemplateManagementPage()
        {
            InitializeComponent();
            
            DataContextChanged += (s, e) =>
            {
                if (e.OldValue is TemplateManagementViewModel oldVm)
                {
                    oldVm.PropertyChanged -= ViewModel_PropertyChanged;
                }
                if (e.NewValue is TemplateManagementViewModel newVm)
                {
                    newVm.PropertyChanged += ViewModel_PropertyChanged;
                    UpdateActivePositionDisplay();
                }
            };

            Loaded += async (s, e) =>
            {
                if (DataContext is TemplateManagementViewModel viewModel)
                {
                    await viewModel.InitializeAsync();
                    UpdateActivePositionDisplay();
                }
            };
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TemplateManagementViewModel.SelectedTemplate))
            {
                UpdateActivePositionDisplay();
            }
        }

        private void IncrementValue(string property, float step, float max)
        {
            if (DataContext is TemplateManagementViewModel vm && vm.SelectedTemplate != null)
            {
                var prop = typeof(TemplateConfig).GetProperty(property);
                if (prop == null) return;

                if (prop.PropertyType == typeof(int))
                {
                    int val = (int)prop.GetValue(vm.SelectedTemplate)!;
                    val = (int)System.Math.Min(val + (int)step, (int)max);
                    prop.SetValue(vm.SelectedTemplate, val);
                }
                else if (prop.PropertyType == typeof(float))
                {
                    float val = (float)prop.GetValue(vm.SelectedTemplate)!;
                    val = System.Math.Min(val + step, max);
                    val = (float)System.Math.Round(val, 1);
                    prop.SetValue(vm.SelectedTemplate, val);
                }
                // INotifyPropertyChanged on TemplateConfig handles UI update.
                // Only call UpdatePreviewCards for grid dimension changes.
                vm.UpdatePreviewCards();
            }
        }

        private void DecrementValue(string property, float step, float min)
        {
            if (DataContext is TemplateManagementViewModel vm && vm.SelectedTemplate != null)
            {
                var prop = typeof(TemplateConfig).GetProperty(property);
                if (prop == null) return;

                if (prop.PropertyType == typeof(int))
                {
                    int val = (int)prop.GetValue(vm.SelectedTemplate)!;
                    val = (int)System.Math.Max(val - (int)step, (int)min);
                    prop.SetValue(vm.SelectedTemplate, val);
                }
                else if (prop.PropertyType == typeof(float))
                {
                    float val = (float)prop.GetValue(vm.SelectedTemplate)!;
                    val = System.Math.Max(val - step, min);
                    val = (float)System.Math.Round(val, 1);
                    prop.SetValue(vm.SelectedTemplate, val);
                }
                // INotifyPropertyChanged on TemplateConfig handles UI update.
                vm.UpdatePreviewCards();
            }
        }

        // ─── Grid / Margin Buttons ────────────────────────────────────────────────
        private void BtnColUp_Click(object sender, RoutedEventArgs e)    => IncrementValue("Columns", 1, 4);
        private void BtnColDown_Click(object sender, RoutedEventArgs e)  => DecrementValue("Columns", 1, 1);
        private void BtnRowUp_Click(object sender, RoutedEventArgs e)    => IncrementValue("Rows", 1, 22);
        private void BtnRowDown_Click(object sender, RoutedEventArgs e)  => DecrementValue("Rows", 1, 1);
        private void BtnMarginXUp_Click(object sender, RoutedEventArgs e)   => IncrementValue("MarginX", 0.1f, 20f);
        private void BtnMarginXDown_Click(object sender, RoutedEventArgs e) => DecrementValue("MarginX", 0.1f, 0f);
        private void BtnMarginYUp_Click(object sender, RoutedEventArgs e)   => IncrementValue("MarginY", 0.1f, 20f);
        private void BtnMarginYDown_Click(object sender, RoutedEventArgs e) => DecrementValue("MarginY", 0.1f, 0f);

        // ─── Active Element X/Y Spinbox ───────────────────────────────────────────
        private string _activeElement = "Username";

        private void BtnActiveXUp_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TemplateManagementViewModel vm && vm.SelectedTemplate != null)
            {
                var t = vm.SelectedTemplate;
                double maxW = t.Columns > 0 ? (210.0 - t.MarginX * t.Columns) / t.Columns : 70.0;
                IncrementValue(_activeElement + "X", 0.5f, (float)maxW);
                UpdateActivePositionDisplay();
            }
        }
        private void BtnActiveXDown_Click(object sender, RoutedEventArgs e)
        {
            DecrementValue(_activeElement + "X", 0.5f, 0f);
            UpdateActivePositionDisplay();
        }
        private void BtnActiveYUp_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TemplateManagementViewModel vm && vm.SelectedTemplate != null)
            {
                var t = vm.SelectedTemplate;
                double maxH = t.Rows > 0 ? (297.0 - t.MarginY * t.Rows) / t.Rows : 40.0;
                IncrementValue(_activeElement + "Y", 0.5f, (float)maxH);
                UpdateActivePositionDisplay();
            }
        }
        private void BtnActiveYDown_Click(object sender, RoutedEventArgs e)
        {
            DecrementValue(_activeElement + "Y", 0.5f, 0f);
            UpdateActivePositionDisplay();
        }

        private void UpdateActivePositionDisplay()
        {
            if (DataContext is TemplateManagementViewModel vm && vm.SelectedTemplate != null)
            {
                var xProp = typeof(TemplateConfig).GetProperty(_activeElement + "X");
                var yProp = typeof(TemplateConfig).GetProperty(_activeElement + "Y");
                if (xProp != null && TxtActiveX != null)
                    TxtActiveX.Text = xProp.GetValue(vm.SelectedTemplate)?.ToString() ?? "0";
                if (yProp != null && TxtActiveY != null)
                    TxtActiveY.Text = yProp.GetValue(vm.SelectedTemplate)?.ToString() ?? "0";
            }
        }

        // ─── Color Buttons Click Event Handlers ──────────────────────────────
        private void BtnFontColor_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TemplateManagementViewModel vm && vm.SelectedTemplate != null)
            {
                var activeWindow = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(x => x.IsActive)
                                   ?? System.Windows.Application.Current.MainWindow;

                var dialog = new Dialogs.ColorPickerDialog(activeWindow!, vm.SelectedTemplate.FontColorHex);
                if (dialog.ShowDialog() == true)
                {
                    // Setting FontColorHex fires INotifyPropertyChanged on TemplateConfig,
                    // which updates the Binding directly without destroying/recreating the Canvas.
                    vm.SelectedTemplate.FontColorHex = dialog.SelectedColorHex;
                }
            }
        }

        private void BtnFrameColor_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TemplateManagementViewModel vm && vm.SelectedTemplate != null)
            {
                var activeWindow = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(x => x.IsActive)
                                   ?? System.Windows.Application.Current.MainWindow;

                var dialog = new Dialogs.ColorPickerDialog(activeWindow!, vm.SelectedTemplate.FrameColorHex);
                if (dialog.ShowDialog() == true)
                {
                    // Setting FrameColorHex fires INotifyPropertyChanged on TemplateConfig directly.
                    vm.SelectedTemplate.FrameColorHex = dialog.SelectedColorHex;
                }
            }
        }

        // ─── Direct Image Manipulation — تحديث موضع العنصر المليمكري فورياً ──────
        private void CardPreviewImage_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            UpdateElementPositionFromMouse(sender, e.GetPosition((IInputElement)sender));
        }

        private void CardPreviewImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                UpdateElementPositionFromMouse(sender, e.GetPosition((IInputElement)sender));
            }
        }

        private void UpdateElementPositionFromMouse(object sender, Point pos)
        {
            if (sender is not FrameworkElement el) return;
            if (DataContext is not TemplateManagementViewModel vm || vm.SelectedTemplate == null) return;

            double widthPx = el.ActualWidth;
            double heightPx = el.ActualHeight;
            if (widthPx <= 0 || heightPx <= 0) return;

            var t = vm.SelectedTemplate;
            double cols = t.Columns > 0 ? t.Columns : 3;
            double rows = t.Rows > 0 ? t.Rows : 7;
            double cardWMm = (210.0 - t.MarginX * cols) / cols;
            double cardHMm = (297.0 - t.MarginY * rows) / rows;

            float mmX = (float)System.Math.Round(System.Math.Clamp((pos.X / widthPx) * cardWMm, 0, cardWMm), 1);
            float mmY = (float)System.Math.Round(System.Math.Clamp((pos.Y / heightPx) * cardHMm, 0, cardHMm), 1);

            var xProp = typeof(TemplateConfig).GetProperty(_activeElement + "X");
            var yProp = typeof(TemplateConfig).GetProperty(_activeElement + "Y");
            if (xProp != null && yProp != null)
            {
                xProp.SetValue(t, mmX);
                yProp.SetValue(t, mmY);
                UpdateActivePositionDisplay();
            }
        }
    }
}
