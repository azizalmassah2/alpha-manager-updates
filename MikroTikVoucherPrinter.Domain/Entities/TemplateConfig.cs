using MikroTikVoucherPrinter.Domain.Common;
using Lux.Platform.Abstractions.Common;
using MikroTikVoucherPrinter.Domain.Enums;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MikroTikVoucherPrinter.Domain.Entities;

/// <summary>
/// إعدادات قالب الطباعة (Grid-based rendering)
/// يعتمد على وضع صورة خلفية، وتحديد إحداثيات النصوص والـ QR (X, Y) لتطابق التصميم بدقة
/// </summary>
public class TemplateConfig : BaseEntity, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    private bool _isDefault;
    public bool IsDefault
    {
        get => _isDefault;
        set { if (_isDefault != value) { _isDefault = value; OnPropertyChanged(); } }
    }

    /// <summary>تصنيف الورق / نوع العرض (A4، حراري، شبكة مخصصة).</summary>
    public TemplateType Kind { get; set; } = TemplateType.A4;

    /// <summary>قالب نظامي محفوظ في القائمة (لا يُعرض كـ "مستخدم" فقط).</summary>
    public bool IsSystemTemplate { get; set; }

    /// <summary>
    /// عند التعبئة يُستخدم محرك الطباعة الكلاسيكي (قوالب النظام القديمة) بدل شبكة CustomGrid.
    /// أمثلة: HawaeGridDefault، A4GridDefault، ThermalDefault.
    /// </summary>
    public string? LegacyRendererKey { get; set; }

    /// <summary>عرض المنطقة القابلة للطباعة للحراري (مم) — اختياري.</summary>
    public double? ThermalPrintableWidthMm { get; set; }

    // ─── أبعاد وإعدادات الشبكة (بالمليمتر mm) ───
    private int _columns = 3;
    public int Columns
    {
        get => _columns;
        set { if (_columns != value) { _columns = value; OnPropertyChanged(); } }
    }

    private int _rows = 7;
    public int Rows
    {
        get => _rows;
        set { if (_rows != value) { _rows = value; OnPropertyChanged(); } }
    }

    public float CardWidth { get; set; } = 70f;
    public float CardHeight { get; set; } = 40f;

    private float _marginX = 0f;
    public float MarginX
    {
        get => _marginX;
        set { if (_marginX != value) { _marginX = value; OnPropertyChanged(); } }
    }

    private float _marginY = 0f;
    public float MarginY
    {
        get => _marginY;
        set { if (_marginY != value) { _marginY = value; OnPropertyChanged(); } }
    }

    // ─── الصور والربط ───
    private string? _backgroundImagePath;
    public string? BackgroundImagePath
    {
        get => _backgroundImagePath;
        set { if (_backgroundImagePath != value) { _backgroundImagePath = value; OnPropertyChanged(); } }
    }

    private string? _logoImagePath;
    public string? LogoImagePath
    {
        get => _logoImagePath;
        set { if (_logoImagePath != value) { _logoImagePath = value; OnPropertyChanged(); } }
    }

    public string? LinkedProfileName { get; set; }

    // ─── إطار الكرت ───
    private float _frameSize = 0f;
    public float FrameSize
    {
        get => _frameSize;
        set { if (_frameSize != value) { _frameSize = value; OnPropertyChanged(); } }
    }

    private string _frameColorHex = "#000000";
    public string FrameColorHex
    {
        get => _frameColorHex;
        set { if (_frameColorHex != value) { _frameColorHex = value; OnPropertyChanged(); } }
    }

    // ─── التنسيق العام للخط ───
    private string _fontFamily = "Arial";
    public string FontFamily
    {
        get => _fontFamily;
        set { if (_fontFamily != value) { _fontFamily = value; OnPropertyChanged(); } }
    }

    private float _fontSize = 10f;
    public float FontSize
    {
        get => _fontSize;
        set { if (_fontSize != value) { _fontSize = value; OnPropertyChanged(); } }
    }

    private string _fontColorHex = "#000000";
    public string FontColorHex
    {
        get => _fontColorHex;
        set { if (_fontColorHex != value) { _fontColorHex = value; OnPropertyChanged(); } }
    }

    private bool _isBold = false;
    public bool IsBold
    {
        get => _isBold;
        set { if (_isBold != value) { _isBold = value; OnPropertyChanged(); } }
    }

    private bool _isItalic = false;
    public bool IsItalic
    {
        get => _isItalic;
        set { if (_isItalic != value) { _isItalic = value; OnPropertyChanged(); } }
    }

    // ─── إحداثيات العناصر (بالمليمتر mm) ───

    // اسم المستخدم (رمز الدخول)
    private bool _showUsername = true;
    public bool ShowUsername
    {
        get => _showUsername;
        set { if (_showUsername != value) { _showUsername = value; OnPropertyChanged(); } }
    }

    private float _usernameX = 35f;
    public float UsernameX
    {
        get => _usernameX;
        set { if (_usernameX != value) { _usernameX = value; OnPropertyChanged(); } }
    }

    private float _usernameY = 25f;
    public float UsernameY
    {
        get => _usernameY;
        set { if (_usernameY != value) { _usernameY = value; OnPropertyChanged(); } }
    }

    // كلمة السر
    private bool _showPassword = true;
    public bool ShowPassword
    {
        get => _showPassword;
        set { if (_showPassword != value) { _showPassword = value; OnPropertyChanged(); } }
    }

    private float _passwordX = 35f;
    public float PasswordX
    {
        get => _passwordX;
        set { if (_passwordX != value) { _passwordX = value; OnPropertyChanged(); } }
    }

    private float _passwordY = 15f;
    public float PasswordY
    {
        get => _passwordY;
        set { if (_passwordY != value) { _passwordY = value; OnPropertyChanged(); } }
    }

    // السعر
    private bool _showPrice = true;
    public bool ShowPrice
    {
        get => _showPrice;
        set { if (_showPrice != value) { _showPrice = value; OnPropertyChanged(); } }
    }

    private float _priceX = 10f;
    public float PriceX
    {
        get => _priceX;
        set { if (_priceX != value) { _priceX = value; OnPropertyChanged(); } }
    }

    private float _priceY = 20f;
    public float PriceY
    {
        get => _priceY;
        set { if (_priceY != value) { _priceY = value; OnPropertyChanged(); } }
    }

    // مدة الصلاحية
    private bool _showValidity = false;
    public bool ShowValidity
    {
        get => _showValidity;
        set { if (_showValidity != value) { _showValidity = value; OnPropertyChanged(); } }
    }

    private float _validityX = 10f;
    public float ValidityX
    {
        get => _validityX;
        set { if (_validityX != value) { _validityX = value; OnPropertyChanged(); } }
    }

    private float _validityY = 30f;
    public float ValidityY
    {
        get => _validityY;
        set { if (_validityY != value) { _validityY = value; OnPropertyChanged(); } }
    }

    // الوقت / الحجم (Quota)
    private bool _showTime = false;
    public bool ShowTime
    {
        get => _showTime;
        set { if (_showTime != value) { _showTime = value; OnPropertyChanged(); } }
    }

    private float _timeX = 10f;
    public float TimeX
    {
        get => _timeX;
        set { if (_timeX != value) { _timeX = value; OnPropertyChanged(); } }
    }

    private float _timeY = 10f;
    public float TimeY
    {
        get => _timeY;
        set { if (_timeY != value) { _timeY = value; OnPropertyChanged(); } }
    }

    // الرقم التسلسلي
    private bool _showSerialNumber = false;
    public bool ShowSerialNumber
    {
        get => _showSerialNumber;
        set { if (_showSerialNumber != value) { _showSerialNumber = value; OnPropertyChanged(); } }
    }

    private float _serialNumberX = 5f;
    public float SerialNumberX
    {
        get => _serialNumberX;
        set { if (_serialNumberX != value) { _serialNumberX = value; OnPropertyChanged(); } }
    }

    private float _serialNumberY = 5f;
    public float SerialNumberY
    {
        get => _serialNumberY;
        set { if (_serialNumberY != value) { _serialNumberY = value; OnPropertyChanged(); } }
    }

    // تاريخ الطباعة
    private bool _showPrintDate = false;
    public bool ShowPrintDate
    {
        get => _showPrintDate;
        set { if (_showPrintDate != value) { _showPrintDate = value; OnPropertyChanged(); } }
    }

    private float _printDateX = 50f;
    public float PrintDateX
    {
        get => _printDateX;
        set { if (_printDateX != value) { _printDateX = value; OnPropertyChanged(); } }
    }

    private float _printDateY = 5f;
    public float PrintDateY
    {
        get => _printDateY;
        set { if (_printDateY != value) { _printDateY = value; OnPropertyChanged(); } }
    }

    // رمز الـ QR
    private bool _showQr = true;
    public bool ShowQr
    {
        get => _showQr;
        set { if (_showQr != value) { _showQr = value; OnPropertyChanged(); } }
    }

    private float _qrX = 50f;
    public float QrX
    {
        get => _qrX;
        set { if (_qrX != value) { _qrX = value; OnPropertyChanged(); } }
    }

    private float _qrY = 15f;
    public float QrY
    {
        get => _qrY;
        set { if (_qrY != value) { _qrY = value; OnPropertyChanged(); } }
    }

    private float _qrSize = 20f;
    public float QrSize
    {
        get => _qrSize;
        set { if (_qrSize != value) { _qrSize = value; OnPropertyChanged(); } }
    }

    // الباركود (Barcode)
    private bool _showBarcode = false;
    public bool ShowBarcode
    {
        get => _showBarcode;
        set { if (_showBarcode != value) { _showBarcode = value; OnPropertyChanged(); } }
    }

    private float _barcodeX = 50f;
    public float BarcodeX
    {
        get => _barcodeX;
        set { if (_barcodeX != value) { _barcodeX = value; OnPropertyChanged(); } }
    }

    private float _barcodeY = 30f;
    public float BarcodeY
    {
        get => _barcodeY;
        set { if (_barcodeY != value) { _barcodeY = value; OnPropertyChanged(); } }
    }

    private float _barcodeSize = 15f;
    public float BarcodeSize
    {
        get => _barcodeSize;
        set { if (_barcodeSize != value) { _barcodeSize = value; OnPropertyChanged(); } }
    }

    public Guid RouterId { get; set; }
}
