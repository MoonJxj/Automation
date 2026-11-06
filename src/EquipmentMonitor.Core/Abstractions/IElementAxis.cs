using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 业务侧的轴契约：命令语义 + 状态快照。
/// </summary>
/// <remarks>
/// <b>命令语义是“已下发”，不是“已完成”。</b>
/// 厂商控制卡与 PLC 的循环扫描逻辑决定了：调用 MoveAbs 只代表命令已写入，
/// 轴是否到位要通过 <see cref="IElement.RefreshAsync"/> 读取状态判断。
/// </remarks>
public interface IElementAxis : IElement
{
    /// <summary>控制器编号。</summary>
    int ControllerNo { get; init; }

    /// <summary>轴编号。</summary>
    int AxisNo { get; init; }

    /// <summary>最近一次刷新的轴状态快照。</summary>
    AxisStatus Status { get; }

    /// <summary>最近一次命令失败的详细信息；无失败时为 <c>null</c>。</summary>
    AxisOperationError? LastError { get; }

    /// <summary>使能（命令下发）。</summary>
    Task<Result> ServeOnAsync(CancellationToken cancellationToken = default);

    /// <summary>下使能（命令下发）。</summary>
    Task<Result> ServeOffAsync(CancellationToken cancellationToken = default);

    /// <summary>回零（命令下发）。</summary>
    Task<Result> HomeAsync(CancellationToken cancellationToken = default);

    /// <summary>绝对定位（命令下发）。</summary>
    Task<Result> MoveAbsAsync(double position, CancellationToken cancellationToken = default);

    /// <summary>相对定位（命令下发）。</summary>
    Task<Result> MoveRelAsync(double offset, CancellationToken cancellationToken = default);

    /// <summary>正向点动（命令下发，持续动作）。</summary>
    Task<Result> ForwardJogAsync(CancellationToken cancellationToken = default);

    /// <summary>反向点动（命令下发，持续动作）。</summary>
    Task<Result> AfterJogAsync(CancellationToken cancellationToken = default);

    /// <summary>停止（命令下发）。</summary>
    Task<Result> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>软件急停（停止 + 下使能；不是安全功能）。</summary>
    Task<Result> EStopAsync(CancellationToken cancellationToken = default);

    /// <summary>复位（命令下发）。</summary>
    new Task<Result> ResetAsync(CancellationToken cancellationToken = default);

    /// <summary>读取厂商状态字或故障码。</summary>
    /// <param name="type">状态字类型，见 <c>AxisStateWordTypes</c>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Result<int>> ReadStateWordAsync(string type, CancellationToken cancellationToken = default);
}
