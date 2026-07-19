using System.Collections.ObjectModel;

namespace MikroTikVoucherPrinter.Application.DTOs;

public class NavigationNodeDto
{
    public string Id { get; set; } = string.Empty; // معرف فريد (مثال: all, unassigned, batch:id)
    public string Name { get; set; } = string.Empty; // الاسم المعروض بالعربية
    public string Icon { get; set; } = string.Empty; // أيقونة للتوضيح البصري
    public string Category { get; set; } = string.Empty; // all, unassigned, batches, agents, profiles
    public string AssociatedValue { get; set; } = string.Empty; // القيمة المصاحبة (مثال: Guid أو اسم باقة)
    
    // لدعم التحميل الكسول (Lazy Loading)
    public bool IsLazyLoadDummy { get; set; }
    
    public ObservableCollection<NavigationNodeDto> Children { get; } = new();

    public NavigationNodeDto()
    {
    }

    public NavigationNodeDto(string id, string name, string icon, string category, string associatedValue = "")
    {
        Id = id;
        Name = name;
        Icon = icon;
        Category = category;
        AssociatedValue = associatedValue;
    }
}
