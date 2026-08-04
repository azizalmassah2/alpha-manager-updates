using System.Collections.Generic;
using MikroTikVoucherPrinter.Application;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Infrastructure.Services.CommandProviders;

/// <summary>
/// مزود أوامر RouterOS v7 User Manager.
/// جميع المسارات تبدأ بـ /user-manager/ (بدون /tool/ كما في v6).
/// المعامل الرئيسي للإضافة هو "name" (وليس "username" كما في v6).
/// تعيين الباقة يتم عبر /user-manager/user/profile/add وليس create-and-activate-profile.
/// </summary>
public sealed class RouterOsV7CommandProvider : IMikroTikCommandProvider
{
    public RouterSystemType SystemType => RouterSystemType.UserManagerV7;

    public string DiscoveryPrintPath => "/user-manager/user/print";

    public RouterCommand BuildUserPrintCommand(string username)
        => new()
        {
            Path = "/user-manager/user/print",
            Parameters = new Dictionary<string, string>
            {
                ["name"] = username    // v7 يستخدم "name" كمرشح (وليس "username")
            }
        };

    public RouterCommand BuildUserAddCommand(string username, string? password, string adminUser)
        => new()
        {
            Path = "/user-manager/user/add",
            Parameters = new Dictionary<string, string>
            {
                ["name"]     = username,              // v7: "name" وليس "username"
                ["password"] = password ?? username
                // "owner" في v7 يُستخدم اختيارياً فقط، ليس إلزامياً
            }
        };

    public RouterCommand BuildUserRemoveCommand(string internalId)
        => new()
        {
            Path = "/user-manager/user/remove",
            Parameters = new Dictionary<string, string>
            {
                ["numbers"] = internalId
            }
        };

    public RouterCommand BuildUserBulkRemoveCommand(IEnumerable<string> internalIds)
        => new()
        {
            Path = "/user-manager/user/remove",
            Parameters = new Dictionary<string, string>
            {
                ["numbers"] = string.Join(",", internalIds)
            },
            SupportsTransaction = true
        };

    public RouterCommand BuildAssignProfileCommand(string username, string profileName, string adminUser)
        => new()
        {
            // v7: مسار مختلف كلياً — /user-manager/user/profile/add
            Path = "/user-manager/user/profile/add",
            Parameters = new Dictionary<string, string>
            {
                ["user"]    = username,
                ["profile"] = profileName
                // لا يحتاج "customer" أو "owner" في هذا الأمر
            }
        };
}
