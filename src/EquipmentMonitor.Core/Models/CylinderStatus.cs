using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 气缸的状态快照（不可变）。由 <c>CylinderElement.RefreshAsync</c> 在采集线程构造。
/// </summary>
/// <param name="IsEnabled">气缸是否参与控制（配置禁用时为 <c>false</c>）。</param>
/// <param name="Position">由两个限位开关推导的位置。</param>
/// <param name="ExtendedSensor">伸出限位是否亮。</param>
/// <param name="RetractedSensor">缩回限位是否亮。</param>
/// <param name="IsMoving">是否已下发方向命令、仍在等待到位。</param>
/// <param name="IsFault">是否需要复位。</param>
/// <param name="FaultCode">故障码；0 表示无故障。</param>
/// <param name="LastError">最近一次错误说明；正常为 <c>null</c>。</param>
/// <param name="UpdatedAt">快照时间。</param>
public sealed record CylinderStatus(
    bool IsEnabled,
    CylinderPosition Position,
    bool ExtendedSensor,
    bool RetractedSensor,
    bool IsMoving,
    bool IsFault,
    int FaultCode,
    string? LastError,
    DateTimeOffset UpdatedAt)
{
    /// <summary>初始快照：未采集、未知位置。</summary>
    public static CylinderStatus Unknown { get; } = new(
        IsEnabled: false,
        Position: CylinderPosition.Unknown,
        ExtendedSensor: false,
        RetractedSensor: false,
        IsMoving: false,
        IsFault: false,
        FaultCode: 0,
        LastError: null,
        UpdatedAt: default);

    /// <summary>是否伸出到位。</summary>
    public bool IsExtended => Position == CylinderPosition.Extended;

    /// <summary>是否缩回到位。</summary>
    public bool IsRetracted => Position == CylinderPosition.Retracted;

    /// <summary>位置显示文本。</summary>
    public string PositionText => Position switch
    {
        CylinderPosition.Unknown => "未知",
        CylinderPosition.Retracted => "缩回到位",
        CylinderPosition.Extended => "伸出到位",
        CylinderPosition.Moving => "动作中",
        CylinderPosition.Intermediate => "中间/矛盾",
        CylinderPosition.Fault => "故障",
        _ => Position.ToString()
    };
}
