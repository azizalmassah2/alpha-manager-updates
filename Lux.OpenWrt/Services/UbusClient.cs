using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Lux.OpenWrt.Interfaces;
using Lux.OpenWrt.Models;
using Microsoft.Extensions.Logging;

namespace Lux.OpenWrt.Services;

public class UbusClient : IUbusClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UbusClient> _logger;
    private int _requestId = 1;

    public UbusClient(HttpClient httpClient, ILogger<UbusClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> LoginAsync(string ip, string username, string password, CancellationToken cancellationToken = default)
    {
        var ubusUrl = $"http://{ip}/ubus";
        var id = _requestId++;

        var requestPayload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = "call",
            ["params"] = new JsonArray
            {
                "00000000000000000000000000000000",
                "session",
                "login",
                new JsonObject { ["username"] = username, ["password"] = password }
            }
        };

        var requestJson = requestPayload.ToJsonString();
        var safeRequestJson = requestJson.Replace(password, "***");
        _logger.LogInformation("[UBUS REQ] session.login ({Ip}): {Payload}", ip, safeRequestJson);

        HttpResponseMessage httpResponse;
        string responseJson = string.Empty;
        try
        {
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
            httpResponse = await _httpClient.PostAsync(ubusUrl, content, cancellationToken);
            responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            
            var safeResponseJson = responseJson;
            if (responseJson.Contains("ubus_rpc_session"))
            {
                safeResponseJson = "[UBUS Session Response Omitted for Security]";
            }
            _logger.LogInformation("[UBUS RES] session.login ({Ip}) - Status: {StatusCode} - Response: {Response}", ip, (int)httpResponse.StatusCode, safeResponseJson);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("تم إلغاء عملية تسجيل الدخول للجهاز {Ip} (Timeout/Cancel).", ip);
            throw new Exception($"انتهت مهلة الاتصال أو تم الإلغاء ({ip})", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل الاتصال بـ {Ip}: {Message}", ip, ex.Message);
            throw new Exception($"خطأ في الاتصال بالشبكة: {ex.Message}", ex);
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new Exception($"فشل طلب HTTP UBUS بترميز الحالة: {(int)httpResponse.StatusCode} ({httpResponse.ReasonPhrase})");
        }

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errorProp) && errorProp.ValueKind != JsonValueKind.Null)
        {
            var code = errorProp.GetProperty("code").GetInt32();
            var msg = errorProp.GetProperty("message").GetString();
            throw new Exception($"خطأ JSON-RPC من الجهاز (رمز {code}): {msg}");
        }

        if (root.TryGetProperty("result", out var resultProp) && resultProp.ValueKind == JsonValueKind.Array && resultProp.GetArrayLength() == 2)
        {
            var ubusStatus = resultProp[0].GetInt32();
            if (ubusStatus != 0)
            {
                throw new Exception($"فشل تسجيل الدخول. رمز خطأ UBUS: {ubusStatus} ({GetUbusErrorDescription(ubusStatus)})");
            }

            var data = resultProp[1];
            if (data.TryGetProperty("ubus_rpc_session", out var sessionProp))
            {
                var session = sessionProp.GetString();
                if (!string.IsNullOrEmpty(session))
                {
                    return session;
                }
            }
        }

        throw new Exception("استجابة UBUS غير صالحة: لم يتم العثور على رمز الجلسة ubus_rpc_session");
    }

    public async Task<(string Session, DeviceAcls Acls)> LoginWithAclsAsync(string ip, string username, string password, CancellationToken cancellationToken = default)
    {
        var ubusUrl = $"http://{ip}/ubus";
        var id = _requestId++;

        var requestPayload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = "call",
            ["params"] = new JsonArray
            {
                "00000000000000000000000000000000",
                "session",
                "login",
                new JsonObject { ["username"] = username, ["password"] = password }
            }
        };

        var requestJson = requestPayload.ToJsonString();
        var safeRequestJson = requestJson.Replace(password, "***");
        _logger.LogInformation("[UBUS REQ] session.login ({Ip}): {Payload}", ip, safeRequestJson);

        HttpResponseMessage httpResponse;
        string responseJson = string.Empty;
        try
        {
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
            httpResponse = await _httpClient.PostAsync(ubusUrl, content, cancellationToken);
            responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("[UBUS RES] session.login ({Ip}) - Status: {StatusCode} - Session Response Omitted", ip, (int)httpResponse.StatusCode);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("تم إلغاء عملية تسجيل الدخول للجهاز {Ip} (Timeout/Cancel).", ip);
            throw new Exception($"انتهت مهلة الاتصال أو تم الإلغاء ({ip})", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل الاتصال بـ {Ip}: {Message}", ip, ex.Message);
            throw new Exception($"خطأ في الاتصال بالشبكة: {ex.Message}", ex);
        }

        if (!httpResponse.IsSuccessStatusCode)
            throw new Exception($"فشل طلب HTTP UBUS بترميز الحالة: {(int)httpResponse.StatusCode} ({httpResponse.ReasonPhrase})");

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errProp) && errProp.ValueKind != JsonValueKind.Null)
        {
            var code = errProp.GetProperty("code").GetInt32();
            var msg  = errProp.GetProperty("message").GetString();
            throw new Exception($"خطأ JSON-RPC من الجهاز (رمز {code}): {msg}");
        }

        if (!root.TryGetProperty("result", out var resultProp) ||
            resultProp.ValueKind != JsonValueKind.Array ||
            resultProp.GetArrayLength() < 2)
            throw new Exception("استجابة UBUS غير صالحة: لم يتم العثور على رمز الجلسة ubus_rpc_session");

        var status = resultProp[0].GetInt32();
        if (status != 0)
            throw new Exception($"فشل تسجيل الدخول. رمز خطأ UBUS: {status} ({GetUbusErrorDescription(status)})");

        var data = resultProp[1];

        if (!data.TryGetProperty("ubus_rpc_session", out var sessionProp) || string.IsNullOrEmpty(sessionProp.GetString()))
            throw new Exception("استجابة UBUS غير صالحة: لم يتم العثور على رمز الجلسة ubus_rpc_session");

        var session = sessionProp.GetString()!;

        DeviceAcls acls;
        try
        {
            if (data.TryGetProperty("acls", out var aclsProp) &&
                aclsProp.TryGetProperty("ubus", out var ubusProp) &&
                ubusProp.TryGetProperty("uci", out var uciAcl) &&
                uciAcl.ValueKind == JsonValueKind.Array)
            {
                var grantedMethods = uciAcl.EnumerateArray()
                    .Select(el => el.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrEmpty(s));

                acls = DeviceAcls.FromUciAclArray(grantedMethods);

                _logger.LogInformation("[ACL] صلاحيات UCI المُمنوحة فعلياً من ACL الجلسة (acls.ubus.uci): get={CanGet} | set={CanSet} | add={CanAdd} | delete={CanDelete} | commit={CanCommit} | apply={CanApply} | rename={CanRename}",
                    acls.CanGet, acls.CanSet, acls.CanAdd, acls.CanDelete, acls.CanCommit, acls.CanApply, acls.CanRename);
                _logger.LogInformation("[ACL] وضع البرمجة المُكتشف: {Mode}", acls.ProgrammingMode);
            }
            else
            {
                acls = DeviceAcls.FullPermissions();
                _logger.LogWarning("[ACL] لم يتم العثور على acls.ubus.uci في استجابة session.login — سيتم افتراض صلاحيات كاملة.");
            }
        }
        catch
        {
            acls = DeviceAcls.FullPermissions();
            _logger.LogWarning("[ACL] فشل تحليل ACL — سيتم افتراض صلاحيات كاملة كإجراء احترازي.");
        }

        return (session, acls);
    }

    public async Task<JsonElement> CallAsync(string ip, string session, string ubusObject, string method, object? args, CancellationToken cancellationToken = default)
    {
        var ubusUrl = $"http://{ip}/ubus";
        var id = _requestId++;

        var argsNode = args != null
            ? JsonSerializer.SerializeToNode(args)
            : new JsonObject();

        var requestPayload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = "call",
            ["params"] = new JsonArray
            {
                session,
                ubusObject,
                method,
                argsNode
            }
        };

        var requestJson = requestPayload.ToJsonString();
        var safeRequestJson = requestJson.Replace(session, "***SESSION***");
        _logger.LogInformation("[UBUS REQ] {Object}.{Method} ({Ip}): {Payload}", ubusObject, method, ip, safeRequestJson);

        HttpResponseMessage httpResponse;
        string responseJson = string.Empty;
        try
        {
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
            httpResponse = await _httpClient.PostAsync(ubusUrl, content, cancellationToken);
            responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            var safeResponseJson = responseJson.Replace(session, "***SESSION***");
            _logger.LogInformation("[UBUS RES] {Object}.{Method} ({Ip}) - Status: {StatusCode} - Response: {Response}", ubusObject, method, ip, (int)httpResponse.StatusCode, safeResponseJson);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("تم إلغاء عملية UBUS ({Object}.{Method}) للجهاز {Ip} (Timeout/Cancel).", ubusObject, method, ip);
            throw new Exception($"انتهت مهلة الاتصال أو تم الإلغاء ({ip})", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في طلب UBUS ({Object}.{Method}) للجهاز {Ip}: {Message}", ubusObject, method, ip, ex.Message);
            throw new Exception($"خطأ في التواصل مع الجهاز {ip}: {ex.Message}", ex);
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new Exception($"خطأ HTTP {(int)httpResponse.StatusCode} عند الاتصال بـ {ubusObject}.{method}");
        }

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement.Clone();

        if (root.TryGetProperty("error", out var errorProp) && errorProp.ValueKind != JsonValueKind.Null)
        {
            var code = errorProp.GetProperty("code").GetInt32();
            var msg = errorProp.GetProperty("message").GetString();
            throw new Exception($"خطأ UBUS JSON-RPC (رمز {code}): {msg}");
        }

        if (root.TryGetProperty("result", out var resultProp) && resultProp.ValueKind == JsonValueKind.Array && resultProp.GetArrayLength() >= 1)
        {
            var ubusStatus = resultProp[0].GetInt32();
            if (ubusStatus != 0)
            {
                throw new Exception($"فشل طلب UBUS {ubusObject}.{method}. رمز الخطأ: {ubusStatus} ({GetUbusErrorDescription(ubusStatus)})");
            }

            if (resultProp.GetArrayLength() >= 2)
            {
                return resultProp[1];
            }

            return JsonDocument.Parse("{}").RootElement.Clone();
        }

        throw new Exception($"استجابة UBUS غير صالحة للطلب {ubusObject}.{method}");
    }

    public async Task<Dictionary<string, JsonElement>> ListAsync(string ip, string session, string? pattern = null, CancellationToken cancellationToken = default)
    {
        var ubusUrl = $"http://{ip}/ubus";
        var id = _requestId++;

        JsonArray paramsArray;
        if (pattern != null)
            paramsArray = new JsonArray { session, pattern };
        else
            paramsArray = new JsonArray { session, "*" };

        var requestPayload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = "list",
            ["params"] = paramsArray
        };

        var requestJson = requestPayload.ToJsonString();
        var safeRequestJson = requestJson.Replace(session, "***SESSION***");
        _logger.LogInformation("[UBUS REQ] list ({Ip}): {Payload}", ip, safeRequestJson);

        HttpResponseMessage httpResponse;
        string responseJson = string.Empty;
        try
        {
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
            httpResponse = await _httpClient.PostAsync(ubusUrl, content, cancellationToken);
            responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("[UBUS RES] list ({Ip}) - Status: {StatusCode}", ip, (int)httpResponse.StatusCode);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("تم إلغاء عملية UBUS list للجهاز {Ip} (Timeout/Cancel).", ip);
            throw new Exception($"انتهت مهلة الاتصال أو تم الإلغاء ({ip})", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في طلب UBUS list للجهاز {Ip}: {Message}", ip, ex.Message);
            throw new Exception($"خطأ في الاتصال بالشبكة: {ex.Message}", ex);
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new Exception($"خطأ HTTP {(int)httpResponse.StatusCode} عند استدعاء قائمة كائنات UBUS");
        }

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errorProp) && errorProp.ValueKind != JsonValueKind.Null)
        {
            var code = errorProp.GetProperty("code").GetInt32();
            var msg = errorProp.GetProperty("message").GetString();
            throw new Exception($"خطأ UBUS JSON-RPC (رمز {code}): {msg}");
        }

        if (root.TryGetProperty("result", out var resultProp) && resultProp.ValueKind == JsonValueKind.Object)
        {
            var dict = new Dictionary<string, JsonElement>();
            foreach (var prop in resultProp.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.Clone();
            }
            return dict;
        }

        throw new Exception("استجابة UBUS غير صالحة لطلب list");
    }

    private string GetUbusErrorDescription(int code)
    {
        return code switch
        {
            0 => "نجاح",
            1 => "أمر غير صالح (Invalid Command)",
            2 => "معامل غير صالح (Invalid Argument)",
            3 => "المجلد أو الكائن غير موجود (Method Not Found)",
            4 => "غير موجود (Not Found)",
            5 => "لا توجد بيانات (No Data)",
            6 => "تم رفض الإذن / غير مصرح به (Permission Denied)",
            7 => "انتهت مهلة الطلب (Timeout)",
            8 => "لم يتم العثور على التطبيق (Not Supported)",
            9 => "خطأ في النظام الداخلي (System Error)",
            _ => "خطأ غير معروف"
        };
    }
}
