namespace Lux.Management.Console.Modules.MikroTik.UserManager;

/// <summary>
/// حالة مساحة العمل المشتركة — تُستخدم في UserManager ViewModels.
/// </summary>
public enum WorkspaceState
{
    Loading,
    Refreshing,
    Loaded,
    Empty,
    Error
}
