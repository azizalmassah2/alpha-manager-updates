using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.Management.Console.Core.Security.Audit;
using Lux.Management.Console.Core.Security.Models;
using Lux.Management.Console.Core.Security.Session;
using Lux.Platform.Abstractions.Interfaces;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Entities.Platform;
using MikroTikVoucherPrinter.Domain.Interfaces.Platform;

namespace Lux.Management.Console.Core.Session;

/// <summary>
/// تنفيذ IConnectionService لتنسيق عمليات الاتصال بالراوتر والتحقق من الترخيص وبناء جلسة العمل
/// </summary>
public class ConnectionService : IConnectionService
{
    private readonly IRouterSessionService _routerSessionService;
    private readonly ILicenseService _licenseService;
    private readonly IActiveRouterContext _activeRouterContext;
    private readonly IRouterRepository _routerRepository;
    private readonly ISecureStorageService _secureStorageService;
    private readonly IHotspotService _hotspotService;
    private readonly ISessionSecurityService _sessionSecurityService;
    private readonly ISecurityAuditService _securityAuditService;

    public ConnectionService(
        IRouterSessionService routerSessionService,
        ILicenseService licenseService,
        IActiveRouterContext activeRouterContext,
        IRouterRepository routerRepository,
        ISecureStorageService secureStorageService,
        IHotspotService hotspotService,
        ISessionSecurityService sessionSecurityService,
        ISecurityAuditService securityAuditService)
    {
        _routerSessionService = routerSessionService;
        _licenseService = licenseService;
        _activeRouterContext = activeRouterContext;
        _routerRepository = routerRepository;
        _secureStorageService = secureStorageService;
        _hotspotService = hotspotService;
        _sessionSecurityService = sessionSecurityService;
        _securityAuditService = securityAuditService;
    }

    public async Task<RouterConnectionResult> ConnectAsync(
        string host, int port, string username, string password,
        CancellationToken cancellationToken = default)
    {
        // 1. الاتصال بالراوتر وجلب تفاصيله الكاملة
        var result = await _routerSessionService.ConnectAndGetInfoAsync(host, port, username, password, cancellationToken);
        if (!result.IsSuccess || result.RouterInfo == null)
        {
            return result;
        }

        // 2. تحديث أو إدخال الراوتر في قاعدة البيانات المحلية وحفظه
        var existingList = await _routerRepository.GetAllAsync();
        var existing = existingList.FirstOrDefault(r =>
            r.Host.Equals(host, StringComparison.OrdinalIgnoreCase) && r.Port == port);

        Router dbRouter;
        if (existing != null)
        {
            existing.DisplayName = result.RouterInfo.Identity;
            existing.Username = username;
            existing.EncryptedPassword = _secureStorageService.Encrypt(password);
            await _routerRepository.UpdateAsync(existing);
            dbRouter = existing;
        }
        else
        {
            dbRouter = new Router
            {
                DisplayName = result.RouterInfo.Identity,
                Host = host,
                Port = port,
                Username = username,
                EncryptedPassword = _secureStorageService.Encrypt(password)
            };
            await _routerRepository.AddAsync(dbRouter);
        }

        result.RouterInfo.RouterId = dbRouter.Id;

        // 3. الاتصال في سياق الراوتر النشط للتطبيق (IActiveRouterContext) لكي تعمل الشاشات الأخرى بشكل متزامن
        await _activeRouterContext.SwitchRouterAsync(dbRouter, cancellationToken);

        return result;
    }

