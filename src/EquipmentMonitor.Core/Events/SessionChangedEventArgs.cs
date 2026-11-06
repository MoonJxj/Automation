using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Events;

/// <summary>
/// 会话变更事件：登录、切换用户、登出都会触发。
/// </summary>
/// <remarks>
/// <para>事件在引发者所在线程触发（当前是 UI 线程，将来远端令牌刷新可能在后台线程），
/// UI 订阅方必须自行 <c>BeginInvoke</c> 切回界面线程。</para>
/// <para>两个会话都是不可变快照，订阅方可以安全持有。</para>
/// </remarks>
public sealed class SessionChangedEventArgs : EventArgs
{
    /// <summary>创建事件参数。</summary>
    /// <param name="previous">变更前的会话；未登录时为 <c>null</c>。</param>
    /// <param name="current">变更后的会话；登出时为 <c>null</c>。</param>
    /// <param name="reason">可读原因（“登录”“切换用户”“会话超时”等）。</param>
    public SessionChangedEventArgs(UserSession? previous, UserSession? current, string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        Previous = previous;
        Current = current;
        Reason = reason;
    }

    /// <summary>变更前的会话。</summary>
    public UserSession? Previous { get; }

    /// <summary>变更后的会话。</summary>
    public UserSession? Current { get; }

    /// <summary>变更原因。</summary>
    public string Reason { get; }

    /// <summary>是否为登录（由未登录变为已登录）。</summary>
    public bool IsSignIn => Previous is null && Current is not null;

    /// <summary>是否为登出（由已登录变为未登录）。</summary>
    public bool IsSignOut => Previous is not null && Current is null;

    /// <inheritdoc />
    public override string ToString()
        => $"SessionChanged(Previous={Previous?.UserName ?? "-"}, Current={Current?.UserName ?? "-"}, Reason={Reason})";
}
