using System;
using Lux.Management.Console.Core.Security.Models;
using Lux.Management.Console.Core.Session;

namespace Lux.Management.Console.Core.Security.Context;

/// <summary>
/// تطبيق ISecurityContext و ISecurityContextUpdater لجمع وإدارة حالة الأمان بشكل موحد دون تكرار الحالة بالذاكرة.
/// </summary>
public class SecurityContext : ISecurityContext, ISecurityContextUpdater
{
    private readonly ISessionManager _sessionManager;

    public SecurityContext(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager;
        _sessionManager.SessionChanged += OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, ApplicationSession? session)
    {
        ContextChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool IsAuthenticated => _sessionManager.HasSession;

    public ApplicationSession? CurrentSession => _sessionManager.CurrentSession;

    public bool IsProMode => _sessionManager.CurrentSession?.IsProMode ?? false;

    public RouterInfo? CurrentRouter => _sessionManager.CurrentSession?.Router;

    public FeatureSet CurrentFeatureSet => IsProMode ? FeatureSet.CreateProSet() : FeatureSet.CreateFreeSet();

    public DateTime? LoginTimestamp => _sessionManager.CurrentSession?.CreatedAt;

    public Guid SessionId => _sessionManager.CurrentSession?.SessionId ?? Guid.Empty;

    public string SessionToken => _sessionManager.CurrentSession?.SessionToken ?? string.Empty;

    public void Initialize(ApplicationSession session)
    {
        _sessionManager.SetSession(session);
    }

    public void Invalidate()
    {
        _sessionManager.CurrentSession?.Invalidate();
        ContextChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ContextChanged;
}
