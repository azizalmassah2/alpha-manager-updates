using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Vouchers.Views;

public partial class SelectProfileDialog : HandyControl.Controls.Window
{
    public string? SelectedProfileName { get; private set; }

    public SelectProfileDialog(string title, string voucherName, IEnumerable<string> availableProfiles, string defaultProfileName)
    {
        InitializeComponent();
        Title = title;
        TxtDialogTitle.Text = title;
        TxtVoucherInfo.Text = $"اسم الكرت: {voucherName}";

        var profileList = availableProfiles.Distinct().Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        CmbProfiles.ItemsSource = profileList;

        if (!string.IsNullOrEmpty(defaultProfileName) && profileList.Contains(defaultProfileName))
        {
            CmbProfiles.SelectedItem = defaultProfileName;
        }
        else if (profileList.Count > 0)
        {
            CmbProfiles.SelectedIndex = 0;
        }
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (CmbProfiles.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
        {
            SelectedProfileName = selected;
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("يرجى اختيار باقة أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
