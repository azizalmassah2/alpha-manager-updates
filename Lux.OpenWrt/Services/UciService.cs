using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;

namespace Lux.OpenWrt.Services;

public class UciService : IUciService
{
    private readonly IUbusClient _ubus;

    public UciService(IUbusClient ubus)
    {
        _ubus = ubus;
    }

    public async Task<JsonElement> GetAsync(string ip, string session, string config, string? section = null, string? option = null, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object>
        {
            { "config", config }
        };

        if (section != null) args["section"] = section;
        if (option != null) args["option"] = option;

        return await _ubus.CallAsync(ip, session, "uci", "get", args, cancellationToken);
    }

    public async Task SetAsync(string ip, string session, string config, string section, Dictionary<string, object> values, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object>
        {
            { "config", config },
            { "section", section },
            { "values", values }
        };

        await _ubus.CallAsync(ip, session, "uci", "set", args, cancellationToken);
    }

    public async Task SetOptionAsync(string ip, string session, string config, string section, string option, object value, CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, object> { { option, value } };
        await SetAsync(ip, session, config, section, values, cancellationToken);
    }

    public async Task<string> AddSectionAsync(string ip, string session, string config, string type, string? name = null, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object>
        {
            { "config", config },
            { "type", type }
        };

        if (name != null) args["name"] = name;

        var result = await _ubus.CallAsync(ip, session, "uci", "add", args, cancellationToken);
        if (result.TryGetProperty("section", out var sectionProp))
        {
            return sectionProp.GetString() ?? string.Empty;
        }

        throw new Exception($"فشل إضافة قسم جديد من نوع {type} في ملف الإعدادات {config}. لم يتم إرجاع اسم القسم.");
    }

    public async Task DeleteAsync(string ip, string session, string config, string? section = null, string? option = null, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object>
        {
            { "config", config }
        };

        if (section != null) args["section"] = section;
        if (option != null) args["option"] = option;

        await _ubus.CallAsync(ip, session, "uci", "delete", args, cancellationToken);
    }

    public async Task CommitAsync(string ip, string session, string config, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object>
        {
            { "config", config }
        };

        await _ubus.CallAsync(ip, session, "uci", "commit", args, cancellationToken);
    }

    public async Task ApplyAsync(string ip, string session, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object>
        {
            { "rollback", false } // apply changes persistently
        };

        await _ubus.CallAsync(ip, session, "uci", "apply", args, cancellationToken);
    }

    public async Task<Dictionary<string, object>> GetConfigDictAsync(string ip, string session, string config, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await GetAsync(ip, session, config, null, null, cancellationToken);
            var dict = new Dictionary<string, object>();

            // The whole config response from uci.get is usually {"values": { "section_name": { ".type": "type", "option": "value" } } }
            if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty("values", out var valuesProp) && valuesProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var sectionProp in valuesProp.EnumerateObject())
                {
                    if (sectionProp.Value.ValueKind == JsonValueKind.Object)
                    {
                        var sectionDict = new Dictionary<string, object>();
                        foreach (var optionProp in sectionProp.Value.EnumerateObject())
                        {
                            // Store option value depending on its type
                            object val = optionProp.Value.ValueKind switch
                            {
                                JsonValueKind.String => optionProp.Value.GetString() ?? string.Empty,
                                JsonValueKind.Number => optionProp.Value.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                JsonValueKind.Array => DeserializeJsonArray(optionProp.Value),
                                _ => optionProp.Value.ToString()
                            };
                            sectionDict[optionProp.Name] = val;
                        }
                        dict[sectionProp.Name] = sectionDict;
                    }
                }
            }
            return dict;
        }
        catch (Exception)
        {
            // Return empty dictionary if file doesn't exist or is empty
            return new Dictionary<string, object>();
        }
    }

    private List<string> DeserializeJsonArray(JsonElement element)
    {
        var list = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            var str = item.GetString();
            if (str != null) list.Add(str);
        }
        return list;
    }

    public async Task RevertAsync(string ip, string session, string config, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object>
        {
            { "config", config }
        };
        await _ubus.CallAsync(ip, session, "uci", "revert", args, cancellationToken);
    }
}
