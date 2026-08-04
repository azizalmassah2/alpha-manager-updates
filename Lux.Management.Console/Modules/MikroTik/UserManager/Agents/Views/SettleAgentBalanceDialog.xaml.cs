using System;
using System.Windows;

namespace Lux.Management.Console.Modules.MikroTik.UserManager.Agents.Views;

public partial class SettleAgentBalanceDialog : HandyControl.Controls.Window
{
    public decimal PaymentAmount { get; private set; }
    public string? PaymentNotes { get; private set; }

    public SettleAgentBalanceDialog(string agentName, decimal currentOwedBalance)
    {
        InitializeComponent();
        TxtAgentInfo.Text = $"الوكيل: {agentName}  |  المتبقي المطلوب: {currentOwedBalance:N0}";
        TxtAmount.Text = currentOwedBalance > 0 ? currentOwedBalance.ToString("0") : "0";
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (decimal.TryParse(TxtAmount.Text, out var val) && val > 0)
        {
            PaymentAmount = val;
            PaymentNotes = TxtNotes.Text?.Trim();
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("يرجى إدخال مبلغ تسديد صالح وأكبر من صفر.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
