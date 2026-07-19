using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Application.Events;

namespace MikroTikVoucherPrinter.Application.Services;

public class FleetOperationService : IFleetOperationService
{
    private readonly IOperationHistoryRepository _historyRepository;
    private readonly IProvisioningOrchestrator _provisioningOrchestrator;
    private readonly IUnifiedBackupService _backupService;
    private readonly IUnifiedFirmwareService _firmwareService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<FleetOperationService> _logger;
    private readonly RetryPolicy _retryPolicy;

    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeCancellations = new();

    public FleetOperationService(
        IProvisioningOrchestrator provisioningOrchestrator,
        IUnifiedBackupService backupService,
        IUnifiedFirmwareService firmwareService,
        IOperationHistoryRepository historyRepository,
        IEventBus eventBus,
        ILogger<FleetOperationService> logger)
    {
        _provisioningOrchestrator = provisioningOrchestrator;
        _backupService = backupService;
        _firmwareService = firmwareService;
        _historyRepository = historyRepository;
        _eventBus = eventBus;
        _logger = logger;
        _retryPolicy = new RetryPolicy(); // Default policy
    }

    public Task<IReadOnlyCollection<FleetOperation>> GetOperationsAsync(CancellationToken cancellationToken = default)
    {
        return _historyRepository.GetAllAsync(cancellationToken);
    }

    public async Task<Guid> StartProvisioningAsync(
        IReadOnlyCollection<IDevice> devices,
        ProvisioningTemplate template,
        CancellationToken cancellationToken = default)
    {
        var operation = CreateOperation("Provisioning Operation", FleetOperationType.Provisioning, devices.Count);
        await _historyRepository.SaveAsync(operation, cancellationToken);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeCancellations[operation.Id] = cts;

        _ = Task.Run(() => ExecuteOperationAsync(
            operation,
            devices,
            device => _provisioningOrchestrator.ProvisionDeviceAsync(device, template, null, cts.Token),
            cts.Token
        ), CancellationToken.None);

        return operation.Id;
    }

    public async Task<Guid> StartBackupAsync(
        IReadOnlyCollection<IDevice> devices,
        CancellationToken cancellationToken = default)
    {
        var operation = CreateOperation("Backup Operation", FleetOperationType.Backup, devices.Count);
        await _historyRepository.SaveAsync(operation, cancellationToken);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeCancellations[operation.Id] = cts;

        _ = Task.Run(() => ExecuteOperationAsync(
            operation,
            devices,
            async device => {
                var result = await _backupService.CreateBackupAsync(device, BackupType.Configuration, cts.Token);
                return new DeviceOperationResult
                {
                    DeviceId = device.Id,
                    DeviceName = device.Name,
                    Success = result.IsSuccess,
                    Error = result.ErrorMessage
                };
            },
            cts.Token
        ), CancellationToken.None);

        return operation.Id;
    }

    public async Task<Guid> StartRestoreAsync(
        IReadOnlyCollection<IDevice> devices,
        DeviceBackup backup,
        CancellationToken cancellationToken = default)
    {
        var operation = CreateOperation("Restore Operation", FleetOperationType.Restore, devices.Count);
        await _historyRepository.SaveAsync(operation, cancellationToken);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeCancellations[operation.Id] = cts;

        _ = Task.Run(() => ExecuteOperationAsync(
            operation,
            devices,
            async device => {
                var result = await _backupService.RestoreBackupAsync(device, backup, cts.Token);
                return new DeviceOperationResult
                {
                    DeviceId = device.Id,
                    DeviceName = device.Name,
                    Success = result.IsSuccess,
                    Error = result.ErrorMessage
                };
            },
            cts.Token
        ), CancellationToken.None);

        return operation.Id;
    }

