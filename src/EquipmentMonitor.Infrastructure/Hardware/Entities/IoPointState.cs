namespace EquipmentMonitor.Infrastructure
{
    /// <summary>
    /// 单个 IO 点的一次性状态快照。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 对应 <see cref="IIoController.ReadIn"/>（<see cref="IoPointKind.Input"/>）、
    /// <see cref="IIoController.ReadOut"/>（<see cref="IoPointKind.Output"/> 回读）与
    /// <see cref="IIoController.SetOut"/>（<see cref="IoPointKind.Output"/> 写入）。
    /// </para>
    /// <para>
    /// 不可变；每次采集/操作后构造新实例，避免跨线程共享可变状态。
    /// </para>
    /// </remarks>
    public sealed record IoPointState
    {
        /// <summary>控制器（板卡）编号，对应接口的 <c>ControllerNo</c> 参数。</summary>
        public required int ControllerNo { get; init; }

        /// <summary>IO 点编号，对应接口的 <c>ioNo</c> 参数。</summary>
        public required int PointNo { get; init; }

        /// <summary>点类型：输入或输出。</summary>
        public required IoPointKind Type { get; init; }

        /// <summary>点的通断状态：<c>true</c> = ON，<c>false</c> = OFF。</summary>
        public required bool IsOn { get; init; }

        /// <inheritdoc />
        public override string ToString()
            => $"IoPointState(Ctrl={ControllerNo}, Point={PointNo}, Type={Type}, IsOn={IsOn})";
    }
}
