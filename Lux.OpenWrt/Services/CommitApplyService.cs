using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Microsoft.Extensions.Logging;

namespace Lux.OpenWrt.Services;

public class CommitApplyService : ICommitApplyService
{
    private readonly IUciService _uci;
    private readonly ILogger<CommitApplyService> _logger;

    public CommitApplyService(IUciService uci, ILogger<CommitApplyService> logger)
    {
        _uci = uci;
        _logger = logger;
    }

    public async Task CommitAndApplyAsync(string ip, string session, bool canCommit, bool canApply, CancellationToken cancellationToken = default)
    {
        if (canCommit)
        {
            _logger.LogInformation("جاري حفظ التغييرات (uci commit)...");
            await _uci.CommitAsync(ip, session, "system", cancellationToken);
            await _uci.CommitAsync(ip, session, "network", cancellationToken);
            await _uci.CommitAsync(ip, session, "wireless", cancellationToken);
            await _uci.CommitAsync(ip, session, "dhcp", cancellationToken);
        }
        else
        {
            _logger.LogWarning("الجهاز يمنع uci.commit عبر ACL. تم تخطي حفظ الإعدادات في الفلاش.");
        }

        if (canApply)
        {
            _logger.LogInformation("جاري تطبيق التغييرات وإعادة تشغيل الخدمات (uci apply)...");
            await _uci.ApplyAsync(ip, session, cancellationToken);
        }
        else
        {
            _logger.LogWarning("الجهاز يمنع uci.apply عبر ACL. التغييرات مكتوبة في بيئة التشغيل فقط (runtime).");
        }
    }
}
