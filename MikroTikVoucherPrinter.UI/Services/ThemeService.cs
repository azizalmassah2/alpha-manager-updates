using System.Windows;
using Microsoft.Extensions.Logging;
using MikroTikVoucherPrinter.Domain.Enums;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Interfaces;
using MaterialDesignThemes.Wpf;

namespace MikroTikVoucherPrinter.UI.Services;

/// <summary>
/// ط®ط¯ظ…ط© ط§ظ„ط«ظٹظ…ط§طھ - طھط¨ط¯ظٹظ„ ط¨ظٹظ† ط§ظ„ط¯ط§ظƒظ† ظˆط§ظ„ظپط§طھط­ ظ…ط¹ ط­ظپط¸ ط§ظ„ط§ط®طھظٹط§ط±
/// طھط¹طھظ…ط¯ ط¨ط§ظ„ظƒط§ظ…ظ„ ط¹ظ„ظ‰ PaletteHelper ط§ظ„ط®ط§طµ ط¨ظ…ظƒطھط¨ط© MaterialDesignInXAML
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ThemeService> _logger;
    private const string ThemeSettingKey = "App.Theme";

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public event Action<AppTheme>? ThemeChanged;

    public ThemeService(ISettingsService settingsService, ILogger<ThemeService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// طھط­ظ…ظٹظ„ ط§ظ„ط«ظٹظ… ط§ظ„ظ…ط­ظپظˆط¸ ظ…ظ† ط§ظ„ط¥ط¹ط¯ط§ط¯ط§طھ
    /// </summary>
    public void LoadSavedTheme()
    {
        var savedTheme = _settingsService.Get("App.Theme", "Dark");
        var theme = savedTheme == "Light" ? AppTheme.Light : AppTheme.Dark;
        SetTheme(theme);
    }

    public void SetTheme(AppTheme theme)
    {
        try
        {
            var paletteHelper = new PaletteHelper();
            ITheme themeData = paletteHelper.GetTheme();

            IBaseTheme baseTheme = theme == AppTheme.Dark ? new MaterialDesignDarkTheme() : new MaterialDesignLightTheme();
            themeData.SetBaseTheme(baseTheme);

            paletteHelper.SetTheme(themeData);

            CurrentTheme = theme;

            // ط­ظپط¸ ط§ظ„ط§ط®طھظٹط§ط±
            _settingsService.Set(ThemeSettingKey, theme.ToString());
            _ = _settingsService.SaveAsync();

            ThemeChanged?.Invoke(theme);

            _logger.LogInformation("طھظ… طھط؛ظٹظٹط± ط§ظ„ط«ظٹظ… ط¨ظ†ط¬ط§ط­ ط¨ط§ط³طھط®ط¯ط§ظ… PaletteHelper ط¥ظ„ظ‰: {Theme}", theme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ظپط´ظ„ طھط؛ظٹظٹط± ط§ظ„ط«ظٹظ… ط¥ظ„ظ‰ {Theme}", theme);
        }
    }

    public void ToggleTheme()
    {
        var newTheme = CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        SetTheme(newTheme);
    }
}
