using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EquipmentMonitor.Desktop.Monitoring;

/// <summary>
/// 后台采集服务：按固定周期刷新全部轴元件，并把结果发布为不可变快照。
/// </summary>
/// <remarks>
/// <para><b>为什么采集放在宿主而不是窗体</b>：<c>AxisElement</c> 的命令/刷新最终会调用
/// 同步的厂商接口（Modbus 轴还要经同步适配桥阻塞），在 UI 线程调用就是「点一下卡一下」。
/// 采集线程是唯一触碰元件的一方，UI 只读 <see cref="AxisSnapshotStore"/>。</para>
/// <para><b>周期 200ms</b>：与交接文档的 0.1s 级轮询一致。它只用于监控，
/// 不参与急停/软限位等实时安全判断（那些由轴卡与 PLC 兜底）。</para>
/// <para><b>单轴失败不中断整轮</b>：某根轴刷新失败只记录错误并继续刷新其余轴，
/// 否则一根掉线会让整页停在旧数据上。</para>
/// </remarks>
public sealed class AxisMonitorService : BackgroundService
{
    /// <summary>采集周期。</summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(200);

    private readonly AxisSnapshotStore _store;
    private readonly ILogger<AxisMonitorService> _logger;

    /// <summary>创建采集服务。</summary>
    public AxisMonitorService(AxisSnapshotStore store, ILogger<AxisMonitorService> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);

        _store = store;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await RefreshOnceAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 宿主停止：正常退出，不当作错误。
        }
    }

    /// <summary>
    /// 执行一轮采集并发布快照。测试直接调用它，不必启动宿主时钟。
    /// </summary>
    /// <returns>本轮快照。</returns>
    internal async Task<AxisMonitorSnapshot> RefreshOnceAsync(CancellationToken cancellationToken)
    {
        var elements = _store.Elements;
        var axes = new List<AxisSnapshot>(elements.Count);
        string? lastError = null;

        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await element.RefreshAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsFailure)
            {
                lastError = $"{element.ElementMetadata.ElementId}：{result.Error}";

                // Debug 级别：轴离线时每 200ms 一条会淹没日志，界面已有错误栏。
                _logger.LogDebug(
                    "轴 {ElementId} 刷新失败：{Error}",
                    element.ElementMetadata.ElementId,
                    result.Error);
            }

            axes.Add(new AxisSnapshot(
                element.ElementMetadata.ElementId,
                element.ElementMetadata.Name,
                element.ControllerNo,
                element.AxisNo,
                element.State,
                element.Status,
                element.LastError?.Message,
                DateTimeOffset.Now,
                element.StationId));
        }

        var snapshot = new AxisMonitorSnapshot(axes, DateTimeOffset.Now, lastError);
        _store.Publish(snapshot);
        return snapshot;
    }
}
