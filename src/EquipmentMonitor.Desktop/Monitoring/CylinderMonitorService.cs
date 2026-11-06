using EquipmentMonitor.Core.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EquipmentMonitor.Desktop.Monitoring;

/// <summary>
/// 气缸采集服务：按固定周期刷新全部气缸元件，并发布不可变快照。
/// </summary>
/// <remarks>
/// <para>与 <see cref="AxisMonitorService"/> 分开：轴/气缸的刷新频率可以不同，
/// 且一根轴掉线不应拖慢气缸状态。</para>
/// <para>单个气缸刷新失败只记录错误并继续，避免一缸掉线让整页停更。</para>
/// </remarks>
public sealed class CylinderMonitorService : BackgroundService
{
    /// <summary>采集周期。</summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(200);

    private readonly CylinderSnapshotStore _store;
    private readonly ILogger<CylinderMonitorService> _logger;

    /// <summary>创建采集服务。</summary>
    public CylinderMonitorService(CylinderSnapshotStore store, ILogger<CylinderMonitorService> logger)
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
            // 宿主停止：正常退出。
        }
    }

    /// <summary>执行一轮采集并发布快照（测试直接调用）。</summary>
    internal async Task<CylinderMonitorSnapshot> RefreshOnceAsync(CancellationToken cancellationToken)
    {
        var elements = _store.Elements;
        var cylinders = new List<CylinderSnapshot>(elements.Count);
        string? lastError = null;

        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await element.RefreshAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsFailure)
            {
                lastError = $"{element.ElementMetadata.ElementId}：{result.Error}";
                _logger.LogDebug(
                    "气缸 {ElementId} 刷新失败：{Error}",
                    element.ElementMetadata.ElementId,
                    result.Error);
            }

            cylinders.Add(new CylinderSnapshot(
                element.ElementMetadata.ElementId,
                element.ElementMetadata.Name,
                element.ControllerNo,
                element.State,
                element.Status,
                element.LastError,
                DateTimeOffset.Now,
                element.StationId));
        }

        var snapshot = new CylinderMonitorSnapshot(cylinders, DateTimeOffset.Now, lastError);
        _store.Publish(snapshot);
        return snapshot;
    }
}
