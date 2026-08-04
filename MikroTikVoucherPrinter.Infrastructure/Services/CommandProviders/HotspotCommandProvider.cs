using System.Collections.Generic;
using MikroTikVoucherPrinter.Application;
using MikroTikVoucherPrinter.Application.Interfaces;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Infrastructure.Services.CommandProviders;

/// <summary>
/// مزود أوامر Hotspot (/ip/hotspot/user/).
/// يُستخدم عندما لا يكون User Manager متاحاً على الراوتر.
/// الفلتر الرئيسي هو "name" (وليس "username").
/// </summary>
public sealed class HotspotCommandProvider : IMikroTikCommandProvider
{
    public RouterSystemType SystemType => RouterSystemType.Hotspot;

    public string DiscoveryPrintPath => "/ip/hotspot/user/print";

    public RouterCommand BuildUserPrintCommand(string username)
        => new()
        {
            Path = "/ip/hotspot/user/print",
            Parameters = new Dictionary<string, string>
            {
                ["name"] = username    // Hotspot يستخدم "name" وليس "username"
            }
        };

    public RouterCommand BuildUserAddCommand(string username, string? password, string adminUser)
        => new()
        {
            Path = "/ip/hotspot/user/add",
            Parameters = new Dictionary<string, string>
            {
                ["name"]     = username,
                ["password"] = password ?? username,
                ["server"]   = "all"
            }
        };

    public RouterCommand BuildUserRemoveCommand(string internalId)
        => new()
        {
            Path = "/ip/hotspot/user/remove",
            Parameters = new Dictionary<string, string>
            {
                ["numbers"] = internalId
            }
        };

    public RouterCommand BuildUserBulkRemoveCommand(IEnumerable<string> internalIds)
        => new()
        {
            Path = "/ip/hotspot/user/remove",
            Parameters = new Dictionary<string, string>
            {
                ["numbers"] = string.Join(",", internalIds)
            },
            SupportsTransaction = true
        };

    public RouterCommand BuildAssignProfileCommand(string username, string profileName, string adminUser)
        => new()
        {
            Path = "/ip/hotspot/user/set",
            Parameters = new Dictionary<string, string>
            {
                ["numbers"] = username,
                ["profile"] = profileName
            }
        };
}
