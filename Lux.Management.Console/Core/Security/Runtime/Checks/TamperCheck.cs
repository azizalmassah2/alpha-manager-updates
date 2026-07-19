using System.Security.Cryptography;
using Lux.Management.Console.Core.Security.Diagnostics;

namespace Lux.Management.Console.Core.Security.Runtime.Checks;

/// <summary>
/// فحص تكميلي للتحقق من سلامة البروسس من محاولات التلاعب المباشر.
/// </summary>
public class TamperCheck : ITamperCheck
{
    private readonly IAntiTamperService _antiTamperService;

    public string CheckName => "TamperCheck";

    public TamperCheck(IAntiTamperService antiTamperService)
    {
        _antiTamperService = antiTamperService;
    }

    public bool CanRun(int level)
    {
        return level == 3;
    }

    public void Execute()
    {
        if (!_antiTamperService.VerifyLoadedAssemblies())
        {
            throw new CryptographicException("Tamper attempt detected during memory assembly verification.");
        }
    }
}
