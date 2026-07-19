using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Sales.Views;

public partial class SalesPage : UserControl
{
    public SalesPage()
    {
        InitializeComponent();
    }

    private void SalesDataGrid_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // عندما يقترب المستخدم من نهاية التمرير (120px threshold) نقوم بتحميل الصفحة التالية
        if (e.VerticalChange > 0)
        {
            var scrollViewer = GetDescendantByType<ScrollViewer>(SalesDataGrid);
            if (scrollViewer != null)
            {
                if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 120)
                {
                    if (DataContext is SalesViewModel vm && vm.HasMoreItems && !vm.IsLoadingMore)
                    {
                        _ = vm.LoadNextPageAsync();
                    }
                }
            }
            else
            {
                if (e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 120)
                {
                    if (DataContext is SalesViewModel vm && vm.HasMoreItems && !vm.IsLoadingMore)
                    {
                        _ = vm.LoadNextPageAsync();
                    }
                }
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
