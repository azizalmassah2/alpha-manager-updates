using MikroTikVoucherPrinter.Application.DTOs;
using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IMikroTikIntegrationService
{
    Task<Result<MikroTikUserResult>> CreateUserAsync(string username, string? password, string profileName, CancellationToken cancellationToken = default);
    Task<Result> DeleteUserAsync(string username, string? externalId = null, CancellationToken cancellationToken = default);
    Task<Dictionary<string, Result>> DeleteUsersBulkAsync(IEnumerable<(string username, string? externalId)> users, IProgress<(int success, int failed, int total)>? progress = null, CancellationToken cancellationToken = default);
    Task<Dictionary<string, Result<MikroTikUserResult>>> CreateUsersBulkAsync(IEnumerable<(string username, string? password, string profileName)> users, IProgress<(int success, int failed, int total)>? progress = null, int initialSuccess = 0, int initialFailed = 0, CancellationToken cancellationToken = default);
}
