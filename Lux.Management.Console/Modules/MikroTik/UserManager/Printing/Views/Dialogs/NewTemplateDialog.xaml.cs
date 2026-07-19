using System;
using System.Collections.Generic;
using System.Windows;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Printing.Views.Dialogs
{
    public partial class NewTemplateDialog : Window
    {
        public string TemplateName { get; private set; } = string.Empty;
        public string? SelectedProfile { get; private set; }

        public NewTemplateDialog(Window owner, IEnumerable<string> availableProfiles)
        {
            InitializeComponent();
            Owner = owner;

            // Load profiles to ComboBox
            CboProfiles.Items.Add(string.Empty); // Empty option
            foreach (var p in availableProfiles)
            {
                CboProfiles.Items.Add(p);
            }
            CboProfiles.SelectedIndex = 0;
            
            TxtTemplateName.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var name = TxtTemplateName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("اسم القالب ضروري ولا يمكن تركه فارغاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtTemplateName.Focus();
                return;
            }

            TemplateName = name;
            SelectedProfile = CboProfiles.SelectedItem as string;
            if (string.IsNullOrEmpty(SelectedProfile))
            {
                SelectedProfile = null;
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
