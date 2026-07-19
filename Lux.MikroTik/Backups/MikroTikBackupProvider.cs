using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lux.MikroTik.Models;
using Lux.MikroTik.Providers;
using Lux.Platform.Abstractions.Common;
using Lux.Platform.Abstractions.Interfaces;
using Lux.Platform.Abstractions.Models;
using Lux.Platform.Abstractions;
using MikroTikVoucherPrinter.Domain.Entities;
using MikroTikVoucherPrinter.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Lux.MikroTik.Backups;

public class MikroTikBackupProvider : IDeviceBackupProvider
{
    private readonly IRouterOsProvider _provider;
    private readonly IRouterOsTextProvider _textProvider;
    private readonly ILogger<MikroTikBackupProvider> _logger;
    private readonly string _baseBackupsPath;

    public MikroTikBackupProvider(IRouterOsProvider provider, IRouterOsTextProvider textProvider, ILogger<MikroTikBackupProvider> logger)
    {
        _provider = provider;
        _textProvider = textProvider;
        _logger = logger;
        _baseBackupsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups", "MikroTik");
    }

    public bool CanHandle(IDevice device)
    {
        return device.GetType().Name == "NetworkDevice" && 
               ((dynamic)device).Vendor.ToString() == "MikroTik";
    }

    public async Task<Result<DeviceBackup>> CreateBackupAsync(IDevice device, BackupType backupType = BackupType.Configuration, CancellationToken cancellationToken = default)
    {
        var netDevice = device as NetworkDevice;
        if (netDevice == null) return Result<DeviceBackup>.Failure("Invalid device type", ErrorType.Unexpected);

        _logger.LogInformation("Creating MikroTik backup for {Host}...", device.IpAddress);

        try
        {
            var deviceDir = Path.Combine(_baseBackupsPath, device.IpAddress);
            if (!Directory.Exists(deviceDir))
                Directory.CreateDirectory(deviceDir);

            if (!_provider.IsConnected)
                return Result<DeviceBackup>.Failure("Provider is not connected", ErrorType.ExternalService);

            var command = new MikroTikCommand { Command = "/export compact" };
            var textResult = await _textProvider.ExecuteTextAsync(command);

            if (!textResult.IsSuccess)
                return Result<DeviceBackup>.Failure(textResult.ErrorMessage, textResult.ErrorType);

            var exportData = textResult.Value;
            
            var timestamp = DateTime.UtcNow;
            var filename = $"{timestamp:yyyyMMdd_HHmmss}.rsc";
            var filePath = Path.Combine(deviceDir, filename);

            await File.WriteAllTextAsync(filePath, exportData, System.Text.Encoding.UTF8, cancellationToken);
            
            var checksum = CalculateChecksum(filePath);
            var fileInfo = new FileInfo(filePath);

            var backup = new DeviceBackup
            {
                Id = Guid.NewGuid().ToString("N"),
                DeviceId = device.IpAddress,
                DeviceName = netDevice.Name ?? "Unknown MikroTik",
                Vendor = DeviceVendor.MikroTik,
                CreatedAt = timestamp,
                BackupType = BackupType.Configuration,
                FilePath = filePath,
                FileName = filename,
                Checksum = checksum,
                SizeBytes = fileInfo.Length,
                Metadata = "{}"
            };

            var metaFilePath = filePath + ".meta.json";
            var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metaFilePath, json, cancellationToken);

            _logger.LogInformation("Backup created successfully for {Host}", device.IpAddress);
            return Result<DeviceBackup>.Success(backup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MikroTik backup for {Host}", device.IpAddress);
            return Result<DeviceBackup>.Failure("Failed to create backup", ErrorType.ExternalService, ex);
        }
    }

    public Task<Result> RestoreBackupAsync(IDevice device, DeviceBackup backup, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MikroTik restore is simulated for {Host} in this phase.", device.IpAddress);
        return Task.FromResult(Result.Success());
    }

    public async Task<Result<IReadOnlyList<DeviceBackup>>> GetBackupsAsync(IDevice device, CancellationToken cancellationToken = default)
    {
        try
        {
            var deviceDir = Path.Combine(_baseBackupsPath, device.IpAddress);
            if (!Directory.Exists(deviceDir))
                return Result<IReadOnlyList<DeviceBackup>>.Success(new List<DeviceBackup>());

            var backups = new List<DeviceBackup>();
            foreach (var metaFile in Directory.GetFiles(deviceDir, "*.meta.json"))
            {
                var json = await File.ReadAllTextAsync(metaFile, cancellationToken);
                var backup = JsonSerializer.Deserialize<DeviceBackup>(json);
                if (backup != null)
                {
                    backups.Add(backup);
                }
            }

            backups.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
            return Result<IReadOnlyList<DeviceBackup>>.Success(backups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve backups for {Host}", device.IpAddress);
            return Result<IReadOnlyList<DeviceBackup>>.Failure("Failed to retrieve backups", ErrorType.ExternalService, ex);
        }
    }

    private string CalculateChecksum(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
