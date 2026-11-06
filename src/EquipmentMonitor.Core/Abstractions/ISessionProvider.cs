using EquipmentMonitor.Core.Events;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 当前会话的唯一持有者（进程内单例）：登录写入、登出清空、变更时通知。
/// </summary>
/// <remarks>
/// <para><b>为什么需要一个独立的持有者</b>：权限服务、界面、审计都要读“当前是谁”。
/// 如果把它塞进 <see cref="IPermissionService"/>，将来远端令牌刷新、Windows 身份降级
/// 就要绕开权限服务去改状态，两处状态一定分叉。</para>
/// <para><b>线程约定</b>：<see cref="Current"/> 可以任意线程读取；
/// <see cref="Changed"/> 在引发者线程触发（可能是后台线程），UI 订阅方自行 <c>BeginInvoke</c>。</para>
/// </remarks>
public interface ISessionProvider
{
    /// <summary>当前会话；未登录时为 <c>null</c>。</summary>
    UserSession? Current { get; }

    /// <summary>会话变更（登录 / 切换 / 登出）。</summary>
    event EventHandler<SessionChangedEventArgs>? Changed;

    /// <summary>建立会话（覆盖旧会话）。</summary>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> 为 <c>null</c>。</exception>
    void Set(UserSession session);

    /// <summary>清空会话。未登录时是空操作。</summary>
    /// <param name="reason">可读原因，会带进事件参数与审计。</param>
    void Clear(string reason);
}
