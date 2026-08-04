namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// تقرير تقدم العمليات على مستوى Batch — يُرسل عبر IProgress
/// </summary>
public sealed class BatchProgress
{
    public int    Current    { get; init; }
    public int    Total      { get; init; }
    public int    Success    { get; init; }
    public int    Failed     { get; init; }
    public string Phase      { get; init; } = string.Empty;
    public string? Detail    { get; init; }

    public double Percentage =>
        Total > 0 ? Math.Round((double)Current / Total * 100, 1) : 0;

    public bool IsComplete => Current >= Total && Total > 0;

    public static BatchProgress Of(int current, int total, string phase,
        int success = 0, int failed = 0, string? detail = null)
        => new() { Current = current, Total = total, Phase = phase,
                   Success = success, Failed = failed, Detail = detail };
}
