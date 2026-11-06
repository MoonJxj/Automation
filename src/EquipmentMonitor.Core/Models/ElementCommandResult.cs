using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 元件命令被接受并到达终态后的结果，语义对齐 PLCopen 输出（Done / CommandAborted / Error）加 Timeout。
/// </summary>
/// <remarks>
/// <para><b>只用于“已被接受”的命令</b>：未知命令、参数非法、互锁拒绝走 <c>Result.Fail</c>，
/// 不产生本对象 —— 分层原因见 <c>IElementCommandTarget</c>。</para>
/// <para>构造走工厂方法，强制不变量：Done 不带错误码、Error 必须有错误码与消息。</para>
/// </remarks>
public sealed record ElementCommandResult
{
    private ElementCommandResult(
        string commandKey,
        CommandOutcome outcome,
        string? errorCode,
        string? message,
        int? deviceAlarmCode,
        DateTimeOffset timestamp)
    {
        CommandKey = commandKey;
        Outcome = outcome;
        ErrorCode = errorCode;
        Message = message;
        DeviceAlarmCode = deviceAlarmCode;
        Timestamp = timestamp;
    }

    /// <summary>命令键（与请求一致）。</summary>
    public string CommandKey { get; init; }

    /// <summary>终态。</summary>
    public CommandOutcome Outcome { get; init; }

    /// <summary>错误码。仅 <see cref="CommandOutcome.Error"/> 时非空。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>面向操作工的消息（如“期望到位，实际仍在运动”）。</summary>
    public string? Message { get; init; }

    /// <summary>设备侧报警码（如伺服 FaultCode），与 ErrorCode 分开：一个是设备说的，一个是我们归类的。</summary>
    public int? DeviceAlarmCode { get; init; }

    /// <summary>终态产生的时刻。</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>成功完成（回读满足期望）。</summary>
    public static ElementCommandResult Done(string commandKey, DateTimeOffset timestamp, string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandKey);
        return new ElementCommandResult(commandKey, CommandOutcome.Done, null, message, null, timestamp);
    }

    /// <summary>命令已下发，尚未观察到位（当前轴契约的默认成功结果）。</summary>
    public static ElementCommandResult Accepted(string commandKey, DateTimeOffset timestamp, string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandKey);
        return new ElementCommandResult(commandKey, CommandOutcome.Accepted, null, message, null, timestamp);
    }

    /// <summary>被新命令或停止命令中断。</summary>
    public static ElementCommandResult Aborted(string commandKey, DateTimeOffset timestamp, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new ElementCommandResult(commandKey, CommandOutcome.CommandAborted, null, message, null, timestamp);
    }

    /// <summary>执行中失败。</summary>
    public static ElementCommandResult Error(
        string commandKey,
        DateTimeOffset timestamp,
        string errorCode,
        string message,
        int? deviceAlarmCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new ElementCommandResult(commandKey, CommandOutcome.Error, errorCode, message, deviceAlarmCode, timestamp);
    }

    /// <summary>回读在超时窗口内始终未满足期望。</summary>
    public static ElementCommandResult TimedOut(string commandKey, DateTimeOffset timestamp, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new ElementCommandResult(
            commandKey, CommandOutcome.Timeout, "COMMAND_TIMEOUT", message, null, timestamp);
    }

    /// <inheritdoc />
    public override string ToString()
        => $"ElementCommandResult({CommandKey}, {Outcome}, Code={ErrorCode ?? "-"}, " +
           $"Alarm={DeviceAlarmCode?.ToString() ?? "-"})";
}
