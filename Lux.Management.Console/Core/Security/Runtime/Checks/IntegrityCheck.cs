using System.Security.Cryptography;
using Lux.Management.Console.Core.Security.Diagnostics;

namespace Lux.Management.Console.Core.Security.Runtime.Checks;

/// <summary>
/// فحص أمني محدد للتأكد من عدم حقن تجميعات غريبة أو إجراء باتش للملفات في الذاكرة.
/// </summary>
public class IntegrityCheck : IIntegrityCheck
{
    private readonly IAntiTamperService _antiTamperService;

    public string CheckName => "IntegrityCheck";

    public IntegrityCheck(IAntiTamperService antiTamperService)
    {
        _antiTamperService = antiTamperService;
    }

    public bool CanRun(int level)
    {
        // يعمل في المستوى الثالث (الفحوصات المكثفة للسلامة التامة)
        return level == 3;
    }

    public void Execute()
    {
        if (!_antiTamperService.VerifyLoadedAssemblies())
        {
            throw new CryptographicException("Assembly integrity check failed: loaded modules were modified.");
        }
    }
}
