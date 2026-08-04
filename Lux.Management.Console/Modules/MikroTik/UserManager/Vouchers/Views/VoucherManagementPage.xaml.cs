using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MikroTikVoucherPrinter.Application.DTOs;
using Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.ViewModels;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.Views
{
    public partial class VoucherManagementPage : UserControl
    {
        public VoucherManagementPage()
        {
            InitializeComponent();
            this.Loaded += VoucherManagementPage_Loaded;
            this.Unloaded += VoucherManagementPage_Unloaded;
        }

        private void VoucherManagementPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is VoucherManagementViewModel vm && vm.SettingsService != null)
            {
                try
                {
                    var widthsStr = vm.SettingsService.Get<string>("VouchersGridColumnWidths", string.Empty);
                    if (!string.IsNullOrEmpty(widthsStr))
                    {
                        var widths = widthsStr.Split(';');
                        for (int i = 0; i < Math.Min(widths.Length, VouchersDataGrid.Columns.Count); i++)
                        {
                            var cleanWidth = widths[i].Replace('٫', '.');
                            if (double.TryParse(cleanWidth, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double w) && w > 0)
                            {
                                VouchersDataGrid.Columns[i].Width = new DataGridLength(w);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore column width loading errors
                }
            }
        }

        private async void VoucherManagementPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is VoucherManagementViewModel vm && vm.SettingsService != null)
            {
                try
                {
                    var widths = string.Join(";", VouchersDataGrid.Columns.Select(c => c.ActualWidth.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)));
                    vm.SettingsService.Set("VouchersGridColumnWidths", widths);
                    await vm.SettingsService.SaveAsync();
                }
                catch
                {
                    // Ignore column width saving errors
                }
            }
        }

        private void VouchersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is VoucherManagementViewModel vm)
            {
                vm.SelectedVoucherIds.Clear();
                foreach (var item in VouchersDataGrid.SelectedItems)
                {
                    if (item is VoucherDto voucher)
                    {
                        vm.SelectedVoucherIds.Add(voucher.Id);
                    }
                }
                vm.SelectedCount = vm.SelectedVoucherIds.Count;
            }
        }

        private void TreeView_SelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is VoucherManagementViewModel vm && e.NewValue is NavigationNodeDto node)
            {
                // لا نحدد عقد المجلدات الفارغة (folders)
                if (node.Category != "folder")
                {
                    vm.SelectedNode = node;
                }
            }
        }

        private async void TreeView_Expanded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem treeViewItem && treeViewItem.DataContext is NavigationNodeDto node)
            {
                if (DataContext is VoucherManagementViewModel vm)
                {
                    await vm.LoadChildrenOnExpandAsync(node);
                }
            }
        }

        private void BtnRowMenu_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.DataContext is VoucherDto voucher && DataContext is VoucherManagementViewModel vm)
                {
                    vm.SelectedVoucher = voucher;
                }
                if (btn.ContextMenu != null)
                {
                    btn.ContextMenu.PlacementTarget = btn;
                    btn.ContextMenu.IsOpen = true;
                }
            }
        }

        private void VouchersDataGrid_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Trigger LoadNextPageAsync when scroll reaches close to the bottom (120px threshold)
            if (e.VerticalChange > 0)
            {
                var scrollViewer = GetDescendantByType<ScrollViewer>(VouchersDataGrid);
                if (scrollViewer != null)
                {
                    if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 120)
                    {
                        if (DataContext is VoucherManagementViewModel vm && vm.HasMoreItems && !vm.IsLoadingMore)
                        {
                            _ = vm.LoadNextPageAsync();
                        }
                    }
                }
                else
                {
                    // Fallback to e.VerticalOffset if scrollViewer is not resolved
                    if (e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 120)
                    {
                        if (DataContext is VoucherManagementViewModel vm && vm.HasMoreItems && !vm.IsLoadingMore)
                        {
                            _ = vm.LoadNextPageAsync();
                        }
                    }
                }
            }
        }

        private void VouchersDataGridRow_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext is VoucherDto voucher && DataContext is VoucherManagementViewModel vm)
            {
                if (vm.ShowVoucherDetailsCommand.CanExecute(voucher))
                {
                    vm.ShowVoucherDetailsCommand.Execute(voucher);
                }
            }
        }

        private T? GetDescendantByType<T>(Visual element) where T : Visual
        {
            if (element == null) return null;
            if (element is T correctlyTyped) return correctlyTyped;

            T? foundElement = null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i) as Visual;
                if (child != null)
                {
                    foundElement = GetDescendantByType<T>(child);
                    if (foundElement != null) break;
                }
            }
            return foundElement;
        }
    }
}
