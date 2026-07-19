namespace MikroTikVoucherPrinter.Application.DTOs;

public class MikroTikUserResult
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool WasAlreadyPresent { get; set; } // مؤشر Idempotency
    public string? ProfileName { get; set; }
    public bool IsDisabled { get; set; }
}