    public async Task<FleetOperation?> GetOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        return await _historyRepository.GetByIdAsync(operationId, cancellationToken);
    }

    public async Task<OperationProgress> GetProgressAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var operation = await _historyRepository.GetByIdAsync(operationId, cancellationToken);
        return operation?.Progress ?? new OperationProgress();
    }

    public async Task CancelAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        if (_activeCancellations.TryGetValue(operationId, out var cts))
        {
            _logger.LogInformation("Cancellation requested for operation {OperationId}", operationId);
            await cts.CancelAsync();
        }
    }

    private FleetOperation CreateOperation(string name, FleetOperationType type, int totalDevices)
    {
        return new FleetOperation
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            Status = FleetOperationStatus.Pending,
            StartedAt = DateTime.UtcNow,
            Progress = new OperationProgress { TotalDevices = totalDevices }
        };
    }

    public async Task<Guid> StartFirmwareUpgradeAsync(IEnumerable<IDevice> devices, FirmwareImage image)
    {
        var devicesList = devices as IReadOnlyCollection<IDevice> ?? devices.ToList();
        var operation = CreateOperation($"Firmware Upgrade: {image.Version}", FleetOperationType.FirmwareUpgrade, devicesList.Count);
        await _historyRepository.SaveAsync(operation);

        var cts = new CancellationTokenSource();
        _activeCancellations.TryAdd(operation.Id, cts);

        _ = Task.Run(() => ExecuteOperationAsync(
            operation,
            devicesList,
            async device => {
                var result = await _firmwareService.UpgradeFirmwareAsync(device, image, cts.Token);
                return new FirmwareDeviceOperationResult
                {
                    DeviceId = device.Id,
                    DeviceName = device.Name,
                    Success = result.IsSuccess && result.Value!.Success,
                    Error = result.IsSuccess ? result.Value!.Error : result.ErrorMessage,
                    PreviousVersion = result.Value?.PreviousVersion,
                    NewVersion = result.Value?.NewVersion
                };
            },
            cts.Token
        ));

        return operation.Id;
    }

    private async Task ExecuteOperationAsync<TResult>(
        FleetOperation operation,
        IReadOnlyCollection<IDevice> devices,
        Func<IDevice, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Operation {OperationId} Started", operation.Id);
        operation.Status = FleetOperationStatus.Running;
        await _historyRepository.UpdateAsync(operation, CancellationToken.None);
        _eventBus.Publish(new FleetOperationStartedEvent(operation));

        foreach (var device in devices)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Operation {OperationId} was cancelled.", operation.Id);
                break;
            }

            _logger.LogInformation("Device {DeviceName} Started", device.Name);
            var stopwatch = Stopwatch.StartNew();
            DeviceOperationResult? finalResult = null;

            for (int attempt = 1; attempt <= _retryPolicy.MaxAttempts; attempt++)
            {
                try
                {
                    var resultObj = await action(device);
                    
                    bool isSuccess = false;
                    string? error = null;

                    if (resultObj is Lux.Platform.Abstractions.Common.Result<DeviceProvisioningResult> provResult)
                    {
                        isSuccess = provResult.IsSuccess && provResult.Value!.IsSuccess;
                        error = provResult.IsSuccess ? provResult.Value!.ErrorMessage : provResult.ErrorMessage;
                    }
                    else if (resultObj is DeviceOperationResult devRes)
                    {
                        isSuccess = devRes.Success;
                        error = devRes.Error;
                    }

                    if (isSuccess)
                    {
                        stopwatch.Stop();
                        finalResult = new DeviceOperationResult
                        {
                            DeviceId = device.Id,
                            DeviceName = device.Name,
                            Success = true,
                            Duration = stopwatch.Elapsed
                        };
                        _logger.LogInformation("Device {DeviceName} Completed Successfully", device.Name);
                        break; // Success, break retry loop
                    }
                    else
                    {
                        error ??= "Unknown error";
                        _logger.LogWarning("Attempt {Attempt} for Device {DeviceName} failed: {Error}", attempt, device.Name, error);
                        if (attempt == _retryPolicy.MaxAttempts)
                        {
                            stopwatch.Stop();
                            finalResult = new DeviceOperationResult
                            {
                                DeviceId = device.Id,
                                DeviceName = device.Name,
                                Success = false,
                                Error = error,
                                Duration = stopwatch.Elapsed
                            };
                            _logger.LogError("Device {DeviceName} Failed after {MaxAttempts} attempts", device.Name, _retryPolicy.MaxAttempts);
                        }
                        else
                        {
                            _logger.LogInformation("Retry Triggered for {DeviceName} in {DelayMs}ms", device.Name, _retryPolicy.Delay.TotalMilliseconds);
                            await Task.Delay(_retryPolicy.Delay, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Attempt {Attempt} for Device {DeviceName} threw an exception", attempt, device.Name);
                    if (attempt == _retryPolicy.MaxAttempts || cancellationToken.IsCancellationRequested)
                    {
                        stopwatch.Stop();
                        finalResult = new DeviceOperationResult
                        {
                            DeviceId = device.Id,
                            DeviceName = device.Name,
                            Success = false,
                            Error = ex.Message,
                            Duration = stopwatch.Elapsed
                        };
                        _logger.LogError("Device {DeviceName} Failed after {MaxAttempts} attempts due to exception", device.Name, _retryPolicy.MaxAttempts);
                    }
                    else
                    {
                        _logger.LogInformation("Retry Triggered for {DeviceName} in {DelayMs}ms after exception", device.Name, _retryPolicy.Delay.TotalMilliseconds);
                        await Task.Delay(_retryPolicy.Delay, cancellationToken);
                    }
                }
            }

            if (finalResult != null)
            {
                operation.DeviceResults.Add(finalResult);
                operation.Progress.ProcessedDevices++;
                if (finalResult.Success)
                    operation.Progress.SuccessfulDevices++;
                else
                    operation.Progress.FailedDevices++;

                await _historyRepository.UpdateAsync(operation, CancellationToken.None);
            }
        }

        operation.FinishedAt = DateTime.UtcNow;
        if (cancellationToken.IsCancellationRequested)
        {
            operation.Status = FleetOperationStatus.Cancelled;
        }
        else
        {
            operation.Status = operation.Progress.FailedDevices == 0 ? FleetOperationStatus.Completed : FleetOperationStatus.Failed;
            _logger.LogInformation("Operation {OperationId} Completed with Status {Status}", operation.Id, operation.Status);
        }

        await _historyRepository.UpdateAsync(operation, CancellationToken.None);
        _activeCancellations.TryRemove(operation.Id, out _);
        _eventBus.Publish(new FleetOperationCompletedEvent(operation));
    }
}
