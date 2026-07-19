using Lux.Platform.Abstractions.Models;
using Xunit;

namespace MikroTikVoucherPrinter.Application.Tests;

public class ProgressCalculationTests
{
    [Fact]
    public void PercentComplete_TotalZero_ReturnsZero()
    {
        var progress = new OperationProgress { TotalDevices = 0, ProcessedDevices = 0 };
        Assert.Equal(0, progress.PercentComplete);
    }

    [Fact]
    public void PercentComplete_CalculatesCorrectly()
    {
        var progress = new OperationProgress { TotalDevices = 10, ProcessedDevices = 5 };
        Assert.Equal(50.0, progress.PercentComplete);
        
        progress.ProcessedDevices = 10;
        Assert.Equal(100.0, progress.PercentComplete);
    }
}
