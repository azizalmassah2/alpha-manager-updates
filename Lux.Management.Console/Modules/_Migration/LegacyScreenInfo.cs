namespace Lux.Management.Console.Modules._Migration;

public enum MigrationStatus
{
    NotStarted,
    InProgress,
    Completed
}

public class LegacyScreenInfo
{
    public string ScreenName { get; set; } = string.Empty;
    public MigrationStatus Status { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string OriginalModule { get; set; } = string.Empty;
}
