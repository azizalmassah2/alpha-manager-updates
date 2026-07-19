using System.Threading.Tasks;
using OpenWrtProgrammerPro.Models;

namespace OpenWrtProgrammerPro.Services.Interfaces
{
    public interface ILicenseValidator
    {
        Task<LicenseValidationResult> ValidateLicenseAsync();
        LicenseModel? ActiveLicense { get; }
        string GetHardwareId();
        Task<bool> LoadAndActivateLicenseAsync(string licenseFilePath);
    }
}