    public async Task<LicenseVerificationResult> VerifyLicenseAsync(RouterInfo routerInfo, CancellationToken cancellationToken = default)
    {
        try
        {
            var router = await _routerRepository.GetByIdAsync(routerInfo.RouterId);
            if (router != null)
            {
                var password = _secureStorageService.Decrypt(router.EncryptedPassword);
                var remoteLicensePath = "license/license.dat";
                var licenseContent = await _hotspotService.DownloadFileFtpAsync(router.Host, router.Username, password, remoteLicensePath);

                if (!string.IsNullOrEmpty(licenseContent))
                {
                    var localLicensePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.dat");
                    await System.IO.File.WriteAllTextAsync(localLicensePath, licenseContent, cancellationToken);

                    var localBackupDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LuxCard");
                    if (!System.IO.Directory.Exists(localBackupDir))
                    {
                        System.IO.Directory.CreateDirectory(localBackupDir);
                    }
                    await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(localBackupDir, "license.backup"), licenseContent, cancellationToken);
                }
            }
        }
        catch
        {
            // Fallback to local files quietly
        }

        if (!_licenseService.HasStoredLicense())
        {
            _securityAuditService.LogEvent(new LicenseAuditEvent(AuditSeverity.Info, routerInfo.RouterId.ToString(), routerInfo.SerialNumber, "NoLicense", $"No license file stored locally for Router '{routerInfo.RouterId}'."));
            return LicenseVerificationResult.NoLicense();
        }

        var status = _licenseService.ValidateLicenseOffline(routerInfo.SerialNumber);
        
        DateTime? expiryDate = null;
        if (status == LicenseStatus.Valid)
        {
            try
            {
                var localLicensePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.dat");
                if (System.IO.File.Exists(localLicensePath))
                {
                    var json = System.IO.File.ReadAllText(localLicensePath);
                    var licenseInfo = System.Text.Json.JsonSerializer.Deserialize<MikroTikVoucherPrinter.Application.Models.LicenseInfo>(json);
                    if (licenseInfo != null)
                    {
                        expiryDate = licenseInfo.ExpiryDate;
                    }
                }
            }
            catch {}
        }

        if (status == LicenseStatus.Valid)
        {
            _securityAuditService.LogEvent(new LicenseAuditEvent(AuditSeverity.Info, routerInfo.RouterId.ToString(), routerInfo.SerialNumber, "Valid", $"License validation succeeded for Router '{routerInfo.RouterId}' with Serial '{routerInfo.SerialNumber}'."));
        }
        else
        {
            _securityAuditService.LogEvent(new LicenseAuditEvent(AuditSeverity.Warning, routerInfo.RouterId.ToString(), routerInfo.SerialNumber, status.ToString(), $"License validation failed for Router '{routerInfo.RouterId}' with Serial '{routerInfo.SerialNumber}'. Result Status: {status}."));
        }

        return status switch
        {
            LicenseStatus.Valid => LicenseVerificationResult.Valid(expiryDate),
            LicenseStatus.InvalidRouter => LicenseVerificationResult.Mismatch(),
            LicenseStatus.Corrupted => LicenseVerificationResult.Corrupted(),
            _ => LicenseVerificationResult.Corrupted()
        };
    }

    public Task<ApplicationSession> CreateSessionAsync(RouterInfo? routerInfo, LicenseVerificationResult? licenseResult)
    {
        if (routerInfo == null)
        {
            // لا يوجد راوتر متصل -> جلسة مجانية بدون راوتر
            return Task.FromResult(ApplicationSession.CreateFreeSession());
        }

        if (licenseResult == null || !licenseResult.IsValid)
        {
            // يوجد راوتر ولكن لا يوجد ترخيص صالح -> جلسة مجانية بالراوتر
            return Task.FromResult(ApplicationSession.CreateFreeSession(routerInfo));
        }

        // توليد رمز الجلسة الآمن (HMAC Session Token) المربوط بالرقم التسلسلي للراوتر
        var sessionToken = _sessionSecurityService.GenerateSessionToken(routerInfo.SerialNumber, true);

        // يوجد راوتر وترخيص صالح -> جلسة احترافية بالراوتر مع رمز الجلسة الموقع
        return Task.FromResult(ApplicationSession.CreateProSession(routerInfo, licenseResult.ExpiresAt, sessionToken));
    }
}
