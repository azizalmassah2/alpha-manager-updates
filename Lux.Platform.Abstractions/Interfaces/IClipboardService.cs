namespace Lux.Platform.Abstractions.Interfaces;

public interface IClipboardService
{
    void SetText(string text);
    string GetText();
}
