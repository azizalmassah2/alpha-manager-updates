using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;

namespace MikroTikVoucherPrinter.Domain.Interfaces;

/// <summary>
/// ظˆط§ط¬ظ‡ط© ط®ط¯ظ…ط© ط§ظ„ط«ظٹظ…ط§طھ
/// </summary>
public interface IThemeService
{
    /// <summary>ط§ظ„ط«ظٹظ… ط§ظ„ط­ط§ظ„ظٹ</summary>
    AppTheme CurrentTheme { get; }

    /// <summary>طھط؛ظٹظٹط± ط§ظ„ط«ظٹظ…</summary>
    void SetTheme(AppTheme theme);

    /// <summary>طھط¨ط¯ظٹظ„ ط¨ظٹظ† ط§ظ„ط¯ط§ظƒظ† ظˆط§ظ„ظپط§طھط­</summary>
    void ToggleTheme();

    /// <summary>ط­ط¯ط« طھط؛ظٹظٹط± ط§ظ„ط«ظٹظ…</summary>
    event Action<AppTheme>? ThemeChanged;
}
