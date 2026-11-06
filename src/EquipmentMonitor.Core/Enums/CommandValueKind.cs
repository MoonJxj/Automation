namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 命令参数的值类型。决定 UI 控件形态与校验方式。
/// </summary>
public enum CommandValueKind
{
    /// <summary>无值。默认值，描述符校验会拒绝。</summary>
    None = 0,

    /// <summary>布尔（开关）。</summary>
    Bool = 1,

    /// <summary>数值（数字输入，带范围校验）。</summary>
    Number = 2,

    /// <summary>文本（可选时配合 Choices 渲染为下拉框）。</summary>
    Text = 3
}
