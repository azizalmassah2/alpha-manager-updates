using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.Management.Console.Core.Security.Context;
using Lux.Management.Console.Core.Security.Models;
using Lux.Management.Console.Core.Security.Audit;
using Lux.Management.Console.Core.Security.Configuration;
using Lux.Management.Console.Core.Session;
using Lux.Management.Console.Core.Security.Diagnostics;
using Lux.Management.Console.Core.Security.Runtime.Checks;

namespace Lux.Management.Console.Core.Security.Runtime;

/// <summary>
/// تطبيق IRuntimeMonitor للمراقبة الأمنية متعددة المستويات.
/// يعتمد على ISecurityEventPublisher بدلاً من الاستدعاء المباشر لـ ISecurityAuditService،
/// مما يُقلل الترابط ويفتح الباب لمستهلكين متعددين دون تعديل هذه الفئة.
/// </summary>
public class RuntimeMonitor : IRuntimeMonitor
{
    private readonly IEnumerable<ISecurityCheck> _checks;
    private readonly IAntiTamperService _antiTamperService;
    private readonly ISecurityContext _securityContext;
    private readonly ISecurityContextUpdater _securityContextUpdater;
    private readonly ISecurityEventPublisher _eventPublisher;
    
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();
    private bool _disposed = false;

    public RuntimeMonitor(
        IEnumerable<ISecurityCheck> checks,
        IAntiTamperService antiTamperService,
        ISecurityContext securityContext,
        ISecurityContextUpdater securityContextUpdater,
        ISecurityEventPublisher eventPublisher)
    {
        _checks = checks;
        _antiTamperService = antiTamperService;
        _securityContext = securityContext;
        _securityContextUpdater = securityContextUpdater;
        _eventPublisher = eventPublisher;
    }

    public Task StartAsync(ApplicationSession session, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_cts != null) return Task.CompletedTask; // قيد التشغيل بالفعل

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // تشغيل المستوى 1 فوراً عند بدء الجلسة
            ExecuteLevel1Checks();

            // نشر حدث التشغيل عبر الناشر (مفكوك الارتباط عن خدمة التدقيق)
            _eventPublisher.Publish(new RuntimeAuditEvent(AuditSeverity.Info, "Started", "Runtime Monitor Service started successfully."));

            Task.Run(async () =>
            {
                // إخفاء خيط المراقبة عن مصحح الأخطاء
                _antiTamperService.HideCurrentThread();

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // المستوى 2: فحوصات دورية خفيفة الوزن
                        ExecuteLevel2Checks();
                    }
                    catch (Exception ex)
                    {
                        TriggerGracefulSecurityShutdown("RUNTIME_MONITOR_PERIODIC", $"Error during Level 2 checks: {ex.Message}");
                        return;
                    }

                    // فترة انتظار عشوائية بين 30 و 90 ثانية
                    var intervalSeconds = Random.Shared.Next(
                        SecurityConfiguration.Level2MinIntervalSeconds, 
                        SecurityConfiguration.Level2MaxIntervalSeconds);
                    
                    try
                    {
                        await Task.Delay(intervalSeconds * 1000, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, token);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_cts == null) return Task.CompletedTask;

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;

            _eventPublisher.Publish(new RuntimeAuditEvent(AuditSeverity.Info, "Stopped", "Runtime Monitor Service stopped."));
        }

        return Task.CompletedTask;
    }

    public void ExecuteLevel1Checks()
    {
        RunChecks(1);
    }

    private void ExecuteLevel2Checks()
    {
        RunChecks(2);
    }

    public void ExecuteLevel3Checks()
    {
        RunChecks(3);
    }

    private void RunChecks(int level)
    {
        foreach (var check in _checks.Where(c => c.CanRun(level)))
        {
            try
            {
                check.Execute();
            }
            catch (Exception ex)
            {
                TriggerGracefulSecurityShutdown($"LEVEL{level}_{check.CheckName.ToUpper()}", ex.Message);
                break;
            }
        }
    }

     private void TriggerGracefulSecurityShutdown(string source, string reason)
     {
         // 1. التثبت والتأكيد الأمني لمنع القراءات الخاطئة
         var confirmDebugger = _antiTamperService.DetectDebugger();
         var confirmIntegrity = _antiTamperService.VerifyLoadedAssemblies();
         
         if (!confirmDebugger && confirmIntegrity && source.Contains("DEBUGGER"))
         {
             return; // تجاهل القراءة الخاطئة للمصحح
         }
 
         // 2. نشر حدث التلاعب عبر الناشر (مفكوك الارتباط)
         _eventPublisher.Publish(new TamperAuditEvent(source, reason, $"Security shutdown triggered. Reason: {reason}")
         {
             CorrelationId = _securityContext.SessionId.ToString()
         });
 
         // 3. إبطال الجلسة وتخفيض الصلاحيات فوراً في الذاكرة
         _securityContextUpdater.Invalidate();
 
         // 4. محاولة الإغلاق المتدرج للنوافذ والاتصالات والـ Application
         try
         {
             var app = System.Windows.Application.Current;
             if (app != null)
             {
                 app.Dispatcher.Invoke(() =>
                 {
                     try
                     {
                         // إغلاق النوافذ النشطة بأمان
                         foreach (System.Windows.Window window in app.Windows)
                         {
                             try { window.Close(); } catch { }
                         }
                     }
                     catch { }
 
                     try
                     {
                         app.Shutdown();
                     }
                     catch { }
                 });
             }
         }
         catch
         {
             // تجاهل خطأ الـ Dispatcher لمتابعة القتل الفوري
         }
 
         // 5. فرض القتل الإجباري في حال تعذر إكمال الإغلاق المتدرج
         Thread.Sleep(SecurityConfiguration.GracefulShutdownTimeoutMs);
         _antiTamperService.TriggerEmergencyShutdown();
     }
 
     public void Dispose()
     {
         lock (_lock)
         {
             if (_disposed) return;
             if (_cts != null)
             {
                 try
                 {
                     _cts.Cancel();
                 }
                 catch { }
                 _cts.Dispose();
                 _cts = null;
             }
             _disposed = true;
         }
     }
}
