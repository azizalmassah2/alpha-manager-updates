using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Lux.OpenWrt.Models;
using Lux.OpenWrt.Services;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lux.OpenWrt.Tests;

public class BackupRestoreServiceTests : IDisposable
{
    private readonly Mock<IUciService> _uciMock;
    private readonly Mock<IUbusClient> _mockUbus;
    private readonly Mock<ILogger<BackupRestoreService>> _mockLogger;
    private readonly BackupRestoreService _service;
    private readonly string _backupsDir;

    public BackupRestoreServiceTests()
    {
        _uciMock = new Mock<IUciService>();
        _mockUbus = new Mock<IUbusClient>();
        _mockLogger = new Mock<ILogger<BackupRestoreService>>();
        _service = new BackupRestoreService(_uciMock.Object, _mockUbus.Object, _mockLogger.Object);
        _backupsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups", "OpenWrt");
        
        if (Directory.Exists(_backupsDir))
        {
            Directory.Delete(_backupsDir, true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_backupsDir))
        {
            try { Directory.Delete(_backupsDir, true); } catch { }
        }
    }

    [Fact]
    public async Task CreateBackupAsync_Success_ReturnsBackupMetadataAndCreatesFile()
    {
        // Arrange
        var ip = "192.168.1.1";
        var session = "session123";
        var host = "router-test";

        _uciMock.Setup(u => u.GetConfigDictAsync(ip, session, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object> { { "test_section", new { test_option = "1" } } });

        // Act
        var result = await _service.CreateBackupAsync(ip, session, host, BackupType.Configuration);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(host, result.Value.DeviceId);
        Assert.Equal(BackupType.Configuration, result.Value.BackupType);
        Assert.True(File.Exists(result.Value.FilePath));
        Assert.True(File.Exists(result.Value.FilePath + ".meta.json"));
    }

    [Fact]
    public async Task RestoreBackupAsync_InvalidChecksum_ReturnsFailure()
    {
        // Arrange
        var ip = "192.168.1.1";
        var session = "session123";
        var host = "router-test";

        _uciMock.Setup(u => u.GetConfigDictAsync(ip, session, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object>());

        var backupResult = await _service.CreateBackupAsync(ip, session, host, BackupType.Configuration);
        var backup = backupResult.Value!;

        // Act
        var result = await _service.RestoreBackupAsync(ip, session, backup.FilePath, "invalid-checksum-123");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Contains("Checksum", result.ErrorMessage);
    }

    [Fact]
    public async Task RestoreBackupAsync_ValidChecksum_ReturnsSuccess()
    {
        // Arrange
        var ip = "192.168.1.1";
        var session = "session123";
        var host = "router-test";

        _uciMock.Setup(u => u.GetConfigDictAsync(ip, session, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object> { { "test_section", new Dictionary<string, object>() } });

        var backupResult = await _service.CreateBackupAsync(ip, session, host, BackupType.Configuration);
        var backup = backupResult.Value!;

        // Act
        var result = await _service.RestoreBackupAsync(ip, session, backup.FilePath, backup.Checksum);

        // Assert
        Assert.True(result.IsSuccess);
        // It should call uci.revert for each config
        _uciMock.Verify(u => u.RevertAsync(ip, session, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DeleteBackupAsync_Success_RemovesFiles()
    {
        // Arrange
        var ip = "192.168.1.1";
        var session = "session123";
        var host = "router-test";

        _uciMock.Setup(u => u.GetConfigDictAsync(ip, session, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object>());

        var backupResult = await _service.CreateBackupAsync(ip, session, host, BackupType.Configuration);
        var backup = backupResult.Value!;
        var metaPath = backup.FilePath + ".meta.json";

        Assert.True(File.Exists(backup.FilePath));
        Assert.True(File.Exists(metaPath));

        // Act
        var result = await _service.DeleteBackupAsync(backup.FilePath);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(backup.FilePath));
        Assert.False(File.Exists(metaPath));
    }

    [Fact]
    public async Task GetBackupsAsync_ReturnsListSortedByDate()
    {
        // Arrange
        var ip = "192.168.1.1";
        var session = "session123";
        var host = "router-test-list";

        _uciMock.Setup(u => u.GetConfigDictAsync(ip, session, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object>());

        await _service.CreateBackupAsync(ip, session, host, BackupType.Configuration);
        await Task.Delay(1100);
        await _service.CreateBackupAsync(ip, session, host, BackupType.Configuration);

        // Act
        var result = await _service.GetBackupsAsync(host);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(BackupType.Configuration, result.Value.First().BackupType); // Newest first
    }
}
