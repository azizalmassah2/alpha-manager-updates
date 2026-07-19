using System;

namespace Lux.Platform.Abstractions.Models;

public class RetryPolicy
{
    public int MaxAttempts { get; set; } = 3;
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(5);
}
