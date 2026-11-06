namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 一次命令请求：命令键 + 参数值集合。
/// </summary>
/// <remarks>
/// <para><b>为什么是“键 → 值”字典而不是强类型负载</b>：UI 要从描述符自动生成控件并收集输入，
/// 字典是唯一与描述符同构的形态；强类型负载会把 UI 与每种命令的形状绑死。</para>
/// <para>本类型<b>不做参数校验</b>：缺参、类型不符、越界由 <c>IElementCommandTarget.ExecuteCommandAsync</c>
/// 按描述符统一校验 —— 校验只在一处实现，UI 与服务端两份校验必然不一致。</para>
/// </remarks>
public sealed record ElementCommandRequest
{
    /// <summary>命令键。必须是目标元件命令表中存在的键。</summary>
    public required string CommandKey { get; init; }

    /// <summary>参数值集合（键取自 <see cref="CommandParameterKeys"/>）。无参数时为空。</summary>
    public IReadOnlyDictionary<string, CommandValue> Arguments { get; init; } = EmptyArguments;

    private static readonly IReadOnlyDictionary<string, CommandValue> EmptyArguments
        = new Dictionary<string, CommandValue>(0, StringComparer.Ordinal);

    /// <summary>取参数原始值。不存在时返回 <c>false</c>。</summary>
    public bool TryGetValue(string key, out CommandValue value)
        => Arguments.TryGetValue(key, out value);

    /// <summary>取布尔参数。</summary>
    public bool TryGetBool(string key, out bool value)
    {
        if (Arguments.TryGetValue(key, out var raw))
        {
            return raw.TryAsBool(out value);
        }

        value = false;
        return false;
    }

    /// <summary>取数值参数（Bool 按 0/1 提升）。</summary>
    public bool TryGetNumber(string key, out double value)
    {
        if (Arguments.TryGetValue(key, out var raw))
        {
            return raw.TryAsNumber(out value);
        }

        value = 0;
        return false;
    }

    /// <inheritdoc />
    public override string ToString()
        => $"ElementCommandRequest({CommandKey}, Args={Arguments.Count})";
}
