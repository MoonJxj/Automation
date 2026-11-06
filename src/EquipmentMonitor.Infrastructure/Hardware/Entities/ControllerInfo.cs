namespace EquipmentMonitor.Infrastructure
{
    /// <summary>
    /// 控制器（板卡）的身份与容量快照。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 字段来源：<see cref="IControllerBase.ControllerCount"/>、<see cref="IControllerBase.IsInitialize"/>、
    /// <see cref="IAxisController.AxisCount"/>、<see cref="IIoController.InputCount"/> 与
    /// <see cref="IIoController.OutputCount"/>。
    /// </para>
    /// <para>
    /// <see cref="Brand"/> 与 <see cref="IControllerFactory.Create{T}"/> 使用的品牌键一致
    /// （当前为 <c>GoogolTech</c>、<c>LeadShine</c>、<c>Modbus</c>）；
    /// 某控制器不支持的类别以 <c>0</c> 表示。
    /// </para>
    /// </remarks>
    public sealed record ControllerInfo
    {
        /// <summary>控制器（板卡）编号；单卡系统为 <c>0</c>。</summary>
        public required int ControllerNo { get; init; }

        /// <summary>品牌键，与工厂的 keyed DI 键一致。</summary>
        public required string Brand { get; init; }

        /// <summary>控制器总数。</summary>
        public required int ControllerCount { get; init; }

        /// <summary>轴总数；不支持轴的控制器为 <c>0</c>。</summary>
        public required int AxisCount { get; init; }

        /// <summary>输入点总数；不支持 IO 的控制器为 <c>0</c>。</summary>
        public required int InputCount { get; init; }

        /// <summary>输出点总数；不支持 IO 的控制器为 <c>0</c>。</summary>
        public required int OutputCount { get; init; }

        /// <summary>控制器是否已成功初始化。</summary>
        public required bool IsInitialized { get; init; }

        /// <inheritdoc />
        public override string ToString()
            => $"ControllerInfo(Brand={Brand}, No={ControllerNo}/{ControllerCount}, " +
               $"Axes={AxisCount}, In={InputCount}, Out={OutputCount}, Initialized={IsInitialized})";
    }
}
