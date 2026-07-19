namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// بطاقات KPI لشاشة المبيعات
/// الحساب مبني على userprofile.activated (أول تفعيل)
/// </summary>
public class SalesKpiDto
{
    /// <summary>عدد الكروت المفعَّلة اليوم</summary>
    public int TodaySales { get; set; }

    /// <summary>عدد الكروت المفعَّلة أمس</summary>
    public int YesterdaySales { get; set; }

    /// <summary>عدد الكروت المفعَّلة خلال آخر 7 أيام</summary>
    public int WeeklySales { get; set; }

    /// <summary>عدد الكروت المفعَّلة خلال الشهر الحالي (من أول الشهر)</summary>
    public int MonthlySales { get; set; }

    /// <summary>إجمالي الكروت المفعَّلة منذ بداية البيانات</summary>
    public int TotalSales { get; set; }

    /// <summary>الكروت غير المستخدمة حتى الآن (activated = 0)</summary>
    public int UnusedInventory { get; set; }

    /// <summary>إيرادات اليوم (بالريال)</summary>
    public long TodayRevenue { get; set; }

    /// <summary>إيرادات أمس (بالريال)</summary>
    public long YesterdayRevenue { get; set; }

    /// <summary>إيرادات الأسبوع (بالريال)</summary>
    public long WeeklyRevenue { get; set; }

    /// <summary>إيرادات الشهر (بالريال)</summary>
    public long MonthlyRevenue { get; set; }

    /// <summary>إجمالي الإيرادات (بالريال)</summary>
    public long TotalRevenue { get; set; }

    /// <summary>أكثر باقة مبيعا اليوم</summary>
    public string TodayBestProfile { get; set; } = string.Empty;

    /// <summary>أكثر باقة مبيعا امس</summary>
    public string YesterdayBestProfile { get; set; } = string.Empty;
}
