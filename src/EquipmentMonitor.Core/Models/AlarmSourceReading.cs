namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 一个报警源在当前时刻的读数。
/// </summary>
/// <remarks>
/// <b>三态是刻意的</b>：<see cref="IsAvailable"/>=false 表示「读不到」，不是「没有报警」。
/// 报警服务必须据此冻结该源已有的报警，否则一次断线就会把全部报警清空——
/// 操作工会误以为设备恢复了。
/// </remarks>
public sealed record AlarmSourceReading
{
    /// <summary>源是否可读。</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>当前仍然存在的报警；不可读时为空。</summary>
    public IReadOnlyList<AlarmSignal> Alarms { get; init; } = [];

    /// <summary>不可读时的原因。</summary>
    public string? Error { get; init; }

    /// <summary>可读。</summary>
    public static AlarmSourceReading Available(IReadOnlyList<AlarmSignal> alarms)
        => new() { IsAvailable = true, Alarms = alarms };

    /// <summary>不可读；服务会据此报 <c>COMM.SOURCE_OFFLINE</c> 并冻结该源。</summary>
    public static AlarmSourceReading Unavailable(string error)
        => new() { IsAvailable = false, Error = error };
}
