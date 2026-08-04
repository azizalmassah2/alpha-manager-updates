using MikroTikVoucherPrinter.Application.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Services.CommandProviders;

/// <summary>
/// مزود اسكريبتات الصيانة لـ RouterOS v7 User Manager.
/// جميع الاسكريبتات تستخدم /user-manager/... المناسب لـ v7.
/// </summary>
public sealed class V7MaintenanceScriptProvider : IMaintenanceScriptProvider
{
    public string CleanQuotaScriptName  => "Alpha_Clean_UM_Quota_Vouchers";
    public string CleanTimeScriptName   => "Alpha_Clean_UM_Time_Vouchers";
    public string CleanSessionsScriptName => "Alpha_Clean_UM_Sessions_Logs";

    public string BuildCleanQuotaScript() =>
        // يحذف فقط المستخدمين الذين استنفدوا رصيد البايتات المتاح فعلاً.
        // لا يحذف: المعطلين الذين لديهم رصيد متبقٍ، ولا المنتهي وقتهم.
        """
        # =========================================================
        # RouterOS Script: Alpha_Clean_UM_Quota_Vouchers (v7)
        # تنظيف كروت User Manager التي استنفدت رصيد البايتات المتاحة
        # =========================================================
        :log info "Alpha Maintenance: Starting quota-depleted voucher cleanup (v7)..."

        :local removed 0
        :foreach u in=[/user-manager user find where bytes-limit>0] do={
            :local dl  [/user-manager user get $u bytes-downloaded]
            :local ul  [/user-manager user get $u bytes-uploaded]
            :local lim [/user-manager user get $u bytes-limit]
            :if (($dl + $ul) >= $lim) do={
                /user-manager user remove $u
                :set removed ($removed + 1)
            }
        }

        :log info ("Alpha Maintenance: Removed " . $removed . " quota-depleted vouchers (v7).")
        """;

    public string BuildCleanTimeScript() =>
        """
        # =========================================================
        # RouterOS Script: Alpha_Clean_UM_Time_Vouchers (v7)
        # تنظيف كروت User Manager التي استنفدت وقت الاستخدام المتاح
        # =========================================================
        :log info "Alpha Maintenance: Starting time-expired voucher cleanup (v7)..."

        :local removed 0
        :foreach u in=[/user-manager user find where time-limit>0s] do={
            :local used [/user-manager user get $u time-used]
            :local lim  [/user-manager user get $u time-limit]
            :if ($used >= $lim) do={
                /user-manager user remove $u
                :set removed ($removed + 1)
            }
        }

        :log info ("Alpha Maintenance: Removed " . $removed . " time-expired vouchers (v7).")
        """;

    public string BuildCleanSessionsScript() =>
        """
        # =========================================================
        # RouterOS Script: Alpha_Clean_UM_Sessions_Logs (v7)
        # تنظيف الجلسات المنتهية واللوج القديم في User Manager
        # =========================================================
        :log info "Alpha Maintenance: Cleaning inactive sessions and logs (v7)..."

        :do { /user-manager session remove [find active=no] } on-error={
            :log warning "Alpha Maintenance: Session cleanup failed silently."
        }
        :do { /user-manager log remove [find] } on-error={
            :log warning "Alpha Maintenance: Log cleanup failed silently."
        }

        :log info "Alpha Maintenance: Sessions and logs cleanup completed (v7)."
        """;
}
