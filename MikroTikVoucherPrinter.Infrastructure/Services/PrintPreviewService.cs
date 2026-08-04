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

        string targetPath;
        if (Path.IsPathRooted(suggestedFileName))
        {
            targetPath = suggestedFileName;
        }
        else
        {
            string tempDir = Path.GetTempPath();
            targetPath = Path.Combine(tempDir, suggestedFileName);
        }

        // كتابة أو تحديث الملف فوراً بحجمه الكامل الصريح
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllBytesAsync(targetPath, pdfBytes, cancellationToken);

        if (File.Exists(targetPath))
        {
            Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true });
        }
    }

    public async Task SaveAndOpenPdfAsync(byte[] pdfBytes, string filePath, CancellationToken cancellationToken = default)
    {
        if (pdfBytes == null || pdfBytes.Length == 0 || string.IsNullOrWhiteSpace(filePath))
            return;

        await File.WriteAllBytesAsync(filePath, pdfBytes, cancellationToken);

        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
    }
}
