namespace OpenWrtProgrammerPro.Models
{
    public enum LicenseStatus
    {
        Valid,
        Missing,
        Expired,
        HardwareMismatch,
        TimeManipulation,
        SignatureFailed,
        Revoked,
        IntegrityViolation
    }

    public class LicenseValidationResult
    {
        public LicenseStatus Status { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int RemainingDays { get; set; }
        public int RemainingGraceDays { get; set; }
    }
}
