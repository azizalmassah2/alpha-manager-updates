using System.Threading;
using System.Threading.Tasks;

namespace Lux.Management.Console.Core.Session;

/// <summary>
/// نتيجة محاولة الاتصال بالراوتر
/// </summary>
public class RouterConnectionResult
{
    public bool IsSuccess { get; set; }
    public RouterInfo? RouterInfo { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public static RouterConnectionResult Success(RouterInfo info)
        => new() { IsSuccess = true, RouterInfo = info };

    public static RouterConnectionResult Failure(string message)
        => new() { IsSuccess = false, ErrorMessage = message };
}

/// <summary>
/// خدمة الاتصال بالراوتر وجلب المعلومات — تُستخدم من LoginViewModel
/// </summary>
public interface IRouterSessionService
{
    /// <summary>
    /// الاتصال بالراوتر وجلب معلوماته الكاملة (Identity, SN, SoftwareId, Version, Board)
    /// </summary>
    Task<RouterConnectionResult> ConnectAndGetInfoAsync(
        string host, int port, string username, string password,
        CancellationToken ct = default);
}
