namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 命令参数键的常量表。
/// </summary>
/// <remarks>
/// 键名同时被三处引用：元件构造描述符、UI 收集输入、元件执行时取值。
/// 字面量散落三处时，拼错一个字母没有任何地方会报错 —— 只会表现为“参数没生效”。
/// </remarks>
public static class CommandParameterKeys
{
    /// <summary>使能请求（MC_Power）。</summary>
    public const string Enable = "Enable";

    /// <summary>目标位置（MC_MoveAbsolute）。</summary>
    public const string Position = "Position";

    /// <summary>相对位移（MC_MoveRelative）。</summary>
    public const string Distance = "Distance";

    /// <summary>回零方式（MC_Home）。</summary>
    public const string HomingMethod = "HomingMethod";
}
