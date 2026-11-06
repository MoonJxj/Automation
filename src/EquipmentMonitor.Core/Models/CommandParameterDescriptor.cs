using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 命令参数的元数据：叫什么、什么类型、取值范围、缺省值。只描述形状，不承载值。
/// </summary>
/// <remarks>
/// <b>存在的理由是“界面按描述符生成”</b>：UI 依赖它自动生成控件（Bool → 开关、
/// Number → 数字输入 + 范围校验、Text + Choices → 下拉框），而不是为每条命令手写面板。
/// 手写面板在新增命令时必然漏改，而漏改的表现是“界面上少了一个参数”，不会报错。
/// </remarks>
public sealed record CommandParameterDescriptor
{
    /// <summary>参数键，命令内唯一。取值用 <see cref="CommandParameterKeys"/> 常量。</summary>
    public required string Key { get; init; }

    /// <summary>界面显示名（中文）。为空时回退到 <see cref="Key"/>。</summary>
    public string? Name { get; init; }

    /// <summary>值类型。决定控件形态与校验方式。</summary>
    public required CommandValueKind ValueKind { get; init; }

    /// <summary>工程单位（<c>mm</c> / <c>mm/s</c>）。无量纲时为 <c>null</c>。</summary>
    public string? Unit { get; init; }

    /// <summary>保留小数位。仅对 <see cref="CommandValueKind.Number"/> 有意义。</summary>
    public int? Precision { get; init; }

    /// <summary>合法范围下限（含）。</summary>
    public double? Minimum { get; init; }

    /// <summary>合法范围上限（含）。</summary>
    public double? Maximum { get; init; }

    /// <summary>缺省值。<see cref="CommandValue.None"/> 表示无缺省。</summary>
    public CommandValue DefaultValue { get; init; } = CommandValue.None;

    /// <summary>可选值列表（仅 <see cref="CommandValueKind.Text"/> 使用）。</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>是否必填。默认必填 —— 可选是例外，必须显式声明。</summary>
    public bool IsRequired { get; init; } = true;

    /// <summary>描述，用于界面提示与文档。</summary>
    public string? Description { get; init; }

    /// <summary>显示名（回退到 <see cref="Key"/>）。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Key : Name;

    /// <summary>校验描述符自身（编程错误，构造元件时当场抛）。</summary>
    /// <exception cref="InvalidOperationException">描述符不合法。</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new InvalidOperationException("命令参数描述符缺少 Key。");
        }

        if (ValueKind == CommandValueKind.None)
        {
            throw new InvalidOperationException($"命令参数 {Key} 未指定值类型（ValueKind 为 None）。");
        }

        if (Minimum is { } min && Maximum is { } max && min > max)
        {
            throw new InvalidOperationException(
                $"命令参数 {Key} 的范围颠倒：Minimum({min}) 大于 Maximum({max})。");
        }

        if (Precision is < 0 or > 15)
        {
            throw new InvalidOperationException(
                $"命令参数 {Key} 的 Precision 必须在 0–15 之间，当前值 {Precision}。");
        }

        if (Choices.Count > 0 && ValueKind != CommandValueKind.Text)
        {
            throw new InvalidOperationException(
                $"命令参数 {Key} 声明了 Choices，但值类型是 {ValueKind} —— 可选值列表只对 Text 有意义。");
        }

        if (IsRequired && DefaultValue.HasValue)
        {
            throw new InvalidOperationException(
                $"命令参数 {Key} 同时声明了 IsRequired = true 与 DefaultValue —— 两者矛盾。");
        }

        if (DefaultValue.HasValue)
        {
            if (DefaultValue.Kind != ValueKind)
            {
                throw new InvalidOperationException(
                    $"命令参数 {Key} 的值类型是 {ValueKind}，但缺省值类型是 {DefaultValue.Kind}。");
            }

            if (DefaultValue.TryAsNumber(out var numeric) && IsOutOfRange(numeric))
            {
                throw new InvalidOperationException(
                    $"命令参数 {Key} 的缺省值 {numeric} 超出范围 [{Minimum}, {Maximum}]。");
            }
        }
    }

    /// <summary>数值是否超出配置范围。</summary>
    public bool IsOutOfRange(double value)
        => (Minimum is { } min && value < min) || (Maximum is { } max && value > max);

    /// <inheritdoc />
    public override string ToString()
        => $"CommandParameterDescriptor({Key}, {ValueKind}, Unit={Unit ?? "-"}, " +
           $"Range=[{Minimum?.ToString() ?? "-"}, {Maximum?.ToString() ?? "-"}], Required={IsRequired})";
}
