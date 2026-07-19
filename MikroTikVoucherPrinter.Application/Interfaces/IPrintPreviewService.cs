using System.Threading;
using System.Threading.Tasks;

namespace MikroTikVoucherPrinter.Application.Interfaces;

public interface IPrintPreviewService
{
    /// <summary>
    /// ط­ظپط¸ ظ…ظ„ظپ ط§ظ„ظ€ PDF ظپظٹ ظ…ط¬ظ„ط¯ ظ…ط¤ظ‚طھ ظˆظپطھط­ظ‡ ط¨ط§ط³طھط®ط¯ط§ظ… ط§ظ„ط¹ط§ط±ط¶ ط§ظ„ط§ظپطھط±ط§ط¶ظٹ ظپظٹ ظ†ط¸ط§ظ… ط§ظ„طھط´ط؛ظٹظ„
    /// </summary>
    Task PreviewPdfAsync(byte[] pdfBytes, string suggestedFileName, CancellationToken cancellationToken = default);
    /// <summary>
    /// ط­ظپط¸ ظ…ظ„ظپ PDF ظپظٹ ط§ظ„ظ…ط³ط§ط± ط§ظ„ظ…ط­ط¯ط¯ ظˆظپطھط­ظ‡
    /// </summary>
    Task SaveAndOpenPdfAsync(byte[] pdfBytes, string filePath, CancellationToken cancellationToken = default);
}
