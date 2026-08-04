using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.Interfaces;

/// <summary>
/// Factory لاختيار مزود اسكريبتات الصيانة المناسب بناءً على إصدار الراوتر المتصل.
/// </summary>
public interface IMaintenanceScriptProviderFactory
{
    /// <summary>
    /// يُرجع مزود الاسكريبتات المناسب للراوتر المتصل حالياً.
    /// يعتمد على نفس RouterCapabilityService Cache المستخدم في CommandProviderFactory.
    /// </summary>
    Task<IMaintenanceScriptProvider> GetProviderAsync(CancellationToken ct = default);
}
