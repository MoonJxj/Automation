namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 参数更新动作：保存配置、下发控制器，还是两者都做。
/// </summary>
/// <remarks>
/// 分开是刻意的：现场需要“先改配置、等停机窗口再下发”，也需要“临时下发调试、先不落盘”。
/// 把所有场景硬编码成“保存即下发”会让调试和正式配置互相干扰。
/// </remarks>
public enum ParameterUpdateMode
{
    /// <summary>只保存配置，不碰控制器。</summary>
    SaveOnly = 0,

    /// <summary>只写控制器，不保存配置（调试用；重启后回到配置文件的值）。</summary>
    ApplyOnly = 1,

    /// <summary>先写控制器，成功后保存配置；任一步失败返回失败。</summary>
    SaveAndApply = 2
}
