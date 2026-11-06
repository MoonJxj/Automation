using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Infrastructure.Axis;

/// <summary>
/// 把异步轴控制器（Modbus）适配到同步 <see cref="IAxisController"/> 契约。
/// </summary>
/// <remarks>
/// <para>
/// <b>这是整个框架里唯一允许同步等待异步的地方。</b>
/// <see cref="IAxisController"/> 是厂商同步契约（雷赛/固高），而 Modbus 的寄存器读写是异步的；
/// 为了让轴控上层只认一个同步接口，必须有人做桥接。把 <c>GetAwaiter().GetResult()</c>
/// 关在这个文件里，框架其他部分（<c>AxisElement</c>、加载器、UI）就不需要知道这个边界。
/// </para>
/// <para>
/// <b>线程要求</b>：调用方必须在后台线程/线程池上调用（<c>AxisElement</c> 的命令与刷新满足），
/// <b>禁止从 WinForms UI 线程直接调用</b>，否则 Modbus 通讯超时会把界面卡住。
/// </para>
/// <para>
/// <b>失败语义</b>：异步方法返回失败时，本适配器抛 <see cref="AxisControllerException"/>，
/// 由 <c>AxisElement</c> 的命令异常处理转成带错误信息的 <c>Result.Fail</c>。
/// 同步 <c>bool</c> 无法携带错误码，直接返回 false 会把“离线/超时/从站异常”全部压成“控制器拒绝”。
/// </para>
/// </remarks>
internal sealed class AsyncAxisControllerAdapter : IAxisController
{
    private static readonly TimeSpan FeedbackCacheTtl = TimeSpan.FromMilliseconds(10);

    private readonly IAsyncAxisController _inner;
    private readonly TimeProvider _timeProvider;
    private readonly object _feedbackGate = new();
    private readonly Dictionary<(int ControllerNo, int AxisNo), (DateTimeOffset At, AxisFeedback Value)> _feedbackCache = new();

