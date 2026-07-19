using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Domain.Entities;

namespace MikroTikVoucherPrinter.Application.Interfaces;

using MikroTikVoucherPrinter.Domain.Enums;

public interface IProfileService
{
    Task<IReadOnlyList<Profile>> GetAllProfilesAsync(PackageSourceType sourceType, CancellationToken cancellationToken = default);
    Task<Profile> CreateProfileAsync(PackageSourceType sourceType, string name, string validity, string transfer, string uptime, string rateLimit, string sharedUsers, decimal price, CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(PackageSourceType sourceType, string name, string validity, string transfer, string uptime, string sharedUsers, decimal price, CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(PackageSourceType sourceType, Profile profile, CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(PackageSourceType sourceType, Guid id, CancellationToken cancellationToken = default);
    Task DeleteProfileByNameAsync(PackageSourceType sourceType, string name, CancellationToken cancellationToken = default);
}
