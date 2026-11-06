using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Infrastructure.Axis;

/// <summary>
/// 异步轴控制器契约，供“通讯本身是异步”的控制器使用（典型是 Modbus）。
/// </summary>
/// <remarks>
/// <para>
/// <b>本接口是 Infrastructure 内部实现细节，不是轴控入口。</b>
/// 对外只暴露同步 <see cref="IAxisController"/>；异步控制器由
/// <c>AsyncAxisControllerAdapter</c> 适配后再交给 <c>AxisElement</c>。
/// </para>
/// <para>
/// <b>为什么单独定义，而不是把 <see cref="IAxisController"/> 改成异步：</b>
/// 雷赛/固高的厂商库是同步 API，强制异步会逼实现层到处 <c>Task.FromResult</c>；
/// Modbus 的寄存器读写本身是异步，强制同步则要阻塞等待。
/// 两个契约各自贴合实现，交接处用一个明确的适配桥转换，阻塞只发生在桥内。
/// </para>
/// <para>
/// 所有方法都返回 <see cref="Result"/>，失败原因（离线、超时、从站异常）不会像同步
/// <c>bool</c> 那样丢失。
/// </para>
/// </remarks>
internal interface IAsyncAxisController
{
    /// <summary>控制器是否已初始化。</summary>
    bool IsInitialize { get; }

    /// <summary>控制器总数。</summary>
    int ControllerCount { get; }

    /// <summary>轴总数。</summary>
    int AxisCount { get; }

    /// <summary>初始化控制器/连接。</summary>
    Task<Result> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>使能轴。</summary>
    Task<Result> ServeOnAsync(int controllerNo, int axisNo, CancellationToken cancellationToken = default);

    /// <summary>下使能轴。</summary>
    Task<Result> ServeOffAsync(int controllerNo, int axisNo, CancellationToken cancellationToken = default);

    /// <summary>回零。</summary>
    Task<Result> HomeAsync(int controllerNo, int axisNo, CancellationToken cancellationToken = default);

    /// <summary>绝对定位。</summary>
    Task<Result> MoveAbsAsync(
        int controllerNo,
        int axisNo,
        double position,
        CancellationToken cancellationToken = default);

    /// <summary>相对定位。</summary>
    Task<Result> MoveRelAsync(
        int controllerNo,
        int axisNo,
        double distance,
        CancellationToken cancellationToken = default);

    /// <summary>按当前目标位置定位。</summary>
    Task<Result> MoveInPosAsync(int controllerNo, int axisNo, CancellationToken cancellationToken = default);

    /// <summary>点动；<paramref name="direction"/> 为 <c>true</c> 表示正方向。</summary>
    Task<Result> JogAsync(
        int controllerNo,
        int axisNo,
        bool direction,
        CancellationToken cancellationToken = default);

    /// <summary>停止。</summary>
    Task<Result> StopAsync(int controllerNo, int axisNo, CancellationToken cancellationToken = default);

    /// <summary>复位单轴故障。</summary>
    Task<Result> ResetAxisAsync(int controllerNo, int axisNo, CancellationToken cancellationToken = default);

    /// <summary>复位全部轴。</summary>
    Task<Result> ResetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>读取一次完整反馈（位置/速度/扭矩/状态字/故障码）。</summary>
    Task<Result<AxisFeedback>> ReadFeedbackAsync(
        int controllerNo,
        int axisNo,
        CancellationToken cancellationToken = default);
}
