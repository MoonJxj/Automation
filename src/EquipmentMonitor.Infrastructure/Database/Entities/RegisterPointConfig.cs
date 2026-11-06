using EquipmentMonitor.Common;
using SqlSugar;
using System;

namespace EquipmentMonitor.Infrastructure
{
    /// <summary>
    /// 保持寄存器点启动配置：Modbus 点表中的一个数值点。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ControllerConfigId"/> 引用 <see cref="ControllerConfig.Id"/>；
    /// <see cref="Address"/> 对应 <see cref="IRegisterController"/> 的 <c>address</c> 参数。
    /// </para>
    /// <para>
    /// <b>工程换算</b>：工程值 = 原始值 × <see cref="Scale"/>。读写两个方向共用同一个系数，
    /// 避免“读用一个、写用另一个”导致现场数值相差倍数。
    /// </para>
    /// </remarks>
    [SugarTable("RegisterPointConfig")]
    public class RegisterPointConfig
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>所属控制器配置的 <see cref="ControllerConfig.Id"/>。</summary>
        [SugarColumn(IsNullable = false, UniqueGroupNameList = new[] { "ControllerRegister" })]
        public int ControllerConfigId { get; set; }

        /// <summary>保持寄存器地址（0–65535）；同一控制器内唯一。</summary>
        [SugarColumn(IsNullable = false, UniqueGroupNameList = new[] { "ControllerRegister" })]
        public int Address { get; set; }

        /// <summary>点名称（如“实际位置”“目标速度”）。</summary>
        [SugarColumn(IsNullable = false)]
        public string? Name { get; set; }

        /// <summary>数值类型，决定占用寄存器数量及解释方式。</summary>
        [SugarColumn(IsNullable = false)]
        public RegisterValueType DataType { get; set; } = RegisterValueType.UInt16;

        /// <summary>读写权限；启动加载器据此拒绝越权写入。</summary>
        [SugarColumn(IsNullable = false)]
        public RegisterAccessMode Access { get; set; } = RegisterAccessMode.ReadWrite;

        /// <summary>32 位量的字序；16 位量忽略本字段。</summary>
        [SugarColumn(IsNullable = false)]
        public RegisterWordOrder WordOrder { get; set; } = RegisterWordOrder.HighWordFirst;

        /// <summary>工程换算系数：工程值 = 原始值 × 本值；默认 1.0。</summary>
        [SugarColumn(IsNullable = false)]
        public double Scale { get; set; } = 1.0;

        /// <summary>工程单位（<c>mm</c>、<c>mm/s</c>、<c>%</c> 等）；纯计数值可留空。</summary>
        [SugarColumn(IsNullable = true)]
        public string? Unit { get; set; }

        /// <summary>是否在启动时启用该点。禁用后不参与轮询或操作。</summary>
        [SugarColumn(IsNullable = false)]
        public bool IsEnabled { get; set; } = true;

        /// <summary>备注（对应 PLC 符号、功能说明等）。</summary>
        [SugarColumn(IsNullable = true)]
        public string? Description { get; set; }

        [SugarColumn(IsNullable = false)]
        public DateTime CreateTime { get; set; }

        [SugarColumn(IsNullable = false)]
        public DateTime LatestChangeTime { get; set; }
    }

    /// <summary>
    /// 保持寄存器数值类型。
    /// </summary>
    public enum RegisterValueType
    {
        /// <summary>有符号 16 位整数（占 1 个寄存器）。</summary>
        Int16 = 0,

        /// <summary>无符号 16 位整数（占 1 个寄存器）。</summary>
        UInt16 = 1,

        /// <summary>有符号 32 位整数（占 2 个寄存器）。</summary>
        Int32 = 2,

        /// <summary>无符号 32 位整数（占 2 个寄存器）。</summary>
        UInt32 = 3,

        /// <summary>32 位浮点数（占 2 个寄存器）。</summary>
        Float32 = 4
    }

    /// <summary>
    /// 寄存器点读写权限。
    /// </summary>
    public enum RegisterAccessMode
    {
        /// <summary>只读，禁止写入。</summary>
        ReadOnly = 0,

        /// <summary>只写，禁止读取。</summary>
        WriteOnly = 1,

        /// <summary>可读可写。</summary>
        ReadWrite = 2
    }
}
