using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Events;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Infrastructure.Security;

/// <summary>
/// 进程内唯一的会话持有者（单例）。
/// </summary>
/// <remarks>
/// <para><b>锁外触发事件</b>：事件订阅方会读会话、更新界面，甚至反向触发登出；
/// 在锁内触发等于把外部代码放进临界区，容易死锁。这里只在锁内换引用，锁外引发事件。</para>
/// <para><b>不做过期检查</b>：过期判定在读取方（<c>PermissionService</c> / 界面）完成，
/// 本类只负责“当前是哪个快照”。否则后台定时器一抖动就会把会话清掉，而用户毫无感知。</para>
/// </remarks>
public sealed class SessionProvider : ISessionProvider
{
    private readonly object _gate = new();

    private UserSession? _current;

    /// <inheritdoc />
    public UserSession? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<SessionChangedEventArgs>? Changed;

    /// <inheritdoc />
    public void Set(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        UserSession? previous;

        lock (_gate)
        {
            previous = _current;
            _current = session;
        }

        Changed?.Invoke(this, new SessionChangedEventArgs(previous, session, "登录"));
    }

    /// <inheritdoc />
    public void Clear(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        UserSession? previous;

        lock (_gate)
        {
            previous = _current;
            _current = null;
        }

        if (previous is null)
        {
            return;
        }

        Changed?.Invoke(this, new SessionChangedEventArgs(previous, null, reason));
    }
}
