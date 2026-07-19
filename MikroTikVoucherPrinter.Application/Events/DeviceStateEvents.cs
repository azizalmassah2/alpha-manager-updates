using System;
using Lux.Platform.Abstractions.Models;

namespace MikroTikVoucherPrinter.Application.Events;

public record DeviceStateChangedEvent(DeviceState DeviceState);

public record DeviceOnlineEvent(Guid DeviceId);

public record DeviceOfflineEvent(Guid DeviceId);

public record DeviceHealthChangedEvent(Guid DeviceId, DeviceHealthStatus OldStatus, DeviceHealthStatus NewStatus);

public record AlertGeneratedEvent(Lux.Platform.Abstractions.Models.Monitoring.Alert Alert);

public record FleetOperationStartedEvent(FleetOperation Operation);

public record FleetOperationCompletedEvent(FleetOperation Operation);
