namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 气缸参数键常量。
/// </summary>
public static class CylinderParameterKeys
{
    /// <summary>气缸名称（显示用）。</summary>
    public const string Name = "Name";

    /// <summary>所属工位。</summary>
    public const string StationId = "StationId";

    /// <summary>是否启用。</summary>
    public const string IsEnabled = "IsEnabled";

    /// <summary>伸出电磁阀输出点号。</summary>
    public const string ExtendOutput = "ExtendOutput";

    /// <summary>缩回电磁阀输出点号；不设置表示单控（弹簧复位）。</summary>
    public const string RetractOutput = "RetractOutput";

    /// <summary>伸出到位限位输入点号。</summary>
    public const string ExtendedInput = "ExtendedInput";

    /// <summary>缩回到位限位输入点号。</summary>
    public const string RetractedInput = "RetractedInput";

    /// <summary>动作超时（秒）。</summary>
    public const string ActionTimeoutSeconds = "ActionTimeoutSeconds";

    /// <summary>备注/机械说明。</summary>
    public const string Description = "Description";
}
