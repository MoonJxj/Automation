using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 命令的元数据描述：这条命令是什么、怎么激活、多大风险、需要哪些参数。
/// </summary>
/// <remarks>
/// <para><b>键名直接用 PLCopen 功能块名</b>（<c>MC_Power</c> / <c>MC_MoveAbsolute</c>…），
/// 中文显示名放 <see cref="Name"/>，便于与 PLC 侧文档互查。</para>
/// <para><b>本类型不承载执行逻辑</b>：互锁与执行在元件实现里，描述符只声明形状。</para>
/// </remarks>
public sealed record ElementCommandDescriptor
{
    /// <summary>命令键，元件内唯一。约定使用 PLCopen 块名。</summary>
    public required string Key { get; init; }

    /// <summary>界面显示名（中文）。为空时回退到 <see cref="Key"/>。</summary>
    public string? Name { get; init; }

    /// <summary>说明，用于界面提示与文档。</summary>
    public string? Description { get; init; }

    /// <summary>激活方式。无缺省值 —— 必须显式声明，误标会导致“保持使能”变成“使能一次”。</summary>
    public ElementCommandActivation Activation { get; init; } = ElementCommandActivation.Unknown;

    /// <summary>风险级别，决定 UI 是否二次确认。</summary>
    public ElementCommandRisk Risk { get; init; } = ElementCommandRisk.Normal;

    /// <summary>参数表。UI 按此生成控件；空列表表示无参数。</summary>
    public IReadOnlyList<CommandParameterDescriptor> Parameters { get; init; } = [];

    /// <summary>显示名（回退到 <see cref="Key"/>）。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Key : Name;

    /// <summary>按参数键查找描述符。不存在时返回 <c>null</c>。</summary>
    public CommandParameterDescriptor? FindParameter(string parameterKey)
    {
        foreach (var parameter in Parameters)
        {
            if (string.Equals(parameter.Key, parameterKey, StringComparison.Ordinal))
            {
                return parameter;
            }
        }

        return null;
    }

    /// <summary>校验描述符自身（编程错误，构造元件时当场抛）。</summary>
    /// <exception cref="InvalidOperationException">描述符不合法。</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new InvalidOperationException("命令描述符缺少 Key。");
        }

        if (Activation == ElementCommandActivation.Unknown)
        {
            throw new InvalidOperationException(
                $"命令 {Key} 未指定激活方式（Unknown）—— 必须显式声明 Execute 或 Enable。");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in Parameters)
        {
            parameter.Validate();

            if (!seen.Add(parameter.Key))
            {
                throw new InvalidOperationException($"命令 {Key} 的参数键 {parameter.Key} 重复。");
            }
        }
    }

    /// <inheritdoc />
    public override string ToString()
        => $"ElementCommandDescriptor({Key}, {Activation}, Risk={Risk}, Params={Parameters.Count})";
}
