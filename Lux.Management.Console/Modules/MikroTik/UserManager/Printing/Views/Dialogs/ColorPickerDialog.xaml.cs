using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Printing.Views.Dialogs
{
    public partial class ColorPickerDialog : Window
    {
        public string SelectedColorHex { get; private set; } = "#000000";

        public ColorPickerDialog(Window owner, string initialColorHex)
        {
            InitializeComponent();
            Owner = owner;

            if (string.IsNullOrWhiteSpace(initialColorHex))
            {
                initialColorHex = "#000000";
            }
            
            TxtColorHex.Text = initialColorHex;
            UpdatePreview(initialColorHex);
        }

        private void ColorSquare_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Background is SolidColorBrush brush)
            {
                var color = brush.Color;
                string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                TxtColorHex.Text = hex;
            }
        }

        private void TxtColorHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview(TxtColorHex.Text);
        }

        private void UpdatePreview(string hex)
        {
            if (BdrPreview == null) return;
            
            try
            {
                var converter = new BrushConverter();
                var brush = (Brush)converter.ConvertFromString(hex)!;
                BdrPreview.Background = brush;
            }
            catch
            {
                // Fallback to transparent or keep previous if invalid
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string hex = TxtColorHex.Text.Trim();
            if (!hex.StartsWith("#"))
            {
                hex = "#" + hex;
            }

            try
            {
                // Validate if it is a valid hex color representation
                var color = (Color)ColorConverter.ConvertFromString(hex);
                SelectedColorHex = hex;
                DialogResult = true;
                Close();
            }
            catch
            {
                MessageBox.Show("كود اللون المدخل غير صالح. يرجى إدخال صيغة صحيحة (مثال: #FF0000).", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtColorHex.Focus();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
