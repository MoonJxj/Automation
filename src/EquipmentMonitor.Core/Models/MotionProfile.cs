namespace EquipmentMonitor.Core.Models
{
    /// <summary>
    /// 控制器侧速度曲线参数：位置、回零、点动类命令共用。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 字段对齐 PLCopen 单轴运动功能块的 Velocity / Acceleration / Deceleration / Jerk，
    /// 与 <c>docs/ChangePlan.md</c> 引用的 <c>ST_ServoTarget</c> 语义一致。
    /// </para>
    /// <para>
    /// 与 <c>EquipmentMonitor.Core.Models.MotionParameters</c>（命令级参数投影，含位置与缓冲策略）
    /// 是两层概念：本类型只承载落到板卡的速度曲线参数，由控制器实现负责换算成厂商单位。
    /// </para>
    /// <para>不可变；配置一次后可被多条命令复用。</para>
    /// </remarks>
    public sealed record MotionProfile
    {
        /// <summary>目标速度（工程单位/秒）。</summary>
        public required double Velocity { get; init; }

        /// <summary>加速度（工程单位/秒²）。</summary>
        public required double Acceleration { get; init; }

        /// <summary>减速度（工程单位/秒²）。</summary>
        public required double Deceleration { get; init; }

        /// <summary>加加速度（工程单位/秒³）；<c>0</c> 表示不使用 S 曲线，按梯形速度规划。</summary>
        public double Jerk { get; init; }

        /// <inheritdoc />
        public override string ToString()
            => $"MotionProfile(Velocity={Velocity}, Accel={Acceleration}, " +
               $"Decel={Deceleration}, Jerk={Jerk})";
    }
}
