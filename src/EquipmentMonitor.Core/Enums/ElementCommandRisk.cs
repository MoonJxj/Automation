namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 命令的风险级别，决定 UI 是否需要二次确认。
/// </summary>
public enum ElementCommandRisk
{
    /// <summary>普通命令：不需要二次确认（如复位、下使能）。</summary>
    Normal = 0,

    /// <summary>危险命令：会引发机械动作，UI 必须二次确认（如定位、回零、点动）。</summary>
    Confirm = 1
}
