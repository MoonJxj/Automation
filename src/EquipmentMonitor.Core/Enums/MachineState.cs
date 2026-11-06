namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 机台级状态。与 <see cref="ElementState"/> 是<b>两套独立的状态机</b>。
/// </summary>
/// <remarks>
/// <para><b>为什么机台状态不等于「元件状态的聚合」：</b></para>
/// <para>
/// 若机台状态只是元件的聚合（任一元件 Error → 机台 Error），
/// 就表达不了 <see cref="Maintenance"/> 与 <see cref="Paused"/> ——
/// 这两种状态<strong>整台机器完好</strong>，只是被人为停机。
/// 把它们塞进元件状态模型会让每个元件都要有「维护中」这种荒唐的状态。
/// </para>
/// <para>
/// 因此：机台状态机接受元件的聚合结果<b>作为迁移的触发条件之一</b>，
/// 但拥有人为的迁移路径（维护、暂停）。聚合只提供「设备侧的事实」，
/// 机台状态还包含「管理侧的意图」。
/// </para>
/// <para>聚合规则本身<b>配置驱动</b>，不硬编码 —— 见 <c>MachineStateMachine</c> 的备注。</para>
/// </remarks>
public enum MachineState
{
    /// <summary>未初始化。</summary>
    Unknown = 0,

    /// <summary>待机。全部元件就绪，等待启动。</summary>
    Idle = 1,

    /// <summary>自动运行中。整台机器处于自动运行状态。</summary>
    Running = 2,

    /// <summary>
    /// 暂停。人为停机，机器本身完好，可随时恢复。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Maintenance"/> 的区别：暂停通常是短时的（换料、调整），
    /// 维护是计划内的停机检修。两者在报表上的统计口径不同，因此分开。
    /// </remarks>
    Paused = 3,

    /// <summary>警告。可继续运行但需要关注（个别元件报警、参数越界）。</summary>
    Warning = 4,

    /// <summary>错误。有元件故障，机台不可运行，需要人工介入。</summary>
    Error = 5,

    /// <summary>
    /// 维护中。计划内停机检修，不计入故障统计。
    /// </summary>
    /// <remarks>
    /// <b>这个状态直接影响 OEE 类指标的计算</b> ——
    /// 维护停机与故障停机在可用率统计里是完全不同的科目。
    /// 这也是机台状态机必须与元件状态机分开的最实际的理由。
    /// </remarks>
    Maintenance = 6
}
