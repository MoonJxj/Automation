using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Events;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Common;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 机台：上位机监控的完整机台，顶层实体。上位机软件的核心对象模型。
/// </summary>
public interface IMachine
{
    /// <summary>静态元数据。</summary>
    MachineMetadata Metadata { get; }

    /// <summary>当前机台状态。</summary>
    MachineState State { get; }

    /// <summary>机台下的全部元件。</summary>
    IReadOnlyCollection<IElement> Elements { get; }

    /// <summary>最近一次状态变化的时刻（UTC）。</summary>
    DateTimeOffset? LastStateChangedAt { get; }

    /// <summary>机台状态变化事件。<b>在轮询线程上触发</b>。</summary>
    event EventHandler<MachineStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// 刷新机台状态（异步）。上位机软件应定期调用此方法以获取最新状态。
    /// </summary>
    /// <returns></returns>
    Task<Result> RefreshAsync();

    /// <summary>
    /// 获取指定 ID 的元件。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="elementId"></param>
    /// <returns></returns>
    Result<T> GetElement<T>(string elementId) where T : class, IElement;

    /// <summary>初始化设备（异步）。</summary>
    Task<Result> InitializeDeviceAsync(CancellationToken ct = default);

    /// <summary>人为启动设备，写入审计日志。</summary>
    Task<Result> StartDeviceAsync(string reason, CancellationToken ct = default);

    /// <summary>人为停止设备，等待硬件完全静止后返回。</summary>
    Task<Result> StopTheDeviceAsync(string reason, CancellationToken ct = default);

    /// <summary>暂停 / 恢复（人为）。</summary>
    Task<Result> SetPausedAsync(bool paused, string reason, CancellationToken ct = default);

    /// <summary>重置设备（清报警、回原点等）。</summary>
    Task<Result> ResetDeviceAsync(CancellationToken ct = default);

    /// <summary>切换到维护状态（纯管理操作，也可统一为异步）。</summary>
    Task<Result> SetMaintenanceAsync(bool entering, string reason, CancellationToken ct = default);
}
