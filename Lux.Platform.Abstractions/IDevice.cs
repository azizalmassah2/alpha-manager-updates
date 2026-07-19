namespace Lux.Platform.Abstractions;

/// <summary>
/// العقد الأساسي لأي جهاز شبكة في المنصة
/// </summary>
public interface IDevice
{
    string Id { get; }
    string Name { get; }
    DeviceVendor Vendor { get; }
    string Model { get; }
    string IpAddress { get; }
    string MacAddress { get; }
    string FirmwareVersion { get; }
    DeviceStatus Status { get; }
    DateTime? LastSeen { get; }
}
