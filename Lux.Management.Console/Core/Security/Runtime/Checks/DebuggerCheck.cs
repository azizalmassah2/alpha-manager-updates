using System.Security.Cryptography;
using Lux.Management.Console.Core.Security.Diagnostics;

namespace Lux.Management.Console.Core.Security.Runtime.Checks;

/// <summary>
/// فحص أمني محدد لرصد مصححات الأخطاء النشطة بالاعتماد على IAntiTamperService.
/// </summary>
public class DebuggerCheck : IDebuggerCheck
{
    private readonly IAntiTamperService _antiTamperService;

    public string CheckName => "DebuggerCheck";

    public DebuggerCheck(IAntiTamperService antiTamperService)
    {
        _antiTamperService = antiTamperService;
    }

    public bool CanRun(int level)
    {
        // يعمل في المستوى الأول (فحوصات لحظية) والمستوى الثاني (فحوصات دورية)
        return level == 1 || level == 2;
    }

    public void Execute()
    {
#if !DEBUG
        if (_antiTamperService.DetectDebugger())
        {
            throw new CryptographicException("Debugger detected active in program memory.");
        }
#endif
    }
}
