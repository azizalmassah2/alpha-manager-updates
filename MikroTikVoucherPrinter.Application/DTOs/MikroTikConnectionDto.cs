namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// بيانات إعدادات الاتصال بالمايكروتك
/// </summary>
public class MikroTikConnectionDto
{
    public string Host { get; set; } = "192.168.88.1";
    public int Port { get; set; } = 8728;
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = string.Empty;
}
