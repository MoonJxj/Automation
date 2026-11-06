namespace EquipmentMonitor.Core
{
    /// <summary>
    /// 轴状态快照：逐字段对齐 PLC 轴控项目（<c>0004.PLC</c>）的 <c>ST_AxisStatus</c>（决策 D6）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 字段对应：<see cref="ActualPosition"/>→<c>ActualPos</c>、<see cref="TargetPosition"/>→<c>TargetPos</c>、
    /// <see cref="IsMoving"/>→<c>IsMoving</c>、<see cref="InPosition"/>→<c>InPosition</c>、
    /// <see cref="IsEnabled"/>→<c>IsEnabled</c>、<see cref="IsFault"/>→<c>IsFault</c>、
    /// <see cref="FaultCode"/>→<c>FaultCode</c>。
    /// 轴标识（<c>AxisID</c>）不在此列 —— 元件自身有 <c>ElementId</c>，再存一份会变成两个真相源。
    /// </para>
    /// <para>
    /// <b>不可变 + 整体替换</b>：元件在状态变化时构造新实例整体替换引用，
    /// 读取方无锁拿到自洽快照（与 <see cref="ElementSnapshot"/> 同一模式）。
    /// 时间不在快照里 —— 以 <c>IElement.LastUpdatedAt</c> 为准。
    /// </para>
    /// <para>
    /// <see cref="InPosition"/> 由状态机推导（就绪 + 到位判据），不是单纯回读某个位 ——
    /// "使能了但没到位"与"到位了"是两种状态，只报使能位的界面会误导操作工。
    /// </para>
    /// </remarks>
    public sealed record AxisStatus
    {
        /// <summary>驱动器是否使能（对应 <c>MC_Power</c> 的期望状态，由回读确认）。</summary>
        public bool IsEnabled { get; init; }

        /// <summary>是否达到上限位。</summary>
        public bool IsUpperLimit { get; init; }

        /// <summary>是否达到原点。</summary>
        public bool IsHome { get; init; }

        /// <summary>是否达到下限位。</summary>
        public bool IsLowerLimit { get; init; }

        /// <summary>是否正在回零。</summary>
        public bool IsHoming { get; init; }

        /// <summary>是否处于静止。</summary>
        public bool IsStandstill { get; init; }

        /// <summary>是否正在运动。</summary>
        public bool IsMoving { get; init; }

        /// <summary>是否故障停止。</summary>
        public bool IsErrorstop { get; init; }

        /// <summary>是否已到位。</summary>
        public bool InPosition { get; init; }

        /// <summary>当前位置（工程单位 mm）。</summary>
        public double ActualPosition { get; init; }

        /// <summary>当前速度（工程单位 mm/s）。</summary>
        public double ActualVelocity { get; init; }

        /// <summary>当前转矩（工程单位 Nm）。</summary>
        public double ActualTorque { get; init; }

        /// <summary>目标位置（工程单位 mm）。无进行中的定位命令时等于当前位置。</summary>
        public double TargetPosition { get; init; }

        /// <summary>是否处于故障（需要 <c>MC_Reset</c>）。</summary>
        public bool IsFault { get; init; }

        /// <summary>
        /// 是否存在报警（状态字 bit4，可继续运行但需要关注）。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="IsFault"/> 是两种语义：报警位亮着不代表不能动，
        /// 但界面与报警服务必须能看到它，否则「可继续运行的报警」会被静默丢弃。
        /// </remarks>
        public bool IsWarning { get; init; }

        /// <summary>故障码。<c>0</c> 表示无故障。</summary>
        public int FaultCode { get; init; }

        public UniaxialState UniaxialState { get; init; } = UniaxialState.Unknown;

        /// <inheritdoc />
        public override string ToString() =>
            $"AxisStatus(Pos={ActualPosition:F3}, Vel={ActualVelocity:F3}, Torque={ActualTorque:F3}, " +
            $"Target={TargetPosition:F3}, " +
            $"Flags=[{GetFlagText()}], " +
            $"Fault={(IsFault ? FaultCode.ToString() : "none")}, " +
            $"UniaxialState={UniaxialState})";

        private string GetFlagText()
        {
            var sb = new System.Text.StringBuilder(16);
            if (IsEnabled) sb.Append("EN ");
            if (IsMoving) sb.Append("MOV ");
            if (InPosition) sb.Append("INP ");
            if (IsStandstill) sb.Append("STD ");
            if (IsHoming) sb.Append("HOM ");
            if (IsHome) sb.Append("HOME ");
            if (IsUpperLimit) sb.Append("UL ");
            if (IsLowerLimit) sb.Append("LL ");
            if (IsErrorstop) sb.Append("ESTOP ");
            return sb.ToString().TrimEnd();
        }
    }
}
