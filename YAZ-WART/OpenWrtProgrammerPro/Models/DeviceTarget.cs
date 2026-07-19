using OpenWrtProgrammerPro.Helpers;

namespace OpenWrtProgrammerPro.Models
{
    public enum TargetStatus
    {
        Pending,
        InProgress,
        Success,
        Failed
    }

    public class DeviceTarget : ObservableObject
    {
        private string _ipAddress = string.Empty;
        private TargetStatus _status = TargetStatus.Pending;
        private string _errorMessage = string.Empty;

        public string IpAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        public TargetStatus Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public string StatusText => Status switch
        {
            TargetStatus.Pending => "قيد الانتظار",
            TargetStatus.InProgress => "جاري البرمجة...",
            TargetStatus.Success => "✓ ناجح",
            TargetStatus.Failed => "✗ فشل",
            _ => string.Empty
        };

        public string StatusColor => Status switch
        {
            TargetStatus.Pending => "#8b949e",
            TargetStatus.InProgress => "#70d6ff",
            TargetStatus.Success => "#06d6a0",
            TargetStatus.Failed => "#ef476f",
            _ => "#ffffff"
        };
    }
}
