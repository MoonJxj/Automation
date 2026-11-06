using SqlSugar;
using System;

namespace EquipmentMonitor.Infrastructure
{
    /// <summary>
    /// 轴启动配置：一根轴的身份、所属控制器、软限位与默认速度曲线。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ControllerConfigId"/> 引用 <see cref="ControllerConfig.Id"/>；
    /// 启动加载器按控制器分组，把配置交给对应的 <see cref="IAxisController"/> 实现。
    /// </para>
    /// <para>
    /// <b>缩放字段</b>：工程值 = 原始值 × Scale。位置、速度、加速度、减速度、加加速度
    /// 可能使用不同量纲，因此各自独立配置；默认 1.0 表示不做换算。
    /// 原始值既可能是编码器计数，也可能是厂商库的脉冲/单位值，由控制器实现解释。
    /// </para>
    /// </remarks>
    [SugarTable("AxisConfig")]
    public class AxisConfig
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>所属控制器配置的 <see cref="ControllerConfig.Id"/>。</summary>
        [SugarColumn(IsNullable = false, UniqueGroupNameList = new[] { "ControllerAxis" })]
        public int ControllerConfigId { get; set; }

        /// <summary>轴编号，即 Hardware 接口中的 <c>axisNo</c>；同一控制器内唯一。</summary>
        [SugarColumn(IsNullable = false, UniqueGroupNameList = new[] { "ControllerAxis" })]
        public int AxisNo { get; set; }

        /// <summary>所属工位编号（骨架阶段固定 1/2）。</summary>
        [SugarColumn(IsNullable = false)]
        public int StationId { get; set; } = 1;

        /// <summary>
        /// Modbus 状态区起始地址（零基）；仅 Modbus 轴使用。
        /// <c>null</c> 表示该轴没有配置 Modbus 地址，装配时会拒绝生成 Modbus 映射。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public ushort? StateStartAddress { get; set; }

        /// <summary>Modbus 命令区起始地址（零基）；仅 Modbus 轴使用。</summary>
        [SugarColumn(IsNullable = true)]
        public ushort? CommandStartAddress { get; set; }

        /// <summary>Modbus Execute 线圈地址（零基）；仅 Modbus 轴使用。</summary>
        [SugarColumn(IsNullable = true)]
        public ushort? ExecuteCoilAddress { get; set; }

        /// <summary>轴名称（如“X 轴”“取料 Z 轴”）。</summary>
        [SugarColumn(IsNullable = false)]
        public string? Name { get; set; }

        /// <summary>工程单位（<c>mm</c>、<c>deg</c> 等）。</summary>
        [SugarColumn(IsNullable = false)]
        public string? Unit { get; set; } = "mm";

        /// <summary>位置缩放：工程单位 = 原始位置 × 本值。</summary>
        [SugarColumn(IsNullable = false)]
        public double PositionScale { get; set; } = 1.0;

        /// <summary>速度缩放：工程单位/秒 = 原始速度 × 本值。</summary>
        [SugarColumn(IsNullable = false)]
        public double VelocityScale { get; set; } = 1.0;

        /// <summary>扭矩缩放：工程单位 = 原始扭矩 × 本值。</summary>
        [SugarColumn(IsNullable = false)]
        public double TorqueScale { get; set; } = 1.0;

        /// <summary>加速度缩放：工程单位/秒² = 原始加速度 × 本值。</summary>
        [SugarColumn(IsNullable = false)]
        public double AccelerationScale { get; set; } = 1.0;

        /// <summary>减速度缩放：工程单位/秒² = 原始减速度 × 本值。</summary>
        [SugarColumn(IsNullable = false)]
        public double DecelerationScale { get; set; } = 1.0;

        /// <summary>加加速度缩放：工程单位/秒³ = 原始加加速度 × 本值。</summary>
        [SugarColumn(IsNullable = false)]
        public double JerkScale { get; set; } = 1.0;

        /// <summary>软限位下限（工程单位）；<c>null</c> 表示不启用。</summary>
        [SugarColumn(IsNullable = true)]
        public double? SoftLimitMin { get; set; }

        /// <summary>软限位上限（工程单位）；<c>null</c> 表示不启用。</summary>
        [SugarColumn(IsNullable = true)]
        public double? SoftLimitMax { get; set; }

        /// <summary>允许的最大速度（工程单位/秒）；<c>null</c> 表示未配置，由加载器校验。</summary>
        [SugarColumn(IsNullable = true)]
        public double? MaxVelocity { get; set; }

        /// <summary>未显式指定速度时使用的默认速度（工程单位/秒）。</summary>
        [SugarColumn(IsNullable = true)]
        public double? DefaultVelocity { get; set; }

        /// <summary>默认加速度（工程单位/秒²）。</summary>
        [SugarColumn(IsNullable = true)]
        public double? Acceleration { get; set; }

        /// <summary>默认减速度（工程单位/秒²）。</summary>
        [SugarColumn(IsNullable = true)]
        public double? Deceleration { get; set; }

        /// <summary>默认加加速度（工程单位/秒³）；<c>0</c> 表示不使用 S 曲线。</summary>
        [SugarColumn(IsNullable = false)]
        public double Jerk { get; set; }

        /// <summary>回零方式，对应厂商库/PLCopen <c>HomingMethod</c> 的取值；<c>0</c> 表示使用实现默认值。</summary>
        [SugarColumn(IsNullable = false)]
        public int HomeMethod { get; set; }

        /// <summary>是否在启动时启用该轴。禁用后不参与初始化与轮询。</summary>
        [SugarColumn(IsNullable = false)]
        public bool IsEnabled { get; set; } = true;

        /// <summary>备注（机械行程、原点位置说明等）。</summary>
        [SugarColumn(IsNullable = true)]
        public string? Description { get; set; }

        [SugarColumn(IsNullable = false)]
        public DateTime CreateTime { get; set; }

        [SugarColumn(IsNullable = false)]
        public DateTime LatestChangeTime { get; set; }
    }
}
