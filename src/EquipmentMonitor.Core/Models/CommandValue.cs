using System.Globalization;
using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 一个命令参数值：轻量判别联合（Bool / Number / Text），不含任何业务含义。
/// </summary>
/// <remarks>
/// <para>用结构体而不是 <c>object</c>：UI 从描述符收集输入、元件按类型取用，
/// 两侧都不做强制转换 —— <c>object</c> 会把类型错误推迟到运行期并变成 <c>InvalidCastException</c>。</para>
/// <para><see cref="None"/>（<c>default</c>）表示“没有值”，不得被当成 0 或 false 使用。</para>
/// </remarks>
public readonly struct CommandValue : IEquatable<CommandValue>
{
    private readonly double _number;
    private readonly bool _flag;
    private readonly string? _text;

    private CommandValue(CommandValueKind kind, double number, bool flag, string? text)
    {
        Kind = kind;
        _number = number;
        _flag = flag;
        _text = text;
    }

    /// <summary>值类型。</summary>
    public CommandValueKind Kind { get; }

    /// <summary>是否携带值。</summary>
    public bool HasValue => Kind != CommandValueKind.None;

    /// <summary>无值。</summary>
    public static CommandValue None => default;

    /// <summary>布尔值。</summary>
    public static CommandValue FromBool(bool value)
        => new(CommandValueKind.Bool, 0, value, null);

    /// <summary>数值。必须是有限数 —— NaN/∞ 进不了设备。</summary>
    public static CommandValue FromNumber(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "命令参数必须是有限数。");
        }

        return new CommandValue(CommandValueKind.Number, value, false, null);
    }

    /// <summary>文本值。</summary>
    public static CommandValue FromText(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new CommandValue(CommandValueKind.Text, 0, false, value);
    }

    /// <summary>按布尔取值。类型不符返回 <c>false</c>。</summary>
    public bool TryAsBool(out bool value)
    {
        if (Kind == CommandValueKind.Bool)
        {
            value = _flag;
            return true;
        }

        value = false;
        return false;
    }

    /// <summary>按数值取值（Bool 提升为 0/1）。类型不符返回 <c>false</c>。</summary>
    public bool TryAsNumber(out double value)
    {
        switch (Kind)
        {
            case CommandValueKind.Number:
                value = _number;
                return true;
            case CommandValueKind.Bool:
                value = _flag ? 1 : 0;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    /// <summary>按文本取值。类型不符返回 <c>false</c>。</summary>
    public bool TryAsText(out string value)
    {
        if (Kind == CommandValueKind.Text && _text is not null)
        {
            value = _text;
            return true;
        }

        value = string.Empty;
        return false;
    }

    /// <inheritdoc />
    public bool Equals(CommandValue other)
        => Kind == other.Kind
           && _number.Equals(other._number)
           && _flag == other._flag
           && string.Equals(_text, other._text, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is CommandValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, _number, _flag, _text);

    /// <summary>相等比较。</summary>
    public static bool operator ==(CommandValue left, CommandValue right) => left.Equals(right);

    /// <summary>不等比较。</summary>
    public static bool operator !=(CommandValue left, CommandValue right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => Kind switch
    {
        CommandValueKind.Bool => _flag ? "true" : "false",
        CommandValueKind.Number => _number.ToString("0.###", CultureInfo.InvariantCulture),
        CommandValueKind.Text => _text ?? string.Empty,
        _ => "-"
    };
}
