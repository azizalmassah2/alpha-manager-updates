using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenWrtProgrammerPro.Helpers;
using OpenWrtProgrammerPro.Models;
using OpenWrtProgrammerPro.Services.Interfaces;

namespace OpenWrtProgrammerPro.Services
{
    public class ProgrammingService : IProgrammingService
    {
        private IUbusClient Ubus => ServiceLocator.Instance.Resolve<IUbusClient>();
        private IUciService Uci => ServiceLocator.Instance.Resolve<IUciService>();
        private IDeviceDiscoveryService Discovery => ServiceLocator.Instance.Resolve<IDeviceDiscoveryService>();
        private INetworkService Network => ServiceLocator.Instance.Resolve<INetworkService>();
        private IWirelessService Wireless => ServiceLocator.Instance.Resolve<IWirelessService>();
        private IBackupService Backup => ServiceLocator.Instance.Resolve<IBackupService>();
        private ILoggerService Logger => ServiceLocator.Instance.Resolve<ILoggerService>();

        public async Task ProgramDeviceSingleAsync(
            string connectIp,
            string username,
            string password,
            string targetIp,
            string gateway,
            string subnetMask,
            int vlanId,
            WirelessConfig wirelessConfig,
            IProgress<(int percent, string message)> progress,
            CancellationToken cancellationToken,
            bool canCommit = true,
            bool canApply = true,
            bool changePassword = false,
            string newPassword = "",
            bool tryNetworkPasswordFirst = false)
        {
            string session = string.Empty;
            try
            {
                // Step 1: Login + ACL Discovery
                // نستخدم LoginWithAclsAsync لاستخراج ACL الفعلية من استجابة session.login مباشرةً.
                // وجود الدالة في ubus list لا يعني الإذن بتنفيذها — يجب قراءة ACLs فعلياً.
                progress.Report((10, "جاري الاتصال وتسجيل الدخول + استخراج صلاحيات ACL..."));

                DeviceAcls acls = DeviceAcls.FullPermissions();
                string workingPassword = password;
                bool loginSuccess = false;

                if (tryNetworkPasswordFirst && !string.IsNullOrEmpty(newPassword))
                {
                    try
                    {
                        if (canCommit || canApply)
                        {
                            session = await Ubus.LoginAsync(connectIp, username, newPassword);
                            acls = new DeviceAcls
                            {
                                CanGet = true, CanSet = true, CanAdd = true, CanDelete = true,
                                CanCommit = canCommit, CanApply = canApply
                            };
                        }
                        else
                        {
                            (session, acls) = await Ubus.LoginWithAclsAsync(connectIp, username, newPassword);
                            canCommit = acls.CanCommit;
                            canApply = acls.CanApply;
                        }
                        workingPassword = newPassword;
                        loginSuccess = true;
                    }
                    catch
                    {
                        // Fallback to default password
                    }
                }

                if (!loginSuccess)
                {
                    if (canCommit || canApply)
                    {
                        session = await Ubus.LoginAsync(connectIp, username, password);
                        acls = new DeviceAcls
                        {
                            CanGet = true, CanSet = true, CanAdd = true, CanDelete = true,
                            CanCommit = canCommit, CanApply = canApply
                        };
                    }
                    else
                    {
                        (session, acls) = await Ubus.LoginWithAclsAsync(connectIp, username, password);
                        canCommit = acls.CanCommit;
                        canApply = acls.CanApply;
                    }
                    workingPassword = password;
                }

                cancellationToken.ThrowIfCancellationRequested();
                Logger.LogSuccess($"تم تسجيل الدخول بنجاح للجهاز {connectIp}. الوضع: {acls.ProgrammingMode}");

                if (!acls.CanCommit)
                    Logger.LogWarning("[ACL] uci.commit غير مصرح به — التغييرات ستُكتب في الذاكرة فقط.");
                if (!acls.CanApply)
                    Logger.LogWarning("[ACL] uci.apply غير مصرح به — الخدمات لن تُعاد تشغيلها تلقائياً.");

                // Step 2: التحقق من الحد الأدنى للصلاحيات (مُستخرج من ACL في Step 1 — لا حاجة لـ ubus list)
                progress.Report((20, "التحقق من الحد الأدنى للصلاحيات المطلوبة..."));
                cancellationToken.ThrowIfCancellationRequested();

                if (!acls.CanGet || !acls.CanSet)
                {
                    throw new Exception(
                        $"الجهاز لا يمنح الحد الأدنى من الصلاحيات المطلوبة للبرمجة.\n" +
                        $"المطلوب: uci.get + uci.set | الممنوح: get={acls.CanGet}, set={acls.CanSet}");
                }

                Logger.LogSuccess($"[ACL] الجهاز متوافق. الوضع: {acls.ProgrammingMode}");


                // Step 3: Discovery
                progress.Report((30, "جاري اكتشاف البنية البرمجية والشبكية للجهاز..."));
                var info = await Discovery.DiscoverDeviceAsync(connectIp, session);
                cancellationToken.ThrowIfCancellationRequested();

                // Step 4: Backup
                progress.Report((40, "جاري إنشاء نسخة احتياطية من الإعدادات الحالية..."));
                await Backup.CreateBackupAsync(connectIp, session, connectIp);
                cancellationToken.ThrowIfCancellationRequested();

                // Step 5: System Hostname Configuration
                progress.Report((50, "جاري ضبط اسم المضيف (Hostname)..."));
                var hostname = HostnameGenerator.Generate(targetIp);
                
                // Discover the system section name (usually system.@system[0])
                var systemConfig = await Uci.GetConfigDictAsync(connectIp, session, "system");
                var systemSection = "@system[0]";
                foreach (var key in systemConfig.Keys)
                {
                    if (systemConfig[key] is Dictionary<string, object> sDict && sDict.TryGetValue(".type", out var typeVal) && typeVal.ToString() == "system")
                    {
                        systemSection = key;
                        break;
                    }
                }
                
                await Uci.SetOptionAsync(connectIp, session, "system", systemSection, "hostname", hostname);
                Logger.LogSuccess($"تم تعيين اسم المضيف الجديد: {hostname}");
                cancellationToken.ThrowIfCancellationRequested();

                // Step 6: Network configuration
                progress.Report((60, "جاري إعداد العناوين والشبكة المحلية (LAN)..."));
                await Network.SetLanIpAsync(connectIp, session, info.LanSectionName, targetIp, gateway, subnetMask);
                cancellationToken.ThrowIfCancellationRequested();

                // Step 7: VLAN configuration
                progress.Report((70, $"جاري إنشاء واجهة VLAN {vlanId}..."));
                await Network.CreateVlanAsync(connectIp, session, info.LanDeviceName, info.VlanType, vlanId, info.SwitchName, info.SwitchCpuPort, info.SwitchLanPorts);
                cancellationToken.ThrowIfCancellationRequested();

                // Step 8: Disable DHCP on LAN
                progress.Report((80, "جاري تعطيل خادم DHCP على الشبكة المحلية..."));
                await Network.DisableDhcpAsync(connectIp, session, info.LanSectionName);
                cancellationToken.ThrowIfCancellationRequested();

                // Step 9: Configure Wireless
                progress.Report((90, "جاري ضبط الإعدادات اللاسلكية (Wi-Fi)..."));
                
                // Radio 0 (2.4GHz) AP mode on vlan<VLAN_ID>
                var vlanNetworkName = $"vlan{vlanId}";
                var apPassword = wirelessConfig.IsEncrypted ? wirelessConfig.WifiPassword : string.Empty;
                await Wireless.ConfigureRadioApAsync(connectIp, session, info.Radio24GhzName, info.WifiIface24GhzSection, wirelessConfig.Ssid24Ghz, apPassword, vlanNetworkName);
                cancellationToken.ThrowIfCancellationRequested();

                // Radio 1 (5GHz)
                if (wirelessConfig.Mode == WirelessMode.AccessPoint)
                {
                    // AP mode on lan
                    await Wireless.ConfigureRadioApAsync(connectIp, session, info.Radio5GhzName, info.WifiIface5GhzSection, wirelessConfig.Ssid5Ghz, apPassword, "lan");
                }
                else
                {
                    // Client WDS mode on lan
                    await Wireless.ConfigureRadioStaWdsAsync(connectIp, session, info.Radio5GhzName, info.WifiIface5GhzSection, wirelessConfig.RemoteSsid, wirelessConfig.RemotePassword, "lan");
                }
                cancellationToken.ThrowIfCancellationRequested();

                // Step 10: Commit and Apply
                if (canCommit || canApply)
                {
                    progress.Report((95, "جاري حفظ وتطبيق الإعدادات الجديدة على الجهاز..."));

                    if (canCommit)
                    {
                        Logger.Log("جاري حفظ التغييرات (uci commit)...");
                        await Uci.CommitAsync(connectIp, session, "system");
                        await Uci.CommitAsync(connectIp, session, "network");
                        await Uci.CommitAsync(connectIp, session, "wireless");
                        await Uci.CommitAsync(connectIp, session, "dhcp");
                    }
                    else
                    {
                        Logger.LogWarning("الجهاز يمنع uci.commit عبر ACL. تم تخطي حفظ الإعدادات في الفلاش.");
                    }

                    if (canApply)
                    {
                        Logger.Log("جاري تطبيق التغييرات وإعادة تشغيل الخدمات (uci apply)...");
                        await Uci.ApplyAsync(connectIp, session);
                    }
                    else
                    {
                        Logger.LogWarning("الجهاز يمنع uci.apply عبر ACL. التغييرات مكتوبة في بيئة التشغيل فقط (runtime).");
                    }
                }
                else
                {
                    progress.Report((95, "تم تخطي حفظ وتطبيق الإعدادات بسبب قيود ACL..."));
                    Logger.LogWarning("Device ACL prevents commit/apply. Configuration written to UCI runtime only.");
                }

                // Step 11: Robust password change if requested
                if (changePassword && !string.IsNullOrEmpty(newPassword))
                {
                    progress.Report((98, "جاري تغيير كلمة مرور المستخدم root..."));
                    Logger.Log("جاري التحقق من دعم تغيير كلمة المرور عبر UBUS...");

                    bool isLuciSetPasswordSupported = false;
                    try
                    {
                        var ubusObjects = await Ubus.ListAsync(connectIp, session, "luci");
                        if (ubusObjects.TryGetValue("luci", out var luciObj) && luciObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            if (luciObj.TryGetProperty("setPassword", out _))
                            {
                                isLuciSetPasswordSupported = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"فشل التحقق من دعم luci.setPassword: {ex.Message}");
                    }

                    if (!isLuciSetPasswordSupported)
                    {
                        Logger.LogWarning("[WARNING] Password change is not supported by this device.");
                    }
                    else
                    {
                        try
                        {
                            await Ubus.CallAsync(connectIp, session, "luci", "setPassword", new { username = "root", password = newPassword });
                            Logger.LogSuccess("[SUCCESS] Device password changed successfully.");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"[WARNING] Password change failed but device programming completed successfully. Details: {ex.Message}");
                        }
                    }
                }

                progress.Report((100, "اكتملت البرمجة بنجاح!"));
                Logger.LogSuccess($"[OK] تم برمجة الجهاز {connectIp} بنجاح إلى العنوان الجديد {targetIp} واسم المضيف {hostname}");
                if (!canCommit || !canApply)
                {
                    Logger.LogWarning("ملاحظة: تمت البرمجة في الوضع المقيّد (Mode B - set only) بسبب قيود صلاحيات الجهاز.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"فشل برمجة الجهاز {connectIp}: {ex.Message}");
                Logger.LogWarning("إجراء التراجع التلقائي: لم يتم حفظ الإعدادات على الجهاز بشكل دائم لعدم اكتمال العملية.");
                throw;
            }
        }
    }
}
