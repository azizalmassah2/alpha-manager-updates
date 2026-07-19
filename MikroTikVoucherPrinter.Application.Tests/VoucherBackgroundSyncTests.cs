using Xunit;
using System;
using System.Reflection;
using MikroTikVoucherPrinter.Infrastructure.Services;

namespace MikroTikVoucherPrinter.Application.Tests;

public class VoucherBackgroundSyncTests
{
    private static MethodInfo GetPrivateMethod(string name)
    {
        var method = typeof(VoucherBackgroundImportManager).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            // Try to find it on base or non-public instance
            method = typeof(VoucherBackgroundImportManager).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
        }
        return method;
    }

    [Theory]
    [InlineData("1d", 86400)]
    [InlineData("1h 30m", 5400)]
    [InlineData("45m", 2700)]
    [InlineData("1w", 604800)]
    [InlineData("0", 0)]
    [InlineData("", 0)]
    public void ParseDurationToSeconds_ShouldParseCorrectly(string duration, long expectedSeconds)
    {
        var method = GetPrivateMethod("ParseDurationToSeconds");
        Assert.NotNull(method);
        var result = (long)method.Invoke(null, new object[] { duration });
        Assert.Equal(expectedSeconds, result);
    }

    [Theory]
    [InlineData("10 GB", 10737418240)]
    [InlineData("500 MB", 524288000)]
    [InlineData("2048 B", 2048)]
    [InlineData("1.5 GB", 1610612736)]
    [InlineData("0", 0)]
    [InlineData("", 0)]
    public void ParseTransferToBytes_ShouldParseCorrectly(string transfer, long expectedBytes)
    {
        var method = GetPrivateMethod("ParseTransferToBytes");
        Assert.NotNull(method);
        var result = (long)method.Invoke(null, new object[] { transfer });
        Assert.Equal(expectedBytes, result);
    }
}
