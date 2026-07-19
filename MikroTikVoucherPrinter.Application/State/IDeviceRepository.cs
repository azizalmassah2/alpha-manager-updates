using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Models;

namespace MikroTikVoucherPrinter.Application.State;

public interface IDeviceRepository
{
    Task<IEnumerable<DeviceState>> GetAllAsync();
    Task<DeviceState?> GetByIdAsync(Guid deviceId);
    Task UpdateAsync(DeviceState deviceState);
    Task RemoveAsync(Guid deviceId);
}
