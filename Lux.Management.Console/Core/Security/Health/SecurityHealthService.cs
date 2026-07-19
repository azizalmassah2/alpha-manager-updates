using System;
using Lux.Management.Console.Core.Security.Context;
using Lux.Management.Console.Core.Security.Models;
using Lux.Management.Console.Core.Security.Diagnostics;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace Lux.Management.Console.Core.Security.Health;

/// <summary>
/// تطبيق ISecurityHealthService لتقديم تقرير تشخيصي لحظي لسلامة البنية الأمنية للبرنامج.
/// </summary>
public class SecurityHealthService : ISecurityHealthService
{
    private readonly ISecurityContext _securityContext;
    private readonly IAntiTamperService _antiTamperService;
    private readonly IPublicKeyProvider _publicKeyProvider;
    private DateTime _lastValidationTime = DateTime.UtcNow;

    public SecurityHealthService(
        ISecurityContext securityContext,
        IAntiTamperService antiTamperService,
        IPublicKeyProvider publicKeyProvider)
    {
        _securityContext = securityContext;
        _antiTamperService = antiTamperService;
        _publicKeyProvider = publicKeyProvider;
    }

    public SecurityHealthSnapshot GetHealthSnapshot()
    {
        _lastValidationTime = DateTime.UtcNow;

        var session = _securityContext.CurrentSession;
        var licenseState = session?.LicenseState switch
        {
            Lux.Management.Console.Core.Session.LicenseState.Valid => LicenseState.Valid,
            Lux.Management.Console.Core.Session.LicenseState.RouterMismatch => LicenseState.InvalidRouter,
            Lux.Management.Console.Core.Session.LicenseState.Corrupted => LicenseState.Corrupted,
            Lux.Management.Console.Core.Session.LicenseState.Expired => LicenseState.Expired,
            _ => LicenseState.NoLicense
        };

        var isDebuggerDetected = _antiTamperService.DetectDebugger();
        var isIntegrityValid = _antiTamperService.VerifyLoadedAssemblies();
        var isPublicKeyValid = _publicKeyProvider.VerifyPublicKeyIntegrity();

        var isHealthy = _securityContext.IsAuthenticated && 
                         isPublicKeyValid && 
                         !isDebuggerDetected && 
                         isIntegrityValid && 
                         licenseState == LicenseState.Valid;

        return new SecurityHealthSnapshot
        {
            Session = _securityContext.IsAuthenticated ? SessionState.Active : SessionState.Inactive,
            License = licenseState,
            Router = _securityContext.CurrentRouter != null ? RouterState.Connected : RouterState.Disconnected,
            Runtime = RuntimeState.Monitoring,
            Integrity = isIntegrityValid ? IntegrityState.Valid : IntegrityState.Tampered,
            LastValidation = _lastValidationTime,
            IsHealthy = isHealthy
        };
    }
}
