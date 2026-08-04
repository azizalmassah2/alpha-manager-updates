using System;

namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// بيانات الوكيل للعرض في الواجهة
/// </summary>
public class AgentDto
{
    public Guid    Id             { get; set; }
    public string  Name           { get; set; } = string.Empty;
    public string  Phone          { get; set; } = string.Empty;
    public string  Notes          { get; set; } = string.Empty;
    public decimal CommissionRate { get; set; }
    public decimal Balance        { get; set; }
    public bool    IsActive       { get; set; }
    public int     VoucherCount   { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public decimal EarnedCommission { get; set; }
    public decimal NetOwedBalance  { get; set; }
    public DateTime CreatedAt     { get; set; }
}
