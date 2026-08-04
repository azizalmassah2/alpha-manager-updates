using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// Factory لاختيار مزود أوامر RouterOS المناسب بناءً على إصدار الراوتر المتصل.
/// يعتمد على RouterCapabilityService (مع Cache) ولا يُعيد الاستعلام عن الإصدار.
/// </summary>
public interface IMikroTikCommandProviderFactory
{
    /// <summary>
    /// يُرجع المزود المناسب للراوتر المتصل حالياً.
    /// النتيجة محفوظة في Cache مرتبطة بـ RouterId — لا استعلامات متكررة.
    /// </summary>
    Task<IMikroTikCommandProvider> GetProviderAsync(CancellationToken ct = default);
}
