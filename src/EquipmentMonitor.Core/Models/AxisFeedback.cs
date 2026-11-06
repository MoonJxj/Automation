using System;
using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models
{
    /// <summary>
    /// 单个轴的一次性反馈快照，由 <see cref="IAxisController"/> 的读取方法组合而成。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 字段来源：<see cref="IAxisController.GetPos"/>、<see cref="IAxisController.GetVel"/>、
    /// <see cref="IAxisController.GetTorque"/>、<see cref="IAxisController.ReadStateWord"/>。
    /// </para>
    /// <para>
    /// <b>不可变快照</b>：每次轮询构造新实例，跨线程传递时无需加锁；
    /// 时间戳不放在快照里，由采集方记录，避免同一事实出现两个时间源。
    /// </para>
    /// </remarks>
    public sealed record AxisFeedback
    {
        /// <summary>控制器（板卡）编号，对应接口的 <c>ControllerNo</c> 参数。</summary>
        public required int ControllerNo { get; init; }

        /// <summary>轴编号，对应接口的 <c>axisNo</c> 参数。</summary>
        public required int AxisNo { get; init; }

        /// <summary>当前位置（工程单位，通常为 mm）。</summary>
        public required double ActualPosition { get; init; }

        /// <summary>当前速度（工程单位/秒）。</summary>
        public required double ActualVelocity { get; init; }

        /// <summary>当前扭矩（工程单位，具体量纲由控制器配置决定）。</summary>
        public required double ActualTorque { get; init; }

        /// <summary>状态字，位含义见 <see cref="AxisStatusWord"/>。</summary>
        public required AxisStatusWord StatusWord { get; init; }

        /// <summary>故障码；<c>0</c> 表示无故障。</summary>
        public required int FaultCode { get; init; }

        /// <summary>驱动器是否使能。</summary>
        public bool IsEnabled => (StatusWord & AxisStatusWord.Enabled) != 0;

        /// <summary>轴是否正在移动。</summary>
        public bool IsMoving => (StatusWord & AxisStatusWord.Moving) != 0;

        /// <summary>是否正在回零。</summary>
        public bool IsHoming => (StatusWord & AxisStatusWord.Homing) != 0;

        /// <summary>是否正在定位。</summary>
        public bool IsPositioning => (StatusWord & AxisStatusWord.Positioning) != 0;

        /// <summary>是否存在报警（可继续运行）。</summary>
        public bool HasAlarm => (StatusWord & AxisStatusWord.Alarm) != 0;

        /// <summary>是否处于故障（不可运行，需要复位）。</summary>
        public bool IsFault => (StatusWord & AxisStatusWord.Fault) != 0;

        /// <summary>是否已到位。</summary>
        public bool InPosition => (StatusWord & AxisStatusWord.InPosition) != 0;

        /// <summary>是否已完成回零（坐标基准已建立）。</summary>
        public bool IsHomeCompleted => (StatusWord & AxisStatusWord.HomeCompleted) != 0;

        /// <summary>命令是否执行中。</summary>
        public bool IsCommandBusy => (StatusWord & AxisStatusWord.CommandBusy) != 0;

        /// <summary>命令是否已完成。</summary>
        public bool IsCommandDone => (StatusWord & AxisStatusWord.CommandDone) != 0;

        /// <summary>命令是否被新命令中断。</summary>
        public bool IsCommandAborted => (StatusWord & AxisStatusWord.CommandAborted) != 0;

        /// <summary>命令是否出错。</summary>
        public bool IsCommandError => (StatusWord & AxisStatusWord.CommandError) != 0;

        /// <inheritdoc />
        public override string ToString()
            => $"AxisFeedback(Ctrl={ControllerNo}, Axis={AxisNo}, Pos={ActualPosition}, " +
               $"Vel={ActualVelocity}, Torque={ActualTorque}, Status={StatusWord}, Fault={FaultCode})";
    }
}
