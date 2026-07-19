namespace Lux.Platform.Abstractions;

/// <summary>
/// تعداد الشركات المصنّعة لأجهزة الشبكة المدعومة في المنصة
/// </summary>
public enum DeviceVendor
{
    Unknown  = 0,
    MikroTik = 1,
    OpenWrt  = 2,
    Ubiquiti = 3,
}

/// <summary>
/// حالة الجهاز الحالية
/// </summary>
public enum DeviceStatus
{
    Unknown    = 0,
    Online     = 1,
    Offline    = 2,
    Unreachable= 3,
    Error      = 4,
    Provisioning = 5,
}
