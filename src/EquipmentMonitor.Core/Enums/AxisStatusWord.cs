using System;

namespace EquipmentMonitor.Core.Enums
{
    /// <summary>
    /// 轴状态字：16 位状态位，逐位对齐 <c>docs/axis-command-interface.md</c> §2.1。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 各品牌控制器把厂商状态字映射到本枚举定义的位含义后，
    /// 通过 <see cref="IAxisController.ReadStateWord"/> 的 <c>state</c> 参数返回；
    /// 调用方只依赖本枚举，不直接解释厂商位定义。
    /// </para>
    /// <para>
    /// bit12–bit15 保留，不得赋予临时语义。状态字是只读快照的一部分，
    /// 与 <see cref="AxisFeedback"/> 一起传递。
    /// </para>
    /// </remarks>
    [Flags]
    public enum AxisStatusWord : ushort
    {
        /// <summary>无任何位被置位。</summary>
        None = 0,

        /// <summary>bit0：驱动器使能中（Operation Enabled）。</summary>
        Enabled = 1 << 0,

        /// <summary>bit1：轴正在移动。</summary>
        Moving = 1 << 1,

        /// <summary>bit2：回零进行中。</summary>
        Homing = 1 << 2,

        /// <summary>bit3：定位进行中。</summary>
        Positioning = 1 << 3,

        /// <summary>bit4：报警（可继续运行，但需要关注）。</summary>
        Alarm = 1 << 4,

        /// <summary>bit5：故障（不可运行，需要复位）。</summary>
        Fault = 1 << 5,

        /// <summary>bit6：已到位（就绪且满足到位判据）。</summary>
        InPosition = 1 << 6,

        /// <summary>bit7：回零完成（坐标基准已建立）。</summary>
        HomeCompleted = 1 << 7,

        /// <summary>bit8：命令执行中（Busy）。</summary>
        CommandBusy = 1 << 8,

        /// <summary>bit9：命令完成（Done）。</summary>
        CommandDone = 1 << 9,

        /// <summary>bit10：命令被新命令中断（CommandAborted）。</summary>
        CommandAborted = 1 << 10,

        /// <summary>bit11：命令出错（Error）。</summary>
        CommandError = 1 << 11
    }
}
