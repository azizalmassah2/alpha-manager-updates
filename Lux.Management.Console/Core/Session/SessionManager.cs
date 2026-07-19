using System;

namespace Lux.Management.Console.Core.Session;

/// <summary>
/// تنفيذ ISessionManager — Singleton يُمسك بالجلسة النشطة ويُبلغ عن التغييرات
/// </summary>
public class SessionManager : ISessionManager
{
    private ApplicationSession? _currentSession;

    public ApplicationSession? CurrentSession => _currentSession;
    public bool HasSession => _currentSession != null;

    public event EventHandler<ApplicationSession?>? SessionChanged;

    public void SetSession(ApplicationSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _currentSession = session;
        SessionChanged?.Invoke(this, session);
    }

    public void ClearSession()
    {
        _currentSession = null;
        SessionChanged?.Invoke(this, null);
    }
}
