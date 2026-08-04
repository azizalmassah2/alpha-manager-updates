using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lux.Management.Console.Modules.MikroTik.Hotspot.Models;

public partial class AdImageItemDto : ObservableObject
{
    public int Index { get; set; }
    public string TargetFileName => $"{Index}.jpg";

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _localFilePath;

    [ObservableProperty]
    private ImageSource? _previewImage;

    [ObservableProperty]
    private bool _hasImage;
}
