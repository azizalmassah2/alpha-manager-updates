using System.Security.Cryptography;
using Lux.Management.Console.Core.Security.Context;
using Lux.Management.Console.Core.Security.Session;

namespace Lux.Management.Console.Core.Security.Runtime.Checks;

/// <summary>
/// فحص أمني محدد للتحقق من صحة وسلامة توكن الجلسة الحالية وتوقيعها الرقمي.
/// </summary>
public class SessionValidationCheck : ISessionValidationCheck
{
    private readonly ISecurityContext _securityContext;
    private readonly ISessionSecurityService _sessionSecurityService;

    public string CheckName => "SessionValidationCheck";

    public SessionValidationCheck(ISecurityContext securityContext, ISessionSecurityService sessionSecurityService)
    {
        _securityContext = securityContext;
        _sessionSecurityService = sessionSecurityService;
    }

    public bool CanRun(int level)
    {
        // يعمل في المستوى الأول (لحظي عند كل عملية هامة)
        return level == 1;
    }

    public void Execute()
    {
        if (_securityContext.IsAuthenticated && _securityContext.IsProMode && _securityContext.CurrentRouter != null)
        {
            var token = _securityContext.SessionToken;
            if (string.IsNullOrEmpty(token) ||
                !_sessionSecurityService.ValidateSessionToken(token, _securityContext.CurrentRouter.SerialNumber, out bool isPro) ||
                !isPro)
            {
                throw new CryptographicException("Invalid or tampered Session Token during check.");
            }
        }
    }
}
