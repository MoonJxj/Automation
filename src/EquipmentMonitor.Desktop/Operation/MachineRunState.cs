namespace EquipmentMonitor.Desktop.Operation;

/// <summary>
/// 上位机侧维护的机台运行状态。
/// </summary>
/// <remarks>
/// <para><b>为什么不是 <see cref="EquipmentMonitor.Core.Enums.MachineState"/></b>：Core 的机台状态机
/// 需要自动流程、暂停/维护迁移与配置驱动的聚合规则，当前尚未装配。这里只表达
/// “自动监控页发起过启动、且启动序列成功”这一层事实，不冒充完整机台状态机。</para>
/// <para><b>可观察性边界</b>：当前启动序列只对伺服轴执行 <c>MC_Power</c>，停止序列只执行
/// <c>MC_Halt</c>；没有 PLC 自动循环点位，因此“运行中”表示“启动序列已完成”，
/// 不表示物料/节拍正在流动。界面必须如实标注这一点。</para>
/// </remarks>
public enum MachineRunState
{
    /// <summary>已停止（尚未启动，或停止序列成功）。</summary>
    Stopped = 0,

    /// <summary>运行中（启动序列成功，轴已使能）。</summary>
    Running = 1,

    /// <summary>启动/停止序列未完整成功，需要人工确认设备实际状态。</summary>
    Faulted = 2
}

/// <summary>正在进行的机台级操作；<see cref="None"/> 表示空闲。</summary>
public enum MachineRunActivity
{
    /// <summary>空闲。</summary>
    None = 0,

    /// <summary>正在执行启动序列。</summary>
    Starting = 1,

    /// <summary>正在执行停止序列。</summary>
    Stopping = 2,

    /// <summary>正在复位轴故障。</summary>
    Resetting = 3
}

/// <summary>
/// 机台运行状态的一帧不可变快照；UI 只读它，不直接触碰元件。
/// </summary>
/// <param name="State">当前运行状态。</param>
/// <param name="Activity">正在进行的操作。</param>
/// <param name="ChangedAt">最近一次状态变化时刻（本地时间）。</param>
/// <param name="Message">最近一次成功操作的说明。</param>
/// <param name="LastError">最近一次失败的原因；无失败时为 <c>null</c>。</param>
public sealed record MachineRunSnapshot(
    MachineRunState State,
    MachineRunActivity Activity,
    DateTimeOffset? ChangedAt,
    string? Message,
    string? LastError)
{
    /// <summary>是否正在执行启动/停止/复位序列。</summary>
    public bool IsBusy => Activity != MachineRunActivity.None;

    /// <summary>尚未执行任何机台级操作时的初始快照。</summary>
    public static MachineRunSnapshot Empty { get; } = new(
        MachineRunState.Stopped,
        MachineRunActivity.None,
        null,
        "设备尚未启动。",
        null);
}
