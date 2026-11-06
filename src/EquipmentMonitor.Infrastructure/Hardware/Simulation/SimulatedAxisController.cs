using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Infrastructure;

namespace EquipmentMonitor.Infrastructure.Hardware.Simulation;

/// <summary>
/// 内存中的轴控制器，用于无硬件开发、UI 联调和单元测试。
/// </summary>
/// <remarks>
/// <para>
/// 它不是“假数据生成器”，而是按 <see cref="IAxisController"/> 的语义实现一套可预测的运动：
/// 使能/下使能、回零、绝对/相对定位、点动、停止、复位、状态字读取。
/// 这样 <c>AxisElement</c> 的命令与等待逻辑可以在没有板卡、没有 PLC 的机器上被完整验证。
/// </para>
/// <para>
/// <b>运动时间</b>：默认 <see cref="MotionDuration"/> 为 <see cref="TimeSpan.Zero"/>，
/// 下一次读取状态时立即到位；需要验证超时/等待逻辑时把它设大。
/// 位置推进基于 <see cref="TimeProvider"/>，测试可注入假时钟，不必真实等待。
/// </para>
/// <para>
/// <b>线程安全</b>：所有公开方法在同一把锁内推进模拟并读写状态；
/// 不使用后台线程，避免测试结束时留下未停止的 <see cref="Timer"/>。
/// </para>
/// </remarks>
public sealed class SimulatedAxisController : IAxisController
{
    private readonly object _gate = new();
    private readonly SimulatedAxis[] _axes;
    private readonly TimeProvider _timeProvider;

    private bool _initialized;
    private DateTimeOffset _lastTick;

    /// <summary>
    /// 创建模拟控制器。
    /// </summary>
    /// <param name="axisCount">轴数量，必须大于 0。</param>
    /// <param name="timeProvider">时间源；测试可注入假时钟。</param>
    public SimulatedAxisController(int axisCount = 1, TimeProvider? timeProvider = null)
    {
        if (axisCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(axisCount), "轴数量必须大于 0。");
        }

