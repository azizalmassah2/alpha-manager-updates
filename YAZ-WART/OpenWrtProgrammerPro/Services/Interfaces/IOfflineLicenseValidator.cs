namespace OpenWrtProgrammerPro.Services.Interfaces
{
    public interface IOfflineLicenseValidator : ILicenseValidator
    {
        string GetOfflineStatePath();
    }
}
