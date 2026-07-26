using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Lux.Management.Console.Modules.MikroTik.RouterManagement.Views
{
    public partial class AddVlanDialog : Window
    {
        public string ParentInterface => ComboParentInterface.SelectedItem?.ToString() ?? "bridge1";
        public int VlanId => int.TryParse(TxtVlanId.Text, out int id) ? id : 2;
        public string VlanName => TxtVlanName.Text.Trim();
        public string VlanIp => TxtVlanIp.Text.Trim();

        public AddVlanDialog(List<string> parentInterfaces, int defaultVlanId)
        {
            InitializeComponent();
            
            ComboParentInterface.ItemsSource = parentInterfaces;
            // القائمة تحتوي فقط على البريدجات المجلوبة من الراوتر، نختار الأول تلقائياً
            if (parentInterfaces.Count > 0)
                ComboParentInterface.SelectedIndex = 0;

            TxtVlanId.Text = defaultVlanId.ToString();
            UpdateDefaults(defaultVlanId);
            
            TxtVlanId.Focus();
            TxtVlanId.SelectAll();
        }

        private void UpdateDefaults(int id)
        {
            TxtVlanName.Text = "M" + id;
            TxtVlanIp.Text = $"172.16.{id}.1/24";
        }

        private void TxtVlanId_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded && int.TryParse(TxtVlanId.Text, out int id) && id > 0 && id <= 4094)
            {
                UpdateDefaults(id);
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtVlanId.Text, out int id) || id < 1 || id > 4094)
            {
                HandyControl.Controls.MessageBox.Show("يرجى إدخال معرّف فيلان صحيح بين 1 و 4094.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrEmpty(VlanName))
            {
                HandyControl.Controls.MessageBox.Show("يرجى إدخال اسم الفيلان.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrEmpty(VlanIp) || !VlanIp.Contains("/"))
            {
                HandyControl.Controls.MessageBox.Show("يرجى إدخال عنوان IP صحيح للفيلان مع نطاق الشبكة (مثال: 172.16.2.1/24).", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
