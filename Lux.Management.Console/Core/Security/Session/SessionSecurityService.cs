namespace Lux.Management.Console.Core.Security.Session;

/// <summary>
/// تطبيق ISessionSecurityService واجهة أمان وتوليد رموز الجلسات باستخدام مكونات منفصلة للمسؤوليات.
/// </summary>
public class SessionSecurityService : ISessionSecurityService
{
    private readonly SessionTokenFactory _tokenFactory;
    private readonly SessionTokenValidator _tokenValidator;

    public SessionSecurityService()
    {
        var keyGenerator = new SessionKeyGenerator();
        _tokenFactory = new SessionTokenFactory(keyGenerator);
        _tokenValidator = new SessionTokenValidator(keyGenerator);
    }

    public string GenerateSessionToken(string routerSerial, bool isPro)
    {
        return _tokenFactory.CreateToken(routerSerial, isPro);
    }

    public bool ValidateSessionToken(string token, string routerSerial, out bool isPro)
    {
        return _tokenValidator.ValidateToken(token, routerSerial, out isPro);
    }
}
