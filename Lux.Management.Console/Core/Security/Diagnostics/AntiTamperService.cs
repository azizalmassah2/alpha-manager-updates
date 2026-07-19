using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Lux.Management.Console.Core.Security.Diagnostics;

/// <summary>
/// تطبيق IAntiTamperService لكشف أدوات الهندسة العكسية وحقن الذاكرة والتلاعب بالـ DLLs.
/// </summary>
public class AntiTamperService : IAntiTamperService
{
    private static bool _isAssemblyLoadSubscribed = false;
    private static readonly object _lock = new();
    private static bool _tamperFlag = false;

    // ─── P/Invoke Win32 API ──────────────────────────────────────────────────
    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool IsDebuggerPresent();

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool pbDebuggerPresent);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle, int processInformationClass,
        ref int processInformation, int processInformationLength, ref int returnLength);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetInformationThread(
        IntPtr threadHandle, int threadInformationClass,
        IntPtr threadInformation, int threadInformationLength);

    private const int ProcessDebugPort = 7;
    private const int ThreadHideFromDebugger = 0x11;

    public AntiTamperService()
    {
        InitializeAssemblyLoadWatcher();
    }

    private void InitializeAssemblyLoadWatcher()
    {
        lock (_lock)
        {
            if (!_isAssemblyLoadSubscribed)
            {
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;
                _isAssemblyLoadSubscribed = true;
            }
        }
    }

    private void OnAssemblyLoaded(object? sender, AssemblyLoadEventArgs args)
    {
        // التحقق من التجميع الذي تم تحميله ديناميكياً أثناء تشغيل البرنامج
        var assemblyName = args.LoadedAssembly.GetName().Name;
        if (string.IsNullOrEmpty(assemblyName)) return;

        // مراجعة التجميعات المشبوهة (مثل أدوات التفكيك المعروفة أو تجميعات مجهولة المصدر)
        var suspiciousKeywords = new[] { "de4dot", "harmony", "dnlib", "dile", "reflexil" };
        foreach (var keyword in suspiciousKeywords)
        {
            if (assemblyName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                lock (_lock)
                {
                    _tamperFlag = true; // تعيين علامة التلاعب الأمني
                }
            }
        }
    }

    public bool DetectDebugger()
    {
        // 1. Managed Debugger Check
        if (Debugger.IsAttached) return true;

        // 2. Native Debugger Check (IsDebuggerPresent)
        if (IsDebuggerPresent()) return true;

        // 3. Remote Debugger Check
        bool isRemoteDebuggerPresent = false;
        CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref isRemoteDebuggerPresent);
        if (isRemoteDebuggerPresent) return true;

        // 4. NtQueryInformationProcess (ProcessDebugPort check)
        int debugPort = 0;
        int returnLength = 0;
        int status = NtQueryInformationProcess(
            Process.GetCurrentProcess().Handle,
            ProcessDebugPort,
            ref debugPort,
            sizeof(int),
            ref returnLength);

        if (status == 0 && debugPort != 0)
        {
            return true;
        }

        // 5. Timing Attack Check
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 500_000; i++)
        {
            var temp = Math.Sqrt(i);
        }
        stopwatch.Stop();
        
        if (stopwatch.ElapsedMilliseconds > 150)
        {
            return true;
        }

        // 6. التحقق من علامة التلاعب الخاصة بالتجميعات المحملة ديناميكياً
        lock (_lock)
        {
            if (_tamperFlag) return true;
        }

        return false;
    }

    public bool VerifyLoadedAssemblies()
    {
        try
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var criticalAssemblies = new[]
            {
                "Lux.Management.Console",
                "Lux.MikroTik",
                "Lux.OpenWrt",
                "MikroTikVoucherPrinter.Application",
                "MikroTikVoucherPrinter.Infrastructure"
            };

            foreach (var assembly in loadedAssemblies)
            {
                var name = assembly.GetName().Name;
                if (name == null) continue;

                if (criticalAssemblies.Contains(name))
                {
                    // كشف التحميل غير المستقر من الذاكرة (Reflective Loading/DLL Injection)
                    if (string.IsNullOrEmpty(assembly.Location))
                    {
                        return false; 
                    }

                    if (!File.Exists(assembly.Location))
                    {
                        return false;
                    }
                }
            }

#if !DEBUG
            if (!VerifyAssemblyHashes())
            {
                return false;
            }
#endif

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool VerifyAssemblyHashes()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string integrityPath = Path.Combine(baseDir, "integrity.dat");
            if (!File.Exists(integrityPath))
            {
                return false;
            }

            byte[] cipherBytes = File.ReadAllBytes(integrityPath);

            byte[] keyBytes = new byte[32];
            byte[] ivBytes = new byte[16];
            byte[] rawKeySource = System.Text.Encoding.UTF8.GetBytes("AlphaManagerSecureAssemblyIntegrityKey2026");
            Array.Copy(rawKeySource, keyBytes, Math.Min(rawKeySource.Length, 32));
            Array.Copy(rawKeySource, ivBytes, Math.Min(rawKeySource.Length, 16));

            byte[] plainBytes;
            using (var aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                using (var msInput = new MemoryStream(cipherBytes))
                using (var msOutput = new MemoryStream())
                {
                    using (var cs = new System.Security.Cryptography.CryptoStream(msInput, aes.CreateDecryptor(), System.Security.Cryptography.CryptoStreamMode.Read))
                    {
                        cs.CopyTo(msOutput);
                    }
                    plainBytes = msOutput.ToArray();
                }
            }

            string decryptedText = System.Text.Encoding.UTF8.GetString(plainBytes).Trim();
            if (string.IsNullOrEmpty(decryptedText))
            {
                return false;
            }

            var lines = decryptedText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(':');
                if (parts.Length != 2) return false;

                string filename = parts[0].Trim();
                string expectedHash = parts[1].Trim().ToLower();

                string filePath = Path.Combine(baseDir, filename);
                if (!File.Exists(filePath))
                {
                    return false;
                }

                byte[] fileBytes = File.ReadAllBytes(filePath);
                byte[] computedHashBytes;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    computedHashBytes = sha.ComputeHash(fileBytes);
                }

                var sb = new System.Text.StringBuilder();
                foreach (byte b in computedHashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                string actualHash = sb.ToString();

                if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void HideCurrentThread()
    {
        try
        {
            NtSetInformationThread(
                new IntPtr(-2) /* Current Thread */,
                ThreadHideFromDebugger,
                IntPtr.Zero,
                0);
        }
        catch
        {
            // تجاهل الفشل لضمان التوافق مع الأنظمة الأخرى
        }
    }

    public void TriggerEmergencyShutdown()
    {
        try
        {
            Process.GetCurrentProcess().Kill();
        }
        catch
        {
            Environment.Exit(0xFF);
        }
    }
}
