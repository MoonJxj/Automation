using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Events;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 报警的唯一入口：设备上报、服务评估、界面查询、人工确认都经过它。
/// </summary>
/// <remarks>
/// <para><b>唯一性</b>：这是报警事实的唯一写入口，也是活动报警的唯一读入口。
/// 不要在元件、驱动或页面里再维护一份报警集合，也不要各自拼报警文本。</para>
/// <para><b>线程</b>：实现必须线程安全；<see cref="Changed"/> 在后台线程触发，
/// 订阅方（尤其界面）自行 <c>BeginInvoke</c> 切回。</para>
/// <para><b>失败</b>：参数错误、报警不存在返回 <see cref="Result.Fail"/>；
/// 越权抛 <see cref="UnauthorizedAccessException"/>（与其他业务入口一致）。</para>
/// </remarks>
public interface IAlarmService
{
    /// <summary>当前活动报警快照；任何线程可读。</summary>
    AlarmSnapshot Snapshot { get; }

    /// <summary>活动报警发生变化时触发；事件在后台线程。</summary>
    event EventHandler<AlarmChangedEventArgs>? Changed;

    /// <summary>
    /// 采集一轮：遍历全部 <see cref="IAlarmSource"/>，做上升沿/恢复/去重。
    /// </summary>
    /// <remarks>
    /// <b>只应由后台采集服务调用</b>（<c>AlarmMonitorService</c>），界面不调用。
    /// 单源异常必须被隔离，不能中断其余源。
    /// </remarks>
    Task EvaluateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 上报一条瞬时报警（命令失败、初始化失败、配置错误）。
    /// </summary>
    /// <remarks>
    /// 同步返回：只做内存更新与事件通知，历史落盘由实现内部排队；
    /// 调用方（可能是命令线程）不会被 I/O 阻塞。
    /// </remarks>
    Result Report(AlarmSignal signal);

    /// <summary>
    /// 确认一条报警。权限：<see cref="Permission.AlarmAcknowledge"/>。
    /// </summary>
    /// <param name="alarmId">报警实例标识（<see cref="AlarmRecord.Id"/>）。</param>
    Result Acknowledge(string alarmId);

    /// <summary>确认全部未确认报警；<paramref name="sourceId"/> 非空时只确认该来源。</summary>
    Result AcknowledgeAll(string? sourceId = null);

    /// <summary>查询历史；实现负责把 <see cref="AlarmHistoryQuery.Limit"/> 夹到合法范围。</summary>
    Task<IReadOnlyList<AlarmRecord>> QueryHistoryAsync(
        AlarmHistoryQuery query,
        CancellationToken cancellationToken = default);
}
