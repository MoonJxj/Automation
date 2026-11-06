using SqlSugar;
using System;

namespace EquipmentMonitor.Infrastructure
{
    /// <summary>
    /// IO 点启动配置：输入/输出点的身份、所属控制器与电平约定。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ControllerConfigId"/> 引用 <see cref="ControllerConfig.Id"/>；
    /// 启动加载器按控制器分组，把配置交给对应的 <see cref="IIoController"/> 实现。
    /// </para>
    /// <para>
    /// 输入点与输出点编号各自独立，因此唯一键包含 <see cref="Kind"/>。
    /// </para>
    /// </remarks>
    [SugarTable("IoPointConfig")]
    public class IoPointConfig
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>所属控制器配置的 <see cref="ControllerConfig.Id"/>。</summary>
        [SugarColumn(IsNullable = false, UniqueGroupNameList = new[] { "ControllerIoPoint" })]
        public int ControllerConfigId { get; set; }

        /// <summary>IO 点编号，即 Hardware 接口中的 <c>ioNo</c>。</summary>
        [SugarColumn(IsNullable = false, UniqueGroupNameList = new[] { "ControllerIoPoint" })]
        public int PointNo { get; set; }

        /// <summary>输入点或输出点；与控制器编号、点编号一起唯一。</summary>
        [SugarColumn(IsNullable = false, UniqueGroupNameList = new[] { "ControllerIoPoint" })]
        public IoPointKind Kind { get; set; }

        /// <summary>点名称（如“上料气缸到位”“真空吸盘”）。</summary>
        [SugarColumn(IsNullable = false)]
        public string? Name { get; set; }

        /// <summary>
        /// 是否反转逻辑电平：<c>true</c> 时电气低电平表示 ON。
        /// 用于 NPN/PNP 差异或常闭（NC）传感器；实现层对原始读数做异或，不改变硬件接线。
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public bool IsInverted { get; set; }

        /// <summary>
        /// 输出点的启动初始状态；<c>null</c> 表示保持控制器上电后的原状。
        /// 输入点忽略本字段。安全相关的输出建议显式配置。
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public bool? InitialState { get; set; }

        /// <summary>是否在启动时启用该点。禁用后不参与初始化、轮询或操作。</summary>
        [SugarColumn(IsNullable = false)]
        public bool IsEnabled { get; set; } = true;

        /// <summary>备注（接线端子、功能说明等）。</summary>
        [SugarColumn(IsNullable = true)]
        public string? Description { get; set; }

        [SugarColumn(IsNullable = false)]
        public DateTime CreateTime { get; set; }

        [SugarColumn(IsNullable = false)]
        public DateTime LatestChangeTime { get; set; }
    }
}
