using System;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Services.CommandProviders;

/// <summary>
/// مزود اسكريبتات الصيانة لـ Hotspot.
/// الصيانة غير مدعومة على Hotspot — جميع الدوال ترمي NotSupportedException.
/// </summary>
public sealed class HotspotMaintenanceScriptProvider : IMaintenanceScriptProvider
{
    public string CleanQuotaScriptName   => throw new NotSupportedException("Hotspot does not support quota-based maintenance scripts.");
    public string CleanTimeScriptName    => throw new NotSupportedException("Hotspot does not support time-based maintenance scripts.");
    public string CleanSessionsScriptName => throw new NotSupportedException("Hotspot does not support session maintenance scripts.");

    public string BuildCleanQuotaScript()
        => throw new NotSupportedException("Maintenance scripts are not supported for Hotspot routers.");

    public string BuildCleanTimeScript()
        => throw new NotSupportedException("Maintenance scripts are not supported for Hotspot routers.");

    public string BuildCleanSessionsScript()
        => throw new NotSupportedException("Maintenance scripts are not supported for Hotspot routers.");
}
