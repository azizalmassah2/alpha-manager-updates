using System.Linq;
using tik4net;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

/// <summary>
/// يقرأ معرفاً ثابتاً للراوتر (يفضّل serial-number الحقيقي، ثم بدائل الترخيص).
/// </summary>
public static class MikroTikRouterSerialReader
{
    /// <summary>
    /// محاولة بالترتيب: serial-number من RouterBOARD، ثم من الترخيص، ثم system-id من الترخيص.
    /// </summary>
    public static string? TryReadStableDeviceId(ITikConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var sn = TryWordValue(connection, "/system/routerboard/print", "serial-number");
        if (IsMeaningfulSerial(sn))
            return sn!.Trim();

        sn = TryWordValue(connection, "/system/license/print", "serial-number");
        if (IsMeaningfulSerial(sn))
            return sn!.Trim();

        var systemId = TryWordValue(connection, "/system/license/print", "system-id");
        return string.IsNullOrWhiteSpace(systemId) ? null : systemId.Trim();
    }

    private static string? TryWordValue(ITikConnection connection, string commandPath, string wordKey)
    {
        try
        {
            var row = connection.CreateCommandAndParameters(commandPath).ExecuteList().FirstOrDefault();
            if (row == null) return null;
            var w = row.Words.FirstOrDefault(x => x.Key == wordKey);
            return string.IsNullOrEmpty(w.Value) ? null : w.Value;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsMeaningfulSerial(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (s.Equals("none", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}
