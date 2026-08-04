using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Lux.Management.Console.Modules.MikroTik.Hotspot.Views;

public partial class HotspotFolderPickerDialogWindow : Window
{
    public string SelectedFolder { get; private set; } = "hotspot";

    public HotspotFolderPickerDialogWindow(IEnumerable<string> availableFolders, string currentPath)
    {
        InitializeComponent();

        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pub", "skins", "license", "user-manager", "um", "web-ssl" };
        var foldersList = availableFolders.Where(f => !excluded.Contains(f.Trim())).ToList();

        FoldersItemsControl.ItemsSource = foldersList;
        SelectedFolder = foldersList.FirstOrDefault() ?? (string.IsNullOrWhiteSpace(currentPath) ? "hotspot" : currentPath);
        CustomFolderTextBox.Text = SelectedFolder;

        // Select matching RadioButton if available
        Dispatcher.BeginInvoke(new Action(() =>
        {
            foreach (var item in FoldersItemsControl.Items)
            {
                var container = FoldersItemsControl.ItemContainerGenerator.ContainerFromItem(item) as UIElement;
                if (container != null)
                {
                    var rb = FindVisualChild<RadioButton>(container);
                    if (rb != null && string.Equals(rb.Content?.ToString(), SelectedFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        rb.IsChecked = true;
                        break;
                    }
                }
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void FolderRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Content != null)
        {
            SelectedFolder = rb.Content.ToString() ?? "hotspot";
            CustomFolderTextBox.Text = SelectedFolder;
        }
    }

    private void CustomFolderTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(CustomFolderTextBox.Text))
        {
            SelectedFolder = CustomFolderTextBox.Text.Trim();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder))
        {
            SelectedFolder = "hotspot";
        }
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }
        return null;
    }
}