        _axes = Enumerable.Range(0, axisCount).Select(_ => new SimulatedAxis()).ToArray();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lastTick = _timeProvider.GetUtcNow();
    }

    /// <summary>单次定位/回零的耗时；<see cref="TimeSpan.Zero"/> 表示立即到位。</summary>
    public TimeSpan MotionDuration { get; set; } = TimeSpan.Zero;

    /// <summary>定位速度（工程单位/秒）。</summary>
    public double Speed { get; set; } = 1000.0;

    /// <summary>点动速度（工程单位/秒）。</summary>
    public double JogSpeed { get; set; } = 10.0;

    /// <inheritdoc />
    public bool IsInitialize
    {
        get
        {
            lock (_gate)
            {
                return _initialized;
            }
        }
    }

    /// <inheritdoc />
    public int ControllerCount
    {
        get
        {
            lock (_gate)
            {
                return 1;
            }
        }
    }

    /// <inheritdoc />
    public int AxisCount => _axes.Length;

    /// <inheritdoc />
    public bool Initialize()
    {
        lock (_gate)
        {
            _initialized = true;
            _lastTick = _timeProvider.GetUtcNow();
            return true;
        }
    }

    /// <inheritdoc />
    public void GetAxisCount(int ControllerNo, ref uint axisCount)
    {
        if (ControllerNo != 0)
        {
            return;
        }

        axisCount = (uint)_axes.Length;
    }

    /// <inheritdoc />
    public bool ServeOn(int ControllerNo, int axisNo)
    {
        lock (_gate)
        {
            AdvanceLocked();

            if (!TryGetAxisLocked(ControllerNo, axisNo, out var axis) || axis.Fault)
            {
                return false;
            }

            axis.Enabled = true;
            return true;
        }
    }

    /// <inheritdoc />
    public bool ServeOff(int ControllerNo, int axisNo)
    {
        lock (_gate)
        {
            AdvanceLocked();

            if (!TryGetAxisLocked(ControllerNo, axisNo, out var axis))
            {
                return false;
            }

            axis.Enabled = false;
            StopLocked(axis);
            return true;
        }
    }

    /// <inheritdoc />
    public bool Home(int ControllerNo, int axisNo)
    {
        lock (_gate)
        {
            AdvanceLocked();

            if (!TryGetAxisLocked(ControllerNo, axisNo, out var axis) || !axis.Enabled || axis.Fault)
            {
                return false;
            }

            StartMoveLocked(axis, 0.0, isHoming: true);
            return true;
        }
    }

    /// <inheritdoc />
    public bool MoveAbs(int ControllerNo, int axisNo, double position)
    {
        lock (_gate)
        {
            AdvanceLocked();

            if (!TryGetAxisLocked(ControllerNo, axisNo, out var axis) || !axis.Enabled || axis.Fault)
            {
                return false;
            }

            StartMoveLocked(axis, position, isHoming: false);
            return true;
        }
    }

    /// <inheritdoc />
    public bool MoveRel(int ControllerNo, int axisNo, double distance)
    {
        lock (_gate)
        {
            AdvanceLocked();

            if (!TryGetAxisLocked(ControllerNo, axisNo, out var axis) || !axis.Enabled || axis.Fault)
            {
                return false;
            }

            StartMoveLocked(axis, axis.Position + distance, isHoming: false);
            return true;
        }
    }

    /// <inheritdoc />
    public bool MoveInPos(int ControllerNo, int axisNo)
    {
        lock (_gate)
        {
            AdvanceLocked();

            if (!TryGetAxisLocked(ControllerNo, axisNo, out var axis) || !axis.Enabled || axis.Fault)
            {
                return false;
            }

            StartMoveLocked(axis, axis.TargetPosition, isHoming: false);
            return true;
        }
    }

    /// <inheritdoc />
    public bool Jog(int ControllerNo, int axisNo, bool direction)
    {
        lock (_gate)
        {
            AdvanceLocked();

            if (!TryGetAxisLocked(ControllerNo, axisNo, out var axis) || !axis.Enabled || axis.Fault)
            {
                return false;
            }

            axis.IsJogging = true;
            axis.Moving = true;
            axis.Homing = false;
            axis.InPosition = false;
            axis.CommandDone = false;
            axis.Velocity = direction ? JogSpeed : -JogSpeed;
            return true;
        }
    }

    /// <inheritdoc />
    public bool Stop(int ControllerNo, int axisNo)
    {
        lock (_gate)
        {
            AdvanceLocked();

            if (!TryGetAxisLocked(ControllerNo, axisNo, out var axis))
            {
                return false;
            }

            StopLocked(axis);
            return true;
        }
    }

    /// <inheritdoc />
    public bool ResetAxis(int ControllerNo, int axisNo)
    {
        lock (_gate)
        {
            AdvanceLocked();

            if (!TryGetAxisLocked(ControllerNo, axisNo, out var axis))
            {
                return false;
            }

            axis.Fault = false;
            axis.FaultCode = 0;
            StopLocked(axis);
            return true;
        }
    }

    /// <inheritdoc />
    public bool ResetAll()
    {
        lock (_gate)
        {
            AdvanceLocked();

            foreach (var axis in _axes)
            {
                axis.Fault = false;
                axis.FaultCode = 0;
                StopLocked(axis);
            }

            return true;
        }
    }

    /// <inheritdoc />
    public double GetPos(int ControllerNo, int axisNo)
    {
        lock (_gate)
        {
            AdvanceLocked();
            return TryGetAxisLocked(ControllerNo, axisNo, out var axis) ? axis.Position : 0.0;
        }
    }

    /// <inheritdoc />
    public double GetVel(int ControllerNo, int axisNo)
    {
        lock (_gate)
        {
            AdvanceLocked();
            return TryGetAxisLocked(ControllerNo, axisNo, out var axis) ? axis.Velocity : 0.0;
        }
    }

    /// <inheritdoc />
    public double GetTorque(int ControllerNo, int axisNo)
    {
        lock (_gate)
        {
            AdvanceLocked();
            return TryGetAxisLocked(ControllerNo, axisNo, out var axis) ? axis.Torque : 0.0;
        }
    }

    /// <inheritdoc />
    public bool ReadStateWord(int ControllerNo, int axisNo, string type, out int state)
    {
        lock (_gate)
        {
            AdvanceLocked();

            state = 0;
            if (!TryGetAxisLocked(ControllerNo, axisNo, out var axis))
            {
                return false;
            }

            if (string.Equals(type, AxisStateWordTypes.FaultCode, StringComparison.Ordinal))
            {
                state = axis.FaultCode;
                return true;
            }

            if (!string.Equals(type, AxisStateWordTypes.StateWord, StringComparison.Ordinal))
            {
                return false;
            }

            state = (int)BuildStatusWord(axis);
            return true;
        }
    }

    /// <summary>
    /// 测试辅助：直接设置某轴的位置，不触发生成运动。
    /// </summary>
    public void SetPosition(int axisNo, double position)
    {
        lock (_gate)
        {
            if (!TryGetAxisLocked(0, axisNo, out var axis))
            {
                throw new ArgumentOutOfRangeException(nameof(axisNo), axisNo, "轴编号越界。");
            }

            AdvanceLocked();
            axis.Position = position;
            axis.TargetPosition = position;
            axis.InPosition = true;
        }
    }

    /// <summary>
    /// 测试辅助：让某轴进入故障，用于验证复位命令。
    /// </summary>
    public void SetFault(int axisNo, int faultCode = 1)
    {
        lock (_gate)
        {
            if (!TryGetAxisLocked(0, axisNo, out var axis))
            {
                throw new ArgumentOutOfRangeException(nameof(axisNo), axisNo, "轴编号越界。");
            }

            AdvanceLocked();
            axis.Fault = true;
            axis.FaultCode = faultCode;
            axis.Enabled = false;
            StopLocked(axis);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _initialized = false;
            foreach (var axis in _axes)
            {
                StopLocked(axis);
            }
        }
    }

    private void StartMoveLocked(SimulatedAxis axis, double targetPosition, bool isHoming)
    {
        axis.MoveStartPosition = axis.Position;
        axis.TargetPosition = targetPosition;
        axis.MotionStartedAt = _timeProvider.GetUtcNow();
        axis.Moving = true;
        axis.IsJogging = false;
        axis.Homing = isHoming;
        axis.InPosition = false;
        axis.CommandDone = false;
        axis.Velocity = MotionDuration > TimeSpan.Zero
            ? (targetPosition - axis.Position) / MotionDuration.TotalSeconds
            : Math.Sign(targetPosition - axis.Position) * Speed;
    }

    private static void StopLocked(SimulatedAxis axis)
    {
        axis.Moving = false;
        axis.IsJogging = false;
        axis.Homing = false;
        axis.Velocity = 0.0;
        axis.InPosition = false;
        axis.CommandDone = false;
    }

    private void AdvanceLocked()
    {
        var now = _timeProvider.GetUtcNow();
        var elapsed = now - _lastTick;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        _lastTick = now;

        foreach (var axis in _axes)
        {
            if (axis.IsJogging)
            {
                axis.Position += axis.Velocity * elapsed.TotalSeconds;
                axis.InPosition = false;
                continue;
            }

            if (!axis.Moving)
            {
                continue;
            }

            if (axis.MotionStartedAt is null)
            {
                axis.MotionStartedAt = now;
            }

            if (MotionDuration <= TimeSpan.Zero)
            {
                CompleteMoveLocked(axis);
                continue;
            }

            var progress = (now - axis.MotionStartedAt.Value).TotalSeconds / MotionDuration.TotalSeconds;
            if (progress >= 1.0)
            {
                CompleteMoveLocked(axis);
                continue;
            }

            axis.Position = axis.MoveStartPosition + (axis.TargetPosition - axis.MoveStartPosition) * progress;
        }
    }

    private static void CompleteMoveLocked(SimulatedAxis axis)
    {
        axis.Position = axis.TargetPosition;
        axis.Velocity = 0.0;
        axis.Moving = false;
        axis.InPosition = true;
        axis.CommandDone = true;

        if (axis.Homing)
        {
            axis.Homing = false;
            axis.HomeCompleted = true;
        }
    }

    private bool TryGetAxisLocked(int controllerNo, int axisNo, out SimulatedAxis axis)
    {
        if (controllerNo == 0 && axisNo >= 0 && axisNo < _axes.Length)
        {
            axis = _axes[axisNo];
            return true;
        }

        axis = null!;
        return false;
    }

    private static AxisStatusWord BuildStatusWord(SimulatedAxis axis)
    {
        var flags = AxisStatusWord.None;

        if (axis.Enabled) flags |= AxisStatusWord.Enabled;
        if (axis.Moving) flags |= AxisStatusWord.Moving;
        if (axis.Homing) flags |= AxisStatusWord.Homing;
        if (axis.Moving && !axis.Homing && !axis.IsJogging) flags |= AxisStatusWord.Positioning;
        if (axis.Fault) flags |= AxisStatusWord.Fault;
        if (axis.InPosition) flags |= AxisStatusWord.InPosition;
        if (axis.HomeCompleted) flags |= AxisStatusWord.HomeCompleted;
        if (axis.Moving) flags |= AxisStatusWord.CommandBusy;
        if (axis.CommandDone) flags |= AxisStatusWord.CommandDone;

        return flags;
    }

    private sealed class SimulatedAxis
    {
        public double Position { get; set; }
        public double TargetPosition { get; set; }
        public double MoveStartPosition { get; set; }
        public double Velocity { get; set; }
        public double Torque { get; set; }

        public bool Enabled { get; set; }
        public bool Moving { get; set; }
        public bool IsJogging { get; set; }
        public bool Homing { get; set; }
        public bool InPosition { get; set; }
        public bool CommandDone { get; set; }
        public bool HomeCompleted { get; set; }
        public bool Fault { get; set; }
        public int FaultCode { get; set; }

        public DateTimeOffset? MotionStartedAt { get; set; }
    }
}
