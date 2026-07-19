using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using OpenWrtProgrammerPro.Models;

namespace OpenWrtProgrammerPro.Services.Interfaces
{
    public interface IUbusClient
    {
        /// <summary>
        /// تسجيل الدخول — يُعيد رمز الجلسة فقط (متوافق مع الكود القديم).
        /// </summary>
        Task<string> LoginAsync(string ip, string username, string password);

        /// <summary>
        /// تسجيل الدخول مع استخراج ACL — يُعيد (sessionToken, DeviceAcls).
        /// يجب استخدام هذا في كل الشيفرة الجديدة بدلاً من LoginAsync.
        /// </summary>
        Task<(string Session, DeviceAcls Acls)> LoginWithAclsAsync(string ip, string username, string password);

        Task<JsonElement> CallAsync(string ip, string session, string ubusObject, string method, object? args);
        Task<Dictionary<string, JsonElement>> ListAsync(string ip, string session, string? pattern = null);
    }
}
