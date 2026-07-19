using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.Domain.Interfaces;

/// <summary>
/// [LEGACY] ظ‡ط°ظ‡ ط§ظ„ظˆط§ط¬ظ‡ط© ظ„ط§ طھظ…ظ„ظƒ طھظ†ظپظٹط°ط§ظ‹ ظپط¹ظ„ظٹط§ظ‹.
/// ط§ظ„ظˆط§ط¬ظ‡ط© ط§ظ„ظپط¹ظ‘ط§ظ„ط© ظ‡ظٹ <see cref="MikroTikVoucherPrinter.Application.Interfaces.IMikroTikIntegrationService"/>.
/// طھظڈط­ظپط¸ ظ‡ظ†ط§ ظ„ط£ط؛ط±ط§ط¶ ط§ظ„طھظˆط§ظپظ‚ ط§ظ„ظ…ط³طھظ‚ط¨ظ„ظٹ ظپظ‚ط· â€” ظ„ط§ طھط³طھط®ط¯ظ…ظ‡ط§ ظپظٹ ظƒظˆط¯ ط¬ط¯ظٹط¯.
/// </summary>
[Obsolete("ط§ط³طھط®ط¯ظ… IMikroTikIntegrationService ظ…ظ† Application.Interfaces ط¨ط¯ظ„ط§ظ‹ ظ…ظ† ظ‡ط°ظ‡ ط§ظ„ظˆط§ط¬ظ‡ط©.", error: false)]
public interface IMikroTikService : IDisposable
{
    /// <summary>ط­ط§ظ„ط© ط§ظ„ط§طھطµط§ظ„ ط§ظ„ط­ط§ظ„ظٹط©</summary>
    ConnectionStatus Status { get; }

    /// <summary>ط§ظ„ط§طھطµط§ظ„ ط¨ط§ظ„ط±ط§ظˆطھط±</summary>
    Task<Result> ConnectAsync(string host, int port, string username, string password,
        CancellationToken cancellationToken = default);

    /// <summary>ظ‚ط·ط¹ ط§ظ„ط§طھطµط§ظ„</summary>
    Task DisconnectAsync();

    /// <summary>ظپط­طµ ط§ظ„ط§طھطµط§ظ„</summary>
    Task<Result> TestConnectionAsync(string host, int port, string username, string password,
        CancellationToken cancellationToken = default);

    /// <summary>ط¬ظ„ط¨ ط§ظ„ط¨ط±ظˆظپط§ظٹظ„ط§طھ ظ…ظ† User Manager</summary>
    Task<Result<IReadOnlyList<string>>> GetProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>ط¥ظ†ط´ط§ط، ظ…ط³طھط®ط¯ظ…ظٹظ† (ظƒط±ظˆطھ) ظپظٹ User Manager</summary>
    Task<Result<int>> CreateUsersAsync(IEnumerable<UserCreateRequest> users,
        CancellationToken cancellationToken = default);

    /// <summary>ط­ط°ظپ ظ…ط³طھط®ط¯ظ… ظ…ظ† User Manager</summary>
    Task<Result> DeleteUserAsync(string username, CancellationToken cancellationToken = default);
}

/// <summary>
/// ط·ظ„ط¨ ط¥ظ†ط´ط§ط، ظ…ط³طھط®ط¯ظ… ظپظٹ ط§ظ„ظ…ط§ظٹظƒط±ظˆطھظƒ
/// </summary>
public record UserCreateRequest(
    string Username,
    string Password,
    string Profile,
    string Comment = "");

