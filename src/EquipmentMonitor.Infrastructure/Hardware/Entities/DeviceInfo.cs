namespace EquipmentMonitor.Infrastructure
{
    /// <summary>
    /// 设备身份快照（PLC 伺服、气缸等），对应 <see cref="IDeviceController"/> 的名称与类型读取。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 字段来源：<see cref="IDeviceController.GetDeviceName"/>、
    /// <see cref="IDeviceController.GetDeviceType"/>。
    /// </para>
    /// <para>
    /// <see cref="Type"/> 是设备类别（如 <c>Servo</c>、<c>Cylinder</c>）的字符串标识，
    /// 与 <see cref="IDeviceController.SetControlWord"/>、<see cref="IDeviceController.ReadStateWord"/>
    /// 的 <c>type</c> 参数使用同一套标识；具体取值由设备实现定义。
    /// </para>
    /// </remarks>
    public sealed record DeviceInfo
    {
        /// <summary>控制器编号，对应接口的 <c>ControllerNo</c> 参数。</summary>
        public required int ControllerNo { get; init; }

        /// <summary>设备名称；读取失败时为空字符串。</summary>
        public required string Name { get; init; }

        /// <summary>设备类型标识；读取失败时为空字符串。</summary>
        public required string Type { get; init; }

        /// <inheritdoc />
        public override string ToString()
            => $"DeviceInfo(Ctrl={ControllerNo}, Name={Name}, Type={Type})";
    }
}
