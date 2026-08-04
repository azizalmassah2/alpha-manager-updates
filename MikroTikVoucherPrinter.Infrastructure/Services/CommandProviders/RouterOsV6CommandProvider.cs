using System.Collections.Generic;
using MikroTikVoucherPrinter.Application;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Infrastructure.Services.CommandProviders;

/// <summary>
/// مزود أوامر RouterOS v6 User Manager.
/// جميع المسارات تبدأ بـ /tool/user-manager/
/// المعامل الرئيسي للإضافة هو "customer" (وليس "owner" كما في v7).
/// </summary>
public sealed class RouterOsV6CommandProvider : IMikroTikCommandProvider
{
    public RouterSystemType SystemType => RouterSystemType.UserManagerV6;

    public string DiscoveryPrintPath => "/tool/user-manager/user/print";

    public RouterCommand BuildUserPrintCommand(string username)
        => new()
        {
            Path = "/tool/user-manager/user/print",
            Parameters = new Dictionary<string, string>
            {
                ["username"] = username    // v6 يستخدم "username" كمرشح
            }
        };

    public RouterCommand BuildUserAddCommand(string username, string? password, string adminUser)
        => new()
        {
            Path = "/tool/user-manager/user/add",
            Parameters = new Dictionary<string, string>
            {
                ["customer"] = adminUser,              // v6: "customer" وليس "owner"
                ["username"] = username,
                ["password"] = password ?? username
            }
        };

    public RouterCommand BuildUserRemoveCommand(string internalId)
        => new()
        {
            Path = "/tool/user-manager/user/remove",
            Parameters = new Dictionary<string, string>
            {
                ["numbers"] = internalId
            }
        };

    public RouterCommand BuildUserBulkRemoveCommand(IEnumerable<string> internalIds)
        => new()
        {
            Path = "/tool/user-manager/user/remove",
            Parameters = new Dictionary<string, string>
            {
                ["numbers"] = string.Join(",", internalIds)
            },
            SupportsTransaction = true
        };

    public RouterCommand BuildAssignProfileCommand(string username, string profileName, string adminUser)
        => new()
        {
            Path = "/tool/user-manager/user/create-and-activate-profile",
            Parameters = new Dictionary<string, string>
            {
                ["customer"] = adminUser,   // v6: "customer"
                ["user"]     = username,
                ["profile"]  = profileName
            }
        };
}
