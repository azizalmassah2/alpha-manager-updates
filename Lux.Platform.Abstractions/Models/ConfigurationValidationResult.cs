using System.Collections.Generic;

namespace Lux.Platform.Abstractions.Models;

public class ConfigurationValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public static ConfigurationValidationResult Success() => new();
    
    public static ConfigurationValidationResult Failure(string error)
    {
        var result = new ConfigurationValidationResult();
        result.Errors.Add(error);
        return result;
    }
}
