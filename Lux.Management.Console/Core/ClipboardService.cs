using System.Windows;
using Lux.Platform.Abstractions.Interfaces;

namespace Lux.Management.Console.Core;

public class ClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // Ignore clipboard lock errors
        }
    }

    public string GetText()
    {
        try
        {
            return Clipboard.GetText();
        }
        catch
        {
            return string.Empty;
        }
    }
}
