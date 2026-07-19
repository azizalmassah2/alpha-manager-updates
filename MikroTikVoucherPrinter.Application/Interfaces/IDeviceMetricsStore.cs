using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Models.Monitoring;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IDeviceMetricsStore
{
    Task StoreMetricAsync(DeviceMetric metric);
    Task<IEnumerable<DeviceMetric>> GetMetricsAsync(Guid deviceId, DateTime since);
    Task<IEnumerable<DeviceMetric>> GetLatestMetricsAsync(Guid deviceId, int count);
}
