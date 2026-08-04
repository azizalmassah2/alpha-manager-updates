using MikroTikVoucherPrinter.Application.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Services.CommandProviders;

/// <summary>
/// مزود اسكريبتات الصيانة لـ RouterOS v6 User Manager.
/// كل اسكريبت مستقل ومبني على الأوامر المُثبَتة التي تعمل فعلياً على v6.
/// </summary>
public sealed class V6MaintenanceScriptProvider : IMaintenanceScriptProvider
{
    public string CleanQuotaScriptName    => "Alpha_Clean_UM_Quota_Vouchers";
    public string CleanTimeScriptName     => "Alpha_Clean_UM_Time_Vouchers";
    public string CleanSessionsScriptName => "Alpha_Clean_UM_Sessions_Logs";

    /// <summary>
    /// حذف كروت الرصيد المستنفدة (quota depleted).
    /// المرجع: !actual-profile هو الشرط الصحيح في v6 للكروت المنتهية.
    /// يتضمن: تعطيل الهوتسبوت مؤقتاً ← حذف الكروت ← إعادة تشغيله.
    /// </summary>
    public string BuildCleanQuotaScript() =>
        """
        # =========================================================
        # RouterOS Script: Alpha_Clean_UM_Quota_Vouchers (v6)
        # Fast quota-depleted voucher cleanup
        # =========================================================

        :log info "Alpha Maintenance: Starting fast quota cleanup..."

        :local removed 0

        :foreach pl in=[/tool user-manager profile profile-limitation find] do={

            :local profile [/tool user-manager profile profile-limitation get $pl profile]
            :local limName [/tool user-manager profile profile-limitation get $pl limitation]

            :local limId [/tool user-manager profile limitation find where name=$limName]

            :if ([:len $limId] > 0) do={

                :local quota [/tool user-manager profile limitation get $limId transfer-limit]

                :if ($quota > 0) do={

                    :foreach u in=[/tool user-manager user find where actual-profile=$profile and download-used>0] do={

                        :local dl [/tool user-manager user get $u download-used]
                        :local ul [/tool user-manager user get $u upload-used]

                        :if (($dl + $ul) >= $quota) do={
                            /tool user-manager user remove $u
                            :set removed ($removed + 1)
                        }
                    }
                }
            }
        }

        :log info ("Alpha Maintenance: Removed " . $removed . " quota-depleted vouchers.")
        """;

    /// <summary>
    /// حذف كروت الوقت المنتهية (time expired).
    /// المرجع: !actual-profile هو الشرط الصحيح في v6 للكروت المنتهية.
    /// يتضمن: تعطيل الهوتسبوت مؤقتاً ← حذف الكروت ← إعادة تشغيله.
    /// </summary>
    public string BuildCleanTimeScript() =>
        """
        # =========================================================
        # Alpha_Clean_UM_Time_Vouchers (RouterOS v6)
        # حذف كروت الوقت المنتهية من User Manager
        # =========================================================
        :log info "Alpha: Time voucher cleanup started."

        /ip hotspot disable [find]
        /interface pppoe-server server disable [find]
        :delay 5s

        /tool user-manager user remove [find where !actual-profile and uptime-used>0s]
        :log info "Alpha: Time-expired vouchers removed."
        :delay 3s

        /ip hotspot enable [find]
        /interface pppoe-server server enable [find]
        :log info "Alpha: Time voucher cleanup completed."
        """;

    /// <summary>
    /// تنظيف الجلسات واللوج وإعادة بناء قاعدة البيانات.
    /// المرجع: close-session أولاً ← remove ← remove logs ← rebuild ← rebuild-log.
    /// </summary>
    public string BuildCleanSessionsScript() =>
        """
        # =========================================================
        # Alpha_Clean_UM_Sessions_Logs (RouterOS v6)
        # تنظيف الجلسات واللوج وإعادة بناء قاعدة البيانات
        # =========================================================
        :log info "Alpha: Sessions and logs cleanup started."

        :do { /tool user-manager session close-session [find] } on-error={}
        :delay 3s

        :do { /tool user-manager session remove [find] } on-error={}
        :log info "Alpha: Sessions removed."
        :delay 3s

        :do { /tool user-manager log remove [find] } on-error={}
        :log info "Alpha: Logs removed."
        :delay 3s

        /tool user-manager database rebuild
        :log info "Alpha: User database rebuilt."
        :delay 15s

        /tool user-manager database rebuild-log
        :log info "Alpha: Log database rebuilt."
        :delay 15s

        :log info "Alpha: Sessions and logs cleanup completed."
        """;
}
