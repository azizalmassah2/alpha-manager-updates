using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MikroTikVoucherPrinter.UI.Views.Pages;

public partial class TemplateManagementPage : UserControl
{
    public TemplateManagementPage()
    {
        InitializeComponent();
    }


    private void IncrementValue(string property, float step, float max)
    {
        if (DataContext is ViewModels.Pages.TemplateManagementViewModel vm && vm.SelectedTemplate != null)
        {
            var prop = typeof(Domain.Entities.TemplateConfig).GetProperty(property);
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
            vm.RefreshSelectedTemplate();
        }
    }

    private void DecrementValue(string property, float step, float min)
    {
        if (DataContext is ViewModels.Pages.TemplateManagementViewModel vm && vm.SelectedTemplate != null)
        {
            var prop = typeof(Domain.Entities.TemplateConfig).GetProperty(property);
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
            vm.RefreshSelectedTemplate();
        }
    }

    // ─── Grid / Margin Buttons ────────────────────────────────────────────────
    private void BtnColUp_Click(object sender, System.Windows.RoutedEventArgs e)    => IncrementValue("Columns", 1, 4);
    private void BtnColDown_Click(object sender, System.Windows.RoutedEventArgs e)  => DecrementValue("Columns", 1, 1);
    private void BtnRowUp_Click(object sender, System.Windows.RoutedEventArgs e)    => IncrementValue("Rows", 1, 22);
    private void BtnRowDown_Click(object sender, System.Windows.RoutedEventArgs e)  => DecrementValue("Rows", 1, 1);
    private void BtnMarginXUp_Click(object sender, System.Windows.RoutedEventArgs e)   => IncrementValue("MarginX", 0.1f, 20f);
    private void BtnMarginXDown_Click(object sender, System.Windows.RoutedEventArgs e) => DecrementValue("MarginX", 0.1f, 0f);
    private void BtnMarginYUp_Click(object sender, System.Windows.RoutedEventArgs e)   => IncrementValue("MarginY", 0.1f, 20f);
    private void BtnMarginYDown_Click(object sender, System.Windows.RoutedEventArgs e) => DecrementValue("MarginY", 0.1f, 0f);

    // ─── Active Element X/Y Spinbox ───────────────────────────────────────────
    private string _activeElement = "Username";

    private void BtnActiveXUp_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        IncrementValue(_activeElement + "X", 0.5f, 999f);
        UpdateActivePositionDisplay();
    }
    private void BtnActiveXDown_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        DecrementValue(_activeElement + "X", 0.5f, 0f);
        UpdateActivePositionDisplay();
    }
    private void BtnActiveYUp_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        IncrementValue(_activeElement + "Y", 0.5f, 999f);
        UpdateActivePositionDisplay();
    }
    private void BtnActiveYDown_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        DecrementValue(_activeElement + "Y", 0.5f, 0f);
        UpdateActivePositionDisplay();
    }

    private void UpdateActivePositionDisplay()
    {
        if (DataContext is ViewModels.Pages.TemplateManagementViewModel vm && vm.SelectedTemplate != null)
        {
            var xProp = typeof(Domain.Entities.TemplateConfig).GetProperty(_activeElement + "X");
            var yProp = typeof(Domain.Entities.TemplateConfig).GetProperty(_activeElement + "Y");
            if (xProp != null && TxtActiveX != null)
                TxtActiveX.Text = xProp.GetValue(vm.SelectedTemplate)?.ToString() ?? "0";
            if (yProp != null && TxtActiveY != null)
                TxtActiveY.Text = yProp.GetValue(vm.SelectedTemplate)?.ToString() ?? "0";
        }
    }

    // ─── Drag & Drop: Direct Canvas manipulation (no ViewModel refresh during drag) ──

    /// <summary>
    /// During drag: directly move the Thumb on the Canvas.
    /// ScaleY=-1 has been removed → Y increases downward (normal WPF).
    /// </summary>
    private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is not Thumb thumb) return;
        if (DataContext is not ViewModels.Pages.TemplateManagementViewModel vm || vm.SelectedTemplate == null) return;

        // Current canvas position
        double left = Canvas.GetLeft(thumb);
        double top  = Canvas.GetTop(thumb);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top))  top  = 0;

        // New position (HorizontalChange/VerticalChange are already in Canvas logical pixels)
        double newLeft = left + e.HorizontalChange;
        double newTop  = top  + e.VerticalChange;

        // Card bounds in canvas pixels (mm × 3.77)
        var t = vm.SelectedTemplate;
        double cardW = t.Columns > 0 ? (210.0 - t.MarginX * t.Columns) / t.Columns * 3.77 : 226.2;
        double cardH = t.Rows    > 0 ? (297.0 - t.MarginY * t.Rows)    / t.Rows    * 3.77 : 169.65;

        newLeft = System.Math.Max(0, System.Math.Min(newLeft, cardW - 10));
        newTop  = System.Math.Max(0, System.Math.Min(newTop,  cardH - 10));

        // Move the Thumb directly using SetCurrentValue to PRESERVE BINDINGS
        thumb.SetCurrentValue(Canvas.LeftProperty, newLeft);
        thumb.SetCurrentValue(Canvas.TopProperty, newTop);

        // Update X/Y display only
        _activeElement = thumb.Tag as string ?? "Username";
        if (TxtActiveX != null) TxtActiveX.Text = (newLeft / 3.77).ToString("F1");
        if (TxtActiveY != null) TxtActiveY.Text = (newTop  / 3.77).ToString("F1");
    }

    /// <summary>
    /// When drag ends: save the final canvas position back to the ViewModel.
    /// </summary>
    private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (sender is not Thumb thumb) return;
        if (DataContext is not ViewModels.Pages.TemplateManagementViewModel vm || vm.SelectedTemplate == null) return;

        string? propName = thumb.Tag as string;
        if (string.IsNullOrEmpty(propName)) return;

        double left = Canvas.GetLeft(thumb);
        double top  = Canvas.GetTop(thumb);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top))  top  = 0;

        float mmX = (float)System.Math.Round(left / 3.77, 1);
        float mmY = (float)System.Math.Round(top  / 3.77, 1);

        var xProp = typeof(Domain.Entities.TemplateConfig).GetProperty(propName + "X");
        var yProp = typeof(Domain.Entities.TemplateConfig).GetProperty(propName + "Y");

        xProp?.SetValue(vm.SelectedTemplate, mmX);
        yProp?.SetValue(vm.SelectedTemplate, mmY);

        // Update X/Y display
        if (TxtActiveX != null) TxtActiveX.Text = mmX.ToString("F1");
        if (TxtActiveY != null) TxtActiveY.Text = mmY.ToString("F1");
    }
}
