using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Helpers;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Models;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services
{
    public class UbusClient : IUbusClient
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
        private int _requestId = 1;

        private ILoggerService Logger => ServiceLocator.Instance.Resolve<ILoggerService>();

        public async Task<string> LoginAsync(string ip, string username, string password)
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
            Logger.LogUbusRequest($"session.login ({ip})", requestJson);

            HttpResponseMessage httpResponse;
            string responseJson = string.Empty;
            try
            {
                var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
                httpResponse = await _httpClient.PostAsync(ubusUrl, content);
                responseJson = await httpResponse.Content.ReadAsStringAsync();
                Logger.LogUbusResponse($"session.login ({ip})", (int)httpResponse.StatusCode, responseJson);
            }
            catch (Exception ex)
            {
                Logger.LogError($"فشل الاتصال بـ {ip}: {ex.Message}");
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

        /// <summary>
        /// تسجيل دخول كامل — يُعيد رمز الجلسة + صلاحيات ACL الفعلية من استجابة session.login.
        /// هذا هو الأسلوب الصحيح للكشف الفعلي عن الصلاحيات بدلاً من ubus list.
        /// </summary>
        public async Task<(string Session, DeviceAcls Acls)> LoginWithAclsAsync(string ip, string username, string password)
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
            Logger.LogUbusRequest($"session.login ({ip})", requestJson);

            HttpResponseMessage httpResponse;
            string responseJson = string.Empty;
            try
            {
                var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
                httpResponse = await _httpClient.PostAsync(ubusUrl, content);
                responseJson = await httpResponse.Content.ReadAsStringAsync();
                Logger.LogUbusResponse($"session.login ({ip})", (int)httpResponse.StatusCode, responseJson);
            }
            catch (Exception ex)
            {
                Logger.LogError($"فشل الاتصال بـ {ip}: {ex.Message}");
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

            // ── استخراج رمز الجلسة ─────────────────────────────────────────────
            if (!data.TryGetProperty("ubus_rpc_session", out var sessionProp) || string.IsNullOrEmpty(sessionProp.GetString()))
                throw new Exception("استجابة UBUS غير صالحة: لم يتم العثور على رمز الجلسة ubus_rpc_session");

            var session = sessionProp.GetString()!;

            // ── استخراج صلاحيات ACL من session.login ──────────────────────────
            // البنية الصحيحة: result[1].acls.ubus.uci = ["get", "set", "apply", ...]
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

                    Logger.Log($"[ACL] صلاحيات UCI المُمنوحة فعلياً من ACL الجلسة (acls.ubus.uci):");
                    Logger.Log($"[ACL]   get={acls.CanGet} | set={acls.CanSet} | add={acls.CanAdd} | delete={acls.CanDelete}");
                    Logger.Log($"[ACL]   commit={acls.CanCommit} | apply={acls.CanApply} | rename={acls.CanRename}");
                    Logger.Log($"[ACL]   وضع البرمجة المُكتشف: {acls.ProgrammingMode}");
                }
                else
                {
                    // لم يتوفر المسار acls.ubus.uci → افترض صلاحيات كاملة (توافق مع أجهزة قديمة)
                    acls = DeviceAcls.FullPermissions();
                    Logger.Log("[ACL] لم يتم العثور على acls.ubus.uci في استجابة session.login — سيتم افتراض صلاحيات كاملة.");
                }
            }
            catch
            {
                acls = DeviceAcls.FullPermissions();
                Logger.Log("[ACL] فشل تحليل ACL — سيتم افتراض صلاحيات كاملة كإجراء احترازي.");
            }

            return (session, acls);
        }

        public async Task<JsonElement> CallAsync(string ip, string session, string ubusObject, string method, object? args)
        {
            var ubusUrl = $"http://{ip}/ubus";
            var id = _requestId++;

            // Build params as a JsonArray to avoid C# keyword conflicts
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
            Logger.LogUbusRequest($"{ubusObject}.{method} ({ip})", requestJson);

            HttpResponseMessage httpResponse;
            string responseJson = string.Empty;
            try
            {
                var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
                httpResponse = await _httpClient.PostAsync(ubusUrl, content);
                responseJson = await httpResponse.Content.ReadAsStringAsync();
                Logger.LogUbusResponse($"{ubusObject}.{method} ({ip})", (int)httpResponse.StatusCode, responseJson);
            }
            catch (Exception ex)
            {
                Logger.LogError($"خطأ في طلب UBUS ({ubusObject}.{method}) للجهاز {ip}: {ex.Message}");
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

            // معايير نجاح UBUS:
            // result[0] == 0 لهو نجاح دائماً.
            // الحمولة الإضافية (result[1]) اختيارية — كثير من عمليات UCI تُعيد {"result":[0]} فقط.
            if (root.TryGetProperty("result", out var resultProp) && resultProp.ValueKind == JsonValueKind.Array && resultProp.GetArrayLength() >= 1)
            {
                var ubusStatus = resultProp[0].GetInt32();
                if (ubusStatus != 0)
                {
                    throw new Exception($"فشل طلب UBUS {ubusObject}.{method}. رمز الخطأ: {ubusStatus} ({GetUbusErrorDescription(ubusStatus)})");
                }

                // أعد الحمولة إن وجدت، وإلا أعد عنصر JSON فارغاً
                if (resultProp.GetArrayLength() >= 2)
                {
                    return resultProp[1];
                }

                // نجاح بدون حمولة (result:[0]) — طبيعي لـ uci.set, uci.commit, uci.apply وغيرها
                return JsonDocument.Parse("{}").RootElement.Clone();
            }

            throw new Exception($"استجابة UBUS غير صالحة للطلب {ubusObject}.{method}");
        }

        public async Task<Dictionary<string, JsonElement>> ListAsync(string ip, string session, string? pattern = null)
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
            Logger.LogUbusRequest($"list ({ip})", requestJson);

            HttpResponseMessage httpResponse;
            string responseJson = string.Empty;
            try
            {
                var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
                httpResponse = await _httpClient.PostAsync(ubusUrl, content);
                responseJson = await httpResponse.Content.ReadAsStringAsync();
                Logger.LogUbusResponse($"list ({ip})", (int)httpResponse.StatusCode, responseJson);
            }
            catch (Exception ex)
            {
                Logger.LogError($"خطأ في طلب UBUS list للجهاز {ip}: {ex.Message}");
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
}
