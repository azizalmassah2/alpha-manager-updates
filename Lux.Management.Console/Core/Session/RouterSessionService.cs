using System;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Models;
using Lux.MikroTik.Providers;

namespace Lux.Management.Console.Core.Session;

/// <summary>
/// تنفيذ IRouterSessionService — يتصل بالراوتر عبر RouterOS API ويجلب معلوماته الكاملة
/// </summary>
public class RouterSessionService : IRouterSessionService
{
    private readonly IRouterOsProvider _routerOsProvider;

    public RouterSessionService(IRouterOsProvider routerOsProvider)
    {
        _routerOsProvider = routerOsProvider;
    }

    public async Task<RouterConnectionResult> ConnectAndGetInfoAsync(
        string host, int port, string username, string password,
        CancellationToken ct = default)
    {
        try
        {
            // 1. محاولة الاتصال
            var options = new MikroTikConnectionOptions
            {
                Host = host,
                Port = port,
                Username = username,
                Password = password,
                UseSsl = false,
                ProviderType = RouterOsProviderType.Api
            };

            var connectResult = await _routerOsProvider.ConnectAsync(options);
            if (!connectResult.IsSuccess)
            {
                return RouterConnectionResult.Failure(
                    GetFriendlyError(connectResult.ErrorMessage ?? "فشل الاتصال"));
            }

            // 2. جلب الهوية
            var info = new RouterInfo
            {
                Host = host,
                Port = port,
                Username = username
            };

            // /system/identity/print
            try
            {
                var idResult = await _routerOsProvider.ExecuteAsync(
                    new MikroTikCommand { Command = "/system/identity/print" });

                if (idResult.IsSuccess && idResult.Value?.RawData?.Count > 0)
                {
                    var d = idResult.Value.RawData[0];
                    if (d.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name))
                        info.Identity = name.Trim();
                }
            }
            catch { /* identity is optional */ }

            // 3. جلب معلومات الجهاز /system/resource/print
            try
            {
                var resResult = await _routerOsProvider.ExecuteAsync(
                    new MikroTikCommand { Command = "/system/resource/print" });

                if (resResult.IsSuccess && resResult.Value?.RawData?.Count > 0)
                {
                    var d = resResult.Value.RawData[0];
                    if (d.TryGetValue("version", out var ver)) info.RouterOsVersion = ver;
                    if (d.TryGetValue("board-name", out var board)) info.BoardModel = board;
                    if (d.TryGetValue("architecture-name", out var arch)) info.Architecture = arch;
                }
            }
            catch { /* resource info is optional */ }

            // 4. جلب الرقم التسلسلي وSoftware ID /system/license/print
            try
            {
                var licResult = await _routerOsProvider.ExecuteAsync(
                    new MikroTikCommand { Command = "/system/license/print" });

                if (licResult.IsSuccess && licResult.Value?.RawData?.Count > 0)
                {
                    var d = licResult.Value.RawData[0];
                    if (d.TryGetValue("software-id", out var swId)) info.SoftwareId = swId;
                }
            }
            catch { /* license info optional */ }

            // 5. الرقم التسلسلي /system/routerboard/print
            try
            {
                var rbResult = await _routerOsProvider.ExecuteAsync(
                    new MikroTikCommand { Command = "/system/routerboard/print" });

                if (rbResult.IsSuccess && rbResult.Value?.RawData?.Count > 0)
                {
                    var d = rbResult.Value.RawData[0];
                    if (d.TryGetValue("serial-number", out var sn)) info.SerialNumber = sn;
                    if (d.TryGetValue("model", out var model) && string.IsNullOrEmpty(info.BoardModel))
                        info.BoardModel = model;
                }
            }
            catch { /* routerboard optional (not available on all models) */ }

            return RouterConnectionResult.Success(info);
        }
        catch (OperationCanceledException)
        {
            return RouterConnectionResult.Failure("تم إلغاء عملية الاتصال.");
        }
        catch (Exception ex)
        {
            return RouterConnectionResult.Failure(GetFriendlyError(ex.Message));
        }
    }

    private static string GetFriendlyError(string raw)
    {
        var low = raw.ToLowerInvariant();

        if (low.Contains("login") || low.Contains("authenticate") ||
            low.Contains("password") || low.Contains("credentials") || low.Contains("user"))
            return "❌ اسم المستخدم أو كلمة المرور غير صحيحة.";

        if (low.Contains("timed out") || low.Contains("timeout"))
            return "❌ انتهت مهلة الاتصال. تأكد من أن الجهاز قابل للوصول.";

        if (low.Contains("refused") || low.Contains("reset"))
            return "❌ تم رفض الاتصال. تأكد من تشغيل خدمة API على منفذ 8728.";

        if (low.Contains("no such host") || low.Contains("unreachable") ||
            low.Contains("network") || low.Contains("host"))
            return "❌ لا يمكن الوصول إلى الجهاز. تحقق من عنوان IP والاتصال بالشبكة.";

        return $"❌ فشل الاتصال: {raw}";
    }
}
