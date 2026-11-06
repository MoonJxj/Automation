using SqlSugar;
using System;

namespace EquipmentMonitor.Infrastructure
{
    /// <summary>
    /// 控制器（运动控制卡 / PLC）启动配置。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 一行对应一个启动时要初始化的控制器。品牌键必须与
    /// <see cref="InfrastructureModuleRegister"/> 中 keyed DI 注册的键一致
    /// （当前为 <c>GoogolTech</c>、<c>LeadShine</c>、<c>Modbus</c>）。
    /// </para>
    /// <para>
    /// 连接字段按 <see cref="ConnectionType"/> 择一使用，未使用的字段保留默认值：
    /// PCI 卡只需 <see cref="Brand"/> + <see cref="ControllerNo"/>；
    /// Modbus TCP 使用 <see cref="IpAddress"/> / <see cref="Port"/> / <see cref="SlaveAddress"/>；
    /// Modbus RTU 使用串口字段 / <see cref="SlaveAddress"/>。
    /// </para>
    /// </remarks>
    [SugarTable("ControllerConfig")]
    public class ControllerConfig
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>配置名称，在全部控制器中唯一（如“左工位运动卡”）。</summary>
        [SugarColumn(IsNullable = false, UniqueGroupNameList = new[] { "ControllerName" })]
        public string? Name { get; set; }

        /// <summary>品牌键，决定使用哪个工厂与控制器实现。</summary>
        [SugarColumn(IsNullable = false, UniqueGroupNameList = new[] { "BrandControllerNo" })]
        public string? Brand { get; set; }

        /// <summary>控制器编号，即 Hardware 接口中的 <c>ControllerNo</c> / <c>cardNo</c>；与品牌一起唯一。</summary>
        [SugarColumn(IsNullable = false, UniqueGroupNameList = new[] { "BrandControllerNo" })]
        public int ControllerNo { get; set; }

        /// <summary>连接方式，决定后续连接字段中哪些生效。</summary>
        [SugarColumn(IsNullable = false)]
        public ControllerConnectionType ConnectionType { get; set; } = ControllerConnectionType.PciCard;

        /// <summary>Modbus TCP 主机地址；仅 <see cref="ControllerConnectionType.ModbusTcp"/> 使用。</summary>
        [SugarColumn(IsNullable = true)]
        public string? IpAddress { get; set; }

        /// <summary>Modbus TCP 端口；仅 <see cref="ControllerConnectionType.ModbusTcp"/> 使用。</summary>
        [SugarColumn(IsNullable = false)]
        public int Port { get; set; } = 502;

        /// <summary>串口名称（如 <c>COM1</c>）；仅 <see cref="ControllerConnectionType.ModbusRtu"/> 使用。</summary>
        [SugarColumn(IsNullable = true)]
        public string? PortName { get; set; }

        /// <summary>串口波特率；仅 Modbus RTU 使用。</summary>
        [SugarColumn(IsNullable = false)]
        public int BaudRate { get; set; } = 9600;

        /// <summary>串口校验方式；仅 Modbus RTU 使用，取值与 <see cref="System.IO.Ports.Parity"/> 一致。</summary>
        [SugarColumn(IsNullable = false)]
        public ControllerSerialParity Parity { get; set; } = ControllerSerialParity.None;

        /// <summary>串口数据位；仅 Modbus RTU 使用。</summary>
        [SugarColumn(IsNullable = false)]
        public int DataBits { get; set; } = 8;

        /// <summary>串口停止位；仅 Modbus RTU 使用，取值与 <see cref="System.IO.Ports.StopBits"/> 一致。</summary>
        [SugarColumn(IsNullable = false)]
        public ControllerSerialStopBits StopBits { get; set; } = ControllerSerialStopBits.One;

        /// <summary>Modbus 从站地址（Unit ID）；TCP 与 RTU 均使用。</summary>
        [SugarColumn(IsNullable = false)]
        public int SlaveAddress { get; set; } = 1;

        /// <summary>单次通讯超时（毫秒）。</summary>
        [SugarColumn(IsNullable = false)]
        public int TimeoutMs { get; set; } = 1000;

        /// <summary>初始化顺序，数值小的先初始化；同一顺序时按 <see cref="Id"/> 升序。</summary>
        [SugarColumn(IsNullable = false)]
        public int SortOrder { get; set; }

        /// <summary>是否在启动时初始化该控制器。禁用后其轴/IO/设备配置不会生效。</summary>
        [SugarColumn(IsNullable = false)]
        public bool IsEnabled { get; set; } = true;

        /// <summary>备注（现场位置、接线说明等）。</summary>
        [SugarColumn(IsNullable = true)]
        public string? Description { get; set; }

        [SugarColumn(IsNullable = false)]
        public DateTime CreateTime { get; set; }

        [SugarColumn(IsNullable = false)]
        public DateTime LatestChangeTime { get; set; }
    }

    /// <summary>
    /// 控制器连接方式。
    /// </summary>
    public enum ControllerConnectionType
    {
        /// <summary>PCI / PCIe 板卡（GoogolTech、LeadShine 等）。</summary>
        PciCard = 0,

        /// <summary>Modbus TCP。</summary>
        ModbusTcp = 1,

        /// <summary>Modbus RTU（串口）。</summary>
        ModbusRtu = 2
    }

    /// <summary>
    /// 串口校验方式；数值与 <see cref="System.IO.Ports.Parity"/>、Core 的 <c>SerialParity</c> 一致。
    /// </summary>
    /// <remarks>
    /// 前缀 <c>Controller</c> 是为了避免与 <c>EquipmentMonitor.Core.Models.SerialParity</c> 同名，
    /// 便于将来两个命名空间同时被引用时仍能无歧义地书写。
    /// </remarks>
    public enum ControllerSerialParity
    {
        None = 0,
        Odd = 1,
        Even = 2,
        Mark = 3,
        Space = 4
    }

    /// <summary>
    /// 串口停止位；数值与 <see cref="System.IO.Ports.StopBits"/>、Core 的 <c>SerialStopBits</c> 一致。
    /// </summary>
    /// <remarks>
    /// 刻意不提供 <c>None</c>：停止位为 0 不是有效的串口配置，
    /// 与 Core 的定义保持一致。
    /// </remarks>
    public enum ControllerSerialStopBits
    {
        One = 1,
        Two = 2,
        OnePointFive = 3
    }
}
