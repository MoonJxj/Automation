using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 一个元件的参数值集合。
/// </summary>
/// <remarks>
/// <para><see cref="ElementId"/> 必须稳定：当前 <c>AxisElementConfig.ElementId</c>/<c>CylinderConfig.ElementId</c>
/// 由配置提供；参数编辑要落盘/回写，建议使用显式 <c>ElementId</c>，不要拿生成值当持久化主键。</para>
/// <para><see cref="Version"/> 用于乐观并发：读取时带回，保存时原样带回；存储实现发现版本不一致
/// 应返回冲突，而不是覆盖别人刚改的值。</para>
/// </remarks>
public sealed record ElementParameterSet
{
    /// <summary>元件标识（稳定、机台内唯一）。</summary>
    public required string ElementId { get; init; }

    /// <summary>参数键 → 值。键必须来自元件自己的键常量（如 <c>AxisParameterKeys</c>/<c>CylinderParameterKeys</c>）。</summary>
    public required IReadOnlyDictionary<string, CommandValue> Values { get; init; }

    /// <summary>配置版本/ETag；无版本控制时为 <c>null</c>。</summary>
    public string? Version { get; init; }

    /// <summary>按参数键取值。不存在或类型不符返回 <c>false</c>。</summary>
    public bool TryGetValue(string key, out CommandValue value)
        => Values.TryGetValue(key, out value);

    /// <summary>返回替换了某个参数值的新集合（原集合不变）。</summary>
    public ElementParameterSet WithValue(string key, CommandValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var values = new Dictionary<string, CommandValue>(Values, StringComparer.Ordinal)
        {
            [key] = value
        };

        return this with { Values = values };
    }

    /// <summary>返回删除了某个参数值的新集合。</summary>
    public ElementParameterSet WithoutValue(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!Values.ContainsKey(key))
        {
            return this;
        }

        var values = new Dictionary<string, CommandValue>(Values, StringComparer.Ordinal);
        values.Remove(key);
        return this with { Values = values };
    }

    /// <inheritdoc />
    public override string ToString()
        => $"ElementParameterSet({ElementId}, Values={Values.Count}, Version={Version ?? "-"})";
}

/// <summary>
/// 一个元件的参数快照：描述符 + 配置值 + 控制器回读值。
/// </summary>
/// <param name="ElementId">元件标识。</param>
/// <param name="DisplayName">显示名。</param>
/// <param name="Descriptors">参数描述符（UI 按此生成控件）。</param>
/// <param name="Values">配置中的参数值。</param>
/// <param name="ControllerValues">控制器回读值；控制器不支持参数能力时为 <c>null</c>。</param>
/// <param name="ReadAt">快照时间。</param>
public sealed record ElementParameterSnapshot(
    string ElementId,
    string DisplayName,
    IReadOnlyList<ElementParameterDescriptor> Descriptors,
    ElementParameterSet Values,
    ElementParameterSet? ControllerValues,
    DateTimeOffset ReadAt)
{
    /// <summary>按参数键查找描述符。</summary>
    public ElementParameterDescriptor? FindDescriptor(string key)
    {
        foreach (var descriptor in Descriptors)
        {
            if (string.Equals(descriptor.Key, key, StringComparison.Ordinal))
            {
                return descriptor;
            }
        }

        return null;
    }
}

/// <summary>单条参数校验问题。</summary>
/// <param name="Key">参数键；跨字段问题可为空。</param>
/// <param name="Code">稳定错误码，例如 <c>AXIS_PARAM_RANGE</c>。</param>
/// <param name="Message">可读说明。</param>
public sealed record ElementParameterValidationIssue(
    string? Key,
    string Code,
    string Message)
{
    /// <inheritdoc />
    public override string ToString()
        => string.IsNullOrWhiteSpace(Key) ? $"{Code}: {Message}" : $"{Key}: {Message}";
}

/// <summary>参数校验结果。</summary>
public sealed record ElementParameterValidationResult(
    IReadOnlyList<ElementParameterValidationIssue> Issues)
{
    /// <summary>是否全部合法。</summary>
    public bool IsValid => Issues.Count == 0;

    /// <summary>空结果（全部合法）。</summary>
    public static ElementParameterValidationResult Valid { get; } = new([]);
}

/// <summary>参数更新结果。</summary>
/// <param name="Values">更新后的完整参数集。</param>
/// <param name="ChangedKeys">本次实际变化的参数键。</param>
/// <param name="Persisted">是否已写入配置存储。</param>
/// <param name="AppliedToController">是否已写控制器。</param>
/// <param name="RestartRequired">是否有改动需要重启/重新装配才生效。</param>
public sealed record ElementParameterChangeResult(
    ElementParameterSet Values,
    IReadOnlyList<string> ChangedKeys,
    bool Persisted,
    bool AppliedToController,
    bool RestartRequired);