    /// <summary>创建适配器。</summary>
    /// <param name="inner">异步控制器。</param>
    /// <param name="timeProvider">时间源；测试可注入假时钟。</param>
    public AsyncAxisControllerAdapter(IAsyncAxisController inner, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public bool IsInitialize => _inner.IsInitialize;

    /// <inheritdoc />
    public int ControllerCount => _inner.ControllerCount;

    /// <inheritdoc />
    public int AxisCount => _inner.AxisCount;

    /// <inheritdoc />
    public bool Initialize()
    {
        var result = _inner.InitializeAsync().GetAwaiter().GetResult().IsSuccess;
        if (result)
        {
            InvalidateFeedback();
        }

        return result;
    }

    /// <inheritdoc />
    public void GetAxisCount(int ControllerNo, ref uint axisCount)
    {
        if (ControllerNo == 0)
        {
            axisCount = (uint)_inner.AxisCount;
        }
    }

    /// <inheritdoc />
    public bool ServeOn(int ControllerNo, int axisNo)
        => RunCommand(_inner.ServeOnAsync(ControllerNo, axisNo), axisNo);

    /// <inheritdoc />
    public bool ServeOff(int ControllerNo, int axisNo)
        => RunCommand(_inner.ServeOffAsync(ControllerNo, axisNo), axisNo);

    /// <inheritdoc />
    public bool Home(int ControllerNo, int axisNo)
        => RunCommand(_inner.HomeAsync(ControllerNo, axisNo), axisNo);

    /// <inheritdoc />
    public bool MoveAbs(int ControllerNo, int axisNo, double position)
        => RunCommand(_inner.MoveAbsAsync(ControllerNo, axisNo, position), axisNo);

    /// <inheritdoc />
    public bool MoveRel(int ControllerNo, int axisNo, double distance)
        => RunCommand(_inner.MoveRelAsync(ControllerNo, axisNo, distance), axisNo);

    /// <inheritdoc />
    public bool MoveInPos(int ControllerNo, int axisNo)
        => RunCommand(_inner.MoveInPosAsync(ControllerNo, axisNo), axisNo);

    /// <inheritdoc />
    public bool Jog(int ControllerNo, int axisNo, bool direction)
        => RunCommand(_inner.JogAsync(ControllerNo, axisNo, direction), axisNo);

    /// <inheritdoc />
    public bool Stop(int ControllerNo, int axisNo)
        => RunCommand(_inner.StopAsync(ControllerNo, axisNo), axisNo);

    /// <inheritdoc />
    public bool ResetAxis(int ControllerNo, int axisNo)
        => RunCommand(_inner.ResetAxisAsync(ControllerNo, axisNo), axisNo);

    /// <inheritdoc />
    public bool ResetAll()
    {
        var result = Block(_inner.ResetAllAsync()).IsSuccess;
        InvalidateFeedback();
        return result;
    }

    /// <inheritdoc />
    public double GetPos(int ControllerNo, int axisNo)
        => ReadFeedback(ControllerNo, axisNo).ActualPosition;

    /// <inheritdoc />
    public double GetVel(int ControllerNo, int axisNo)
        => ReadFeedback(ControllerNo, axisNo).ActualVelocity;

    /// <inheritdoc />
    public double GetTorque(int ControllerNo, int axisNo)
        => ReadFeedback(ControllerNo, axisNo).ActualTorque;

    /// <inheritdoc />
    public bool ReadStateWord(int ControllerNo, int axisNo, string type, out int state)
    {
        state = 0;

        if (string.Equals(type, AxisStateWordTypes.FaultCode, StringComparison.Ordinal))
        {
            state = ReadFeedback(ControllerNo, axisNo).FaultCode;
            return true;
        }

        if (string.Equals(type, AxisStateWordTypes.StateWord, StringComparison.Ordinal))
        {
            state = (int)ReadFeedback(ControllerNo, axisNo).StatusWord;
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // 异步控制器由外部持有；适配器不负责释放。
    }

    /// <summary>
    /// 执行命令并在成功后失效该轴的反馈缓存。
    /// </summary>
    private bool RunCommand(Task<Result> task, int axisNo)
    {
        Block(task);
        InvalidateFeedback(axisNo);
        return true;
    }

    /// <summary>
    /// 读取反馈，带 10ms 缓存：<c>AxisElement.Refresh</c> 会连续调用
    /// <c>GetPos/GetVel/GetTorque/ReadStateWord</c>，没有缓存就是 4 次 Modbus 读，
    /// 既慢又可能读到不同时刻的状态。
    /// </summary>
    private AxisFeedback ReadFeedback(int controllerNo, int axisNo)
    {
        var key = (controllerNo, axisNo);
        var now = _timeProvider.GetUtcNow();

        lock (_feedbackGate)
        {
            if (_feedbackCache.TryGetValue(key, out var cached) &&
                now - cached.At < FeedbackCacheTtl)
            {
                return cached.Value;
            }
        }

        var feedback = Block(_inner.ReadFeedbackAsync(controllerNo, axisNo));

        lock (_feedbackGate)
        {
            _feedbackCache[key] = (now, feedback);
        }

        return feedback;
    }

    private void InvalidateFeedback(int? axisNo = null)
    {
        lock (_feedbackGate)
        {
            if (axisNo is null)
            {
                _feedbackCache.Clear();
                return;
            }

            foreach (var key in _feedbackCache.Keys.Where(k => k.AxisNo == axisNo.Value).ToArray())
            {
                _feedbackCache.Remove(key);
            }
        }
    }

    /// <summary>
    /// 同步等待异步结果；失败时抛出带错误信息的异常，避免同步 <c>bool</c> 丢失原因。
    /// </summary>
    private static Result Block(Task<Result> task)
    {
        var result = task.GetAwaiter().GetResult();
        if (result.IsFailure)
        {
            throw new AxisControllerException(
                result.Error ?? "控制器调用失败。",
                result.ErrorCode,
                result.Exception);
        }

        return result;
    }

    private static AxisFeedback Block(Task<Result<AxisFeedback>> task)
    {
        var result = task.GetAwaiter().GetResult();
        if (result.IsFailure)
        {
            throw new AxisControllerException(
                result.Error ?? "控制器调用失败。",
                result.ErrorCode,
                result.Exception);
        }

        return result.Value;
    }
}
