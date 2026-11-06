using SqlSugar;
using System;

namespace EquipmentMonitor.Infrastructure
{
    /// <summary>
    /// 设备启动配置：挂在控制器下的 PLC 伺服、气缸、传感器等逻辑设备。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ControllerConfigId"/> 引用 <see cref="ControllerConfig.Id"/>。
    /// <see cref="DeviceNo"/> 对应 <see cref="IDeviceController"/> 各方法中的 <c>ControllerNo</c> 参数
    /// （该接口用它表示设备序号）。
    /// </para>
    /// <para>
    /// 设备的具体地址仍由轴配置、IO 点配置或寄存器点配置表达；
    /// 本表只负责“有哪些设备、叫什么、属于哪类、是否启用”。
    /// </para>
    /// </remarks>
    [SugarTable("DeviceConfig")]
    public class DeviceConfig
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>所属控制器配置的 <see cref="ControllerConfig.Id"/>。</summary>
        [SugarColumn(IsNullable = false, UniqueGroupNameList = new[] { "ControllerDevice" })]
        public int ControllerConfigId { get; set; }

        /// <summary>设备序号，对应 <see cref="IDeviceController"/> 的 <c>ControllerNo</c> 参数；同一控制器内唯一。</summary>
        [SugarColumn(IsNullable = false, UniqueGroupNameList = new[] { "ControllerDevice" })]
        public int DeviceNo { get; set; }

        /// <summary>设备名称（如“上料伺服”“夹紧气缸”）。</summary>
        [SugarColumn(IsNullable = false)]
        public string? Name { get; set; }

        /// <summary>
        /// 设备类型标识，与 <see cref="IDeviceController.SetControlWord"/> /
        /// <see cref="IDeviceController.ReadStateWord"/> 的 <c>type</c> 参数一致
        /// （如 <c>Servo</c>、<c>Cylinder</c>）。
        /// </summary>
        [SugarColumn(IsNullable = false)]
        public string? DeviceType { get; set; }

        /// <summary>是否在启动时启用该设备。禁用后不参与初始化与轮询。</summary>
        [SugarColumn(IsNullable = false)]
        public bool IsEnabled { get; set; } = true;

        /// <summary>备注（工位、功能说明等）。</summary>
        [SugarColumn(IsNullable = true)]
        public string? Description { get; set; }

        [SugarColumn(IsNullable = false)]
        public DateTime CreateTime { get; set; }

        [SugarColumn(IsNullable = false)]
        public DateTime LatestChangeTime { get; set; }
    }
}
