using EquipmentMonitor.Core;
using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Desktop.Monitoring;

/// <summary>
/// 单根轴的界面快照：后台采集线程写出，UI 线程只读。
/// </summary>
/// <remarks>
/// <b>不可变</b>：采集线程每轮构造新实例整体替换，UI 拿到的永远是自洽的一帧，
/// 不需要为「读到一半被改」加锁。
/// </remarks>
/// <param name="ElementId">元件标识。</param>
/// <param name="Name">显示名称。</param>
/// <param name="ControllerNo">控制器编号。</param>
/// <param name="AxisNo">轴编号。</param>
/// <param name="State">元件状态。</param>
/// <param name="Status">轴状态快照。</param>
/// <param name="Error">最近一次刷新失败的说明；正常为 <c>null</c>。</param>
/// <param name="CapturedAt">采集时间。</param>
/// <param name="StationId">所属工位编号；0 表示未分组。</param>
public sealed record AxisSnapshot(
    string ElementId,
    string Name,
    int ControllerNo,
    int AxisNo,
    ElementState State,
    AxisStatus Status,
    string? Error,
    DateTimeOffset CapturedAt,
    int StationId = 0);
