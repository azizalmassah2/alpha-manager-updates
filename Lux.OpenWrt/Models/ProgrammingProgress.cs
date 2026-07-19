namespace Lux.OpenWrt.Models;

public class ProgrammingProgress
{
    public int CurrentStep { get; set; }
    public int CompletedSteps { get; set; }
    public int TotalSteps { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Percentage { get; set; }
}
