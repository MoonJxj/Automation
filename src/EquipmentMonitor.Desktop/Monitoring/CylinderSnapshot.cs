using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Desktop.Monitoring;

/// <summary>
/// 单个气缸的界面快照：后台采集线程写出，UI 线程只读。
/// </summary>
public sealed record CylinderSnapshot(
    string ElementId,
    string Name,
    int ControllerNo,
    ElementState State,
    CylinderStatus Status,
    string? Error,
    DateTimeOffset CapturedAt,
    int StationId = 0)
{
    /// <summary>位置显示文本。</summary>
    public string PositionText => Status.PositionText;
}
