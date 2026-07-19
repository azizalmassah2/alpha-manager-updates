using System;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Application.DTOs;

public class VoucherQueryParameters
{
    public Guid RouterId { get; set; }
    
    // الفلاتر الشجرية الجانبية
    public string SelectedNodeId { get; set; } = "all"; // all, unassigned, batch:id, agent:id, profile:name
    public string SelectedNodeCategory { get; set; } = "all"; // all, unassigned, batches, agents, profiles
    public string SelectedNodeValue { get; set; } = string.Empty; // القيمة المصاحبة (مثال: معرف الوكيل أو الباقة)
    
    // البحث الذكي
    public string SearchText { get; set; } = string.Empty;

    // الفلاتر الإضافية العامة
    public string FilterStatus { get; set; } = "All"; // All, Unused, Used, Expired, Deleted
    public string FilterSync { get; set; } = "All"; // All, Synced, Pending, Failed
    public string FilterProfile { get; set; } = "كل الباقات"; // اسم الباقة

    // التقسيم الصفحي
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    public bool IsExactSearch { get; set; } = false;
}
