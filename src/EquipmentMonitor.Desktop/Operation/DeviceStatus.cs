using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Monitoring;

namespace EquipmentMonitor.Desktop.Operation;

/// <summary>设备状态汇总的种类；界面只按它选颜色与文案，不重新推导状态。</summary>
public enum DeviceStatusKind
{
    /// <summary>没有可判定的数据（尚未采集 / 全部轴未初始化）。</summary>
    Unknown = 0,

    /// <summary>待机：全部元件就绪，等待启动。</summary>
    Idle = 1,

    /// <summary>运行中：设备级启动序列已完成。</summary>
    Running = 2,

    /// <summary>动作中：手动点动/定位等元件动作正在进行。</summary>
    Moving = 3,

    /// <summary>警告：元件报警但尚可运行。</summary>
    Warning = 4,

    /// <summary>故障：有元件 Error，或设备级序列未完整完成。</summary>
    Fault = 5,

    /// <summary>启动/停止/复位序列正在执行。</summary>
    Busy = 6,

    /// <summary>采集异常：快照过期或最近一轮刷新失败。</summary>
    CollectionWarning = 7
}

/// <summary>设备状态汇总（文案 + 摘要），由快照纯函数计算。</summary>
/// <param name="Text">状态短文案，如“待机”“运行中”。</param>
/// <param name="Kind">状态种类，界面据此选颜色。</param>
/// <param name="Summary">一行摘要：轴/气缸/报警/采集。</param>
public sealed record DeviceStatus(string Text, DeviceStatusKind Kind, string Summary);

/// <summary>
/// 把轴、气缸、报警与机台运行快照汇总成一条设备状态。
/// </summary>
/// <remarks>
/// <para><b>优先级</b>：操作进行中 &gt; 故障 &gt; 停止未完成 &gt; 运行 &gt; 动作 &gt; 警告 &gt; 采集异常 &gt; 待机。
/// 故障优先于运行：设备级状态说“运行中”，但某根轴已报 Error 时，操作工必须首先看到故障。</para>
/// <para>纯函数、不碰控件，便于测试；“设备状态”文案只在这里定义一次，状态栏/页面不再各写一套。</para>
/// </remarks>
public static class DeviceStatusEvaluator
{
    /// <summary>计算设备状态。</summary>
    /// <param name="run">机台运行快照。</param>
    /// <param name="axes">最近一帧轴快照。</param>
    /// <param name="cylinders">最近一帧气缸快照。</param>
    /// <param name="alarms">活动报警快照；未接入报警服务时为 <c>null</c>。</param>
    public static DeviceStatus Evaluate(
        MachineRunSnapshot run,
        AxisMonitorSnapshot axes,
        CylinderMonitorSnapshot cylinders,
        AlarmSnapshot? alarms)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(axes);
        ArgumentNullException.ThrowIfNull(cylinders);

        var summary = BuildSummary(axes, cylinders, alarms);
        var hasFault = axes.Axes.Any(axis => axis.State == ElementState.Error || axis.Status.IsFault)
                       || cylinders.Cylinders.Any(cylinder => cylinder.State == ElementState.Error);

        if (run.Activity != MachineRunActivity.None)
        {
            return new DeviceStatus(ActivityText(run.Activity), DeviceStatusKind.Busy, summary);
        }

        if (hasFault)
        {
            return new DeviceStatus("故障", DeviceStatusKind.Fault, summary);
        }

        if (run.State == MachineRunState.Faulted)
        {
            return new DeviceStatus("停止未完成", DeviceStatusKind.Fault, summary);
        }

        if (run.State == MachineRunState.Running)
        {
            return new DeviceStatus("运行中", DeviceStatusKind.Running, summary);
        }

        if (axes.Axes.Any(axis => axis.State == ElementState.Running)
            || cylinders.Cylinders.Any(cylinder => cylinder.State == ElementState.Running))
        {
            return new DeviceStatus("动作中", DeviceStatusKind.Moving, summary);
        }

        if (axes.Axes.Any(axis => axis.State == ElementState.Warning)
            || cylinders.Cylinders.Any(cylinder => cylinder.State == ElementState.Warning))
        {
            return new DeviceStatus("警告", DeviceStatusKind.Warning, summary);
        }

        if (axes.LastError is not null || cylinders.LastError is not null)
        {
            return new DeviceStatus("采集异常", DeviceStatusKind.CollectionWarning, summary);
        }

        if (axes.Axes.Count == 0 && cylinders.Cylinders.Count == 0)
        {
            return new DeviceStatus("未就绪", DeviceStatusKind.Unknown, summary);
        }

        if (axes.Axes.Any(axis => axis.State == ElementState.Unknown)
            || cylinders.Cylinders.Any(cylinder => cylinder.State == ElementState.Unknown))
        {
            return new DeviceStatus("未就绪", DeviceStatusKind.Unknown, summary);
        }

        return new DeviceStatus("待机", DeviceStatusKind.Idle, summary);
    }

    private static string BuildSummary(
        AxisMonitorSnapshot axes,
        CylinderMonitorSnapshot cylinders,
        AlarmSnapshot? alarms)
    {
        var readyAxes = axes.Axes.Count(axis => axis.State == ElementState.Ready);
        var axesText = axes.Axes.Count == 0
            ? "轴 0"
            : $"轴 {readyAxes}/{axes.Axes.Count} 就绪";

        var cylindersText = cylinders.Cylinders.Count == 0
            ? "气缸 0"
            : $"气缸 {cylinders.Cylinders.Count}";

        var alarmsText = alarms is null
            ? "报警 —"
            : alarms.Count == 0
                ? "报警 0"
                : $"报警 {alarms.Count}（未确认 {alarms.UnacknowledgedCount}）";

        var collectionText = axes.LastError is null && cylinders.LastError is null
            ? "采集正常"
            : "采集异常";

        return $"{axesText} · {cylindersText} · {alarmsText} · {collectionText}";
    }

    private static string ActivityText(MachineRunActivity activity) => activity switch
    {
        MachineRunActivity.Starting => "启动中…",
        MachineRunActivity.Stopping => "停止中…",
        MachineRunActivity.Resetting => "复位中…",
        _ => "处理中…"
    };
}
