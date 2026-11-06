using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EquipmentMonitor.Desktop.Monitoring;

/// <summary>
/// 报警采集宿主：按配置周期调用 <see cref="IAlarmService.EvaluateAsync"/>。
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="AxisMonitorService"/> 分开：报警采集失败不影响轴快照刷新，反之亦然。
/// 单源异常在 <c>AlarmService</c> 内部隔离，这里只兜底整轮异常，避免一次异常让服务退出。
/// </para>
/// <para>
/// <b>报警不是安全功能</b>：500ms 的采集周期只用于监控与提示，不能替代急停/安全门。
/// </para>
/// </remarks>
public sealed class AlarmMonitorService : BackgroundService
{
    private readonly IAlarmService _alarms;
    private readonly ILogger<AlarmMonitorService> _logger;
    private readonly TimeSpan _interval;

    /// <summary>创建报警采集服务。</summary>
    /// <param name="alarms">报警服务。</param>
    /// <param name="options">报警配置（采集周期）。</param>
    /// <param name="logger">日志。</param>
    public AlarmMonitorService(
        IAlarmService alarms,
        IOptions<AlarmOptions> options,
        ILogger<AlarmMonitorService> logger)
    {
        ArgumentNullException.ThrowIfNull(alarms);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _alarms = alarms;
        _logger = logger;

        var milliseconds = Math.Max(100, options.Value.PollIntervalMs);
        _interval = TimeSpan.FromMilliseconds(milliseconds);
    }

    /// <summary>采集周期（测试用）。</summary>
    internal TimeSpan Interval => _interval;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await _alarms.EvaluateAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // 整轮失败不能终止后台服务：下一轮继续。
                    _logger.LogError(ex, "报警采集失败，等待下一轮。");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 宿主停止：正常退出。
        }
    }

    /// <summary>执行一轮采集（测试用，不启动宿主时钟）。</summary>
    internal Task EvaluateOnceAsync(CancellationToken cancellationToken = default)
        => _alarms.EvaluateAsync(cancellationToken);
}
