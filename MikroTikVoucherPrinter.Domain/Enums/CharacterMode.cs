namespace MikroTikVoucherPrinter.Domain.Enums;

/// <summary>
/// نمط توليد الأحرف للكرت - مثل هوائي
/// </summary>
public enum CharacterMode
{
    /// <summary>أرقام فقط (0-9)</summary>
    DigitsOnly = 0,
    
    /// <summary>حروف فقط (A-Z)</summary>
    LettersOnly = 1,
    
    /// <summary>أرقام + حروف مختلطة</summary>
    Mixed = 2,

    /// <summary>حروف صغيرة + أرقام</summary>
    LowercaseMixed = 3
}
