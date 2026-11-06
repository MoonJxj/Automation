namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 一条命令当前可否执行，以及不可执行的原因。
/// </summary>
/// <remarks>
/// <para><b>存在的理由是“置灰的按钮要说得出为什么”</b>：操作工看见按钮不可用却没有解释时，
/// 会去找钳子（物理强制）；写明“伺服未使能，不能定位”比一个灰按钮安全得多。</para>
/// <para><b>一致性约束</b>：本对象返回 <see cref="Blocked"/> ⇔ 执行入口会以 <c>Result.Fail</c> 拒绝。
/// 两处判据必须同源（实现里是同一个私有方法）。</para>
/// <para>刻意用 <c>sealed record</c>：结构体的 <c>default</c> 会得到一个“不可用但没有原因”的非法态。</para>
/// </remarks>
public sealed record CommandAvailability
{
    private CommandAvailability(bool isAvailable, string? reason)
    {
        IsAvailable = isAvailable;
        Reason = reason;
    }

    /// <summary>当前是否可执行。</summary>
    public bool IsAvailable { get; }

    /// <summary>不可执行的原因（面向操作工）。可执行时为 <c>null</c>。</summary>
    public string? Reason { get; }

    /// <summary>可执行。</summary>
    public static CommandAvailability Available { get; } = new(true, null);

    /// <summary>不可执行，并给出原因。</summary>
    /// <param name="reason">原因，必填且应可读（如“未使能：先执行 MC_Power”）。</param>
    public static CommandAvailability Blocked(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Blocked 必须给出可读的原因 —— 置灰而不解释的按钮会让操作工绕开软件。",
                nameof(reason));
        }

        return new CommandAvailability(false, reason);
    }

    /// <inheritdoc />
    public override string ToString()
        => IsAvailable ? "Available" : $"Blocked({Reason})";
}
