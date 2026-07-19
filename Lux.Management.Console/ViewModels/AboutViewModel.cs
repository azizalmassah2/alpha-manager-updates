using Lux.Management.Console.Core;
using Lux.Platform.Abstractions.Interfaces;

namespace Lux.Management.Console.ViewModels;

public partial class AboutViewModel : ViewModelBase
{
    public string AppName => "Alpha Manager";
    public string AppVersion
    {
        get
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"v{version.ToString(3)}" : "v2.0.0";
        }
    }
    public string DeveloperName => "AzizAlmassah";
    public string Copyright => $"© {DateTime.Now.Year} Alpha Manager. جميع الحقوق محفوظة.";
    public string Description => "نظام الإدارة الشامل الذكي وشبكات المايكروتيك والمودمات وإدارة الفيلانات والأجهزة المدمجة.";

    public AboutViewModel(
        IPermissionService permissionService,
        IEventBus eventBus)
        : base(permissionService, eventBus)
    {
        Title = "حول البرنامج";
    }
}
