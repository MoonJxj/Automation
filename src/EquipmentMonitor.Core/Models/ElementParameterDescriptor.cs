using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 元件参数的元数据：叫什么、什么类型、范围、权限、生效方式。只描述形状，不承载值。
/// </summary>
/// <remarks>
/// <para>与 <see cref="CommandParameterDescriptor"/> 分开：命令参数是“本次动作的参数”，
/// 元件参数是“持久化到轴/气缸/控制器上的配置”。两者的生命周期、权限和生效方式不同。</para>
/// <para>UI 按描述符自动生成参数页：Bool → 开关、Number → 数字输入 + 范围校验、
/// Text + Choices → 下拉框；不要为每个元件手写一套表单。</para>
/// </remarks>
public sealed record ElementParameterDescriptor
{
    /// <summary>参数键，元件内唯一。取值用轴/气缸各自的键常量。</summary>
    public required string Key { get; init; }

    /// <summary>界面显示名（中文）。为空时回退到 <see cref="Key"/>。</summary>
    public string? Name { get; init; }

    /// <summary>分组（如“身份”“IO 点”“动作”），UI 按此分栏。</summary>
    public string? Group { get; init; }

    /// <summary>值类型。</summary>
    public required CommandValueKind ValueKind { get; init; }

    /// <summary>工程单位；无量纲时为 <c>null</c>。</summary>
    public string? Unit { get; init; }

    /// <summary>保留小数位；仅 Number 有意义。</summary>
    public int? Precision { get; init; }

    /// <summary>合法范围下限（含）。</summary>
    public double? Minimum { get; init; }

    /// <summary>合法范围上限（含）。</summary>
    public double? Maximum { get; init; }

    /// <summary>缺省值；<see cref="CommandValue.None"/> 表示无缺省。</summary>
    public CommandValue DefaultValue { get; init; } = CommandValue.None;

    /// <summary>可选值列表；仅 Text 使用。</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>是否只读（例如运行时状态、由控制器回读的参数）。</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>作用域，决定权限与配置归属。</summary>
    public ParameterScope Scope { get; init; } = ParameterScope.Device;

    /// <summary>修改该参数所需的权限。</summary>
    public Permission RequiredPermission { get; init; } = Permission.MachineConfig;

    /// <summary>生效方式。</summary>
    public ParameterApplyMode ApplyMode { get; init; } = ParameterApplyMode.Restart;

    /// <summary>说明，用于界面提示与文档。</summary>
    public string? Description { get; init; }

    /// <summary>显示名（回退到 <see cref="Key"/>）。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Key : Name;

    /// <summary>数值是否超出范围。</summary>
    public bool IsOutOfRange(double value)
        => (Minimum is { } min && value < min) || (Maximum is { } max && value > max);

    /// <summary>校验描述符自身；描述符写错属于编程错误，构造参数目录时当场抛。</summary>
    /// <exception cref="InvalidOperationException">描述符不合法。</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new InvalidOperationException("元件参数描述符缺少 Key。");
        }

        if (ValueKind == CommandValueKind.None)
        {
            throw new InvalidOperationException($"元件参数 {Key} 未指定值类型（ValueKind 为 None）。");
        }

        if (Minimum is { } min && Maximum is { } max && min > max)
        {
            throw new InvalidOperationException(
                $"元件参数 {Key} 的范围颠倒：Minimum({min}) 大于 Maximum({max})。");
        }

        if (Precision is < 0 or > 15)
        {
            throw new InvalidOperationException(
                $"元件参数 {Key} 的 Precision 必须在 0–15 之间，当前值 {Precision}。");
        }

        if (Choices.Count > 0 && ValueKind != CommandValueKind.Text)
        {
            throw new InvalidOperationException(
                $"元件参数 {Key} 声明了 Choices，但值类型是 {ValueKind} —— 可选值列表只对 Text 有意义。");
        }

        if (DefaultValue.HasValue)
        {
            if (DefaultValue.Kind != ValueKind)
            {
                throw new InvalidOperationException(
                    $"元件参数 {Key} 的值类型是 {ValueKind}，但缺省值类型是 {DefaultValue.Kind}。");
            }

            if (DefaultValue.TryAsNumber(out var numeric) && IsOutOfRange(numeric))
            {
                throw new InvalidOperationException(
                    $"元件参数 {Key} 的缺省值 {numeric} 超出范围 [{Minimum}, {Maximum}]。");
            }
        }
    }

    /// <inheritdoc />
    public override string ToString()
        => $"ElementParameterDescriptor({Key}, {ValueKind}, Scope={Scope}, " +
           $"Permission={RequiredPermission}, Apply={ApplyMode}, ReadOnly={IsReadOnly})";
}
