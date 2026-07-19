using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MikroTikVoucherPrinter.Application.Interfaces;

namespace MikroTikVoucherPrinter.Infrastructure.Services;

public class PrintPreviewService : IPrintPreviewService
{
    public async Task PreviewPdfAsync(byte[] pdfBytes, string suggestedFileName, CancellationToken cancellationToken = default)
    {
        if (pdfBytes == null || pdfBytes.Length == 0)
            return;

        string tempDir = Path.GetTempPath();
        string tempFile = Path.Combine(tempDir, suggestedFileName);

        // Ensure unique filename
        if (File.Exists(tempFile))
        {
            tempFile = Path.Combine(tempDir, $"{Path.GetFileNameWithoutExtension(suggestedFileName)}_{Guid.NewGuid().ToString("N").Substring(0, 4)}{Path.GetExtension(suggestedFileName)}");
        }

        await File.WriteAllBytesAsync(tempFile, pdfBytes, cancellationToken);

        Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
    }

    public async Task SaveAndOpenPdfAsync(byte[] pdfBytes, string filePath, CancellationToken cancellationToken = default)
    {
        if (pdfBytes == null || pdfBytes.Length == 0 || string.IsNullOrWhiteSpace(filePath))
            return;

        await File.WriteAllBytesAsync(filePath, pdfBytes, cancellationToken);

        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
    }
}
