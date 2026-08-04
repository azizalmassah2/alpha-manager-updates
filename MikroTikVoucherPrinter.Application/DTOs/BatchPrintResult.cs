namespace MikroTikVoucherPrinter.Application.DTOs;

/// <summary>
/// نتيجة عملية طباعة / توليد PDF على مستوى Batch
/// </summary>
public sealed class BatchPrintResult
{
    public bool    IsSuccess    { get; init; }
    public string? PdfPath      { get; init; }
    public string? PdfHash      { get; init; }
    public string? ErrorMessage { get; init; }
    public int     CardCount    { get; init; }

    public static BatchPrintResult Success(string pdfPath, int count, string? hash = null)
        => new() { IsSuccess = true, PdfPath = pdfPath, CardCount = count, PdfHash = hash };

    public static BatchPrintResult Failure(string error)
        => new() { IsSuccess = false, ErrorMessage = error };
}
