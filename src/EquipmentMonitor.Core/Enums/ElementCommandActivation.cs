namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 命令的激活方式，对齐 PLCopen 的两类功能块模型。
/// </summary>
/// <remarks>
/// <see cref="Execute"/>：上升沿触发一次动作，UI 渲染为按钮。
/// <see cref="Enable"/>：电平语义，表达“期望保持的状态”，UI 渲染为开关。
/// 若把 MC_Power 误标为 Execute，症状是掉线恢复后轴不再使能，且没有任何报错。
/// </remarks>
public enum ElementCommandActivation
{
    /// <summary>未指定。描述符校验会拒绝。</summary>
    Unknown = 0,

    /// <summary>触发一次（PLCopen Execute/Done 型）。</summary>
    Execute = 1,

    /// <summary>电平使能（PLCopen Enable/Valid 型），如 MC_Power。</summary>
    Enable = 2
}
