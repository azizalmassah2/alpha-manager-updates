using System;
using Lux.Management.Console.Core.Security.Context;
using Lux.Management.Console.Core.Security.Session;
using Lux.Management.Console.Core.Security.Models;
using Lux.Management.Console.Core.Security.Audit;
using Lux.Management.Console.Core.Security.Configuration;

namespace Lux.Management.Console.Core.Security.Authorization;

/// <summary>
/// تطبيق IFeatureAuthorizationService للتحقق المركزي من صلاحيات الميزات والحدود باستخدام ISecurityContext.
/// </summary>
public class FeatureAuthorizationService : IFeatureAuthorizationService
{
    private readonly ISecurityContext _securityContext;
    private readonly ISessionSecurityService _sessionSecurityService;
    private readonly ISecurityAuditService _securityAuditService;

    public FeatureAuthorizationService(
        ISecurityContext securityContext, 
        ISessionSecurityService sessionSecurityService,
        ISecurityAuditService securityAuditService)
    {
        _securityContext = securityContext;
        _sessionSecurityService = sessionSecurityService;
        _securityAuditService = securityAuditService;
    }

    public bool HasFeature(FeatureId featureId)
    {
        if (!_securityContext.IsAuthenticated) return false;

        // التحقق من صلاحية وسلامة التوقيع للجلسة النشطة
        if (_securityContext.IsProMode && _securityContext.CurrentRouter != null)
        {
            var token = _securityContext.SessionToken;
            if (string.IsNullOrEmpty(token)) return false;

            if (_sessionSecurityService.ValidateSessionToken(token, _securityContext.CurrentRouter.SerialNumber, out bool isPro))
            {
                if (isPro)
                {
                    // التحقق من الميزة باستخدام كائن FeatureSet المنسق
                    return _securityContext.CurrentFeatureSet.Has(featureId);
                }
            }
        }

        // في وضع Free Mode، نقوم بفحص كائن الميزات المجاني المتاح
        var isAllowed = _securityContext.CurrentFeatureSet.Has(featureId);

        if (!isAllowed)
        {
            // توثيق محاولة الوصول المرفوضة في سجل التدقيق الأمني المحلي المشفر
            _securityAuditService.LogEvent(new AuthorizationAuditEvent(
                AuditSeverity.Warning,
                featureId,
                _securityContext.CurrentSession?.UserName ?? "المستخدم",
                $"Access denied to feature '{featureId}'.")
            {
                CorrelationId = _securityContext.SessionId.ToString()
            });
        }

        return isAllowed;
    }

    public void RequireFeature(FeatureId featureId)
    {
        if (!HasFeature(featureId))
        {
            throw new UnauthorizedAccessException($"❌ الميزة '{featureId}' غير متوفرة في النسخة الحالية.");
        }
    }

    public bool CanExecute(FeatureId featureId, object context)
    {
        if (featureId == FeatureId.VoucherGeneration || featureId == FeatureId.VoucherPrinting)
        {
            if (context is int count)
            {
                if (count > SecurityConfiguration.MaxFreeVouchersLimit)
                {
                    var hasAccess = HasFeature(featureId);
                    if (!hasAccess)
                    {
                        _securityAuditService.LogEvent(new AuthorizationAuditEvent(
                            AuditSeverity.Warning,
                            featureId,
                            _securityContext.CurrentSession?.UserName ?? "المستخدم",
                            $"Voucher limit exceeded. Attempted count: {count}, Max limit: {SecurityConfiguration.MaxFreeVouchersLimit}.")
                        {
                            CorrelationId = _securityContext.SessionId.ToString()
                        });
                    }
                    return hasAccess;
                }
                return true;
            }
        }
        return true;
    }
}
