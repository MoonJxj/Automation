using EquipmentMonitor.Infrastructure;

namespace EquipmentMonitor.Infrastructure.Hardware.Simulation;

/// <summary>
/// 内存 IO 控制器：用于无硬件联调气缸。可按点位配置“阀输出 → 限位输入”的联动，
/// 让气缸元件在没有 PLC/板卡时也能完整走完“下发 → 动作 → 到位/超时”的链路。
/// </summary>
/// <remarks>
/// <para>模拟是<i>按时间推进</i>的：<see cref="MotionDuration"/> 为 <see cref="TimeSpan.Zero"/> 时下一次读取立即到位；
/// 设大即可验证超时分支。</para>
/// <para>所有读写都在同一把锁内完成；不启动后台线程。</para>
/// </remarks>
public sealed class SimulatedIoController : IIoController
{
    private const int IoCount = 32;

    private readonly object _gate = new();
    private readonly bool[] _inputs = new bool[IoCount];
    private readonly bool[] _outputs = new bool[IoCount];
    private readonly Dictionary<int, SimulatedCylinder> _cylinders = [];
    private readonly TimeProvider _timeProvider;

    private bool _initialized;

    /// <summary>创建模拟 IO 控制器。</summary>
    public SimulatedIoController(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>气缸动作耗时；<see cref="TimeSpan.Zero"/> 表示下一次读取立即到位。</summary>
    public TimeSpan MotionDuration { get; set; } = TimeSpan.FromMilliseconds(300);

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
    public int ControllerCount => 1;

    /// <inheritdoc />
    public int InputCount => IoCount;

    /// <inheritdoc />
    public int OutputCount => IoCount;

    /// <inheritdoc />
    public bool Initialize()
    {
        lock (_gate)
        {
            _initialized = true;
            return true;
        }
    }

    /// <inheritdoc />
    public bool GetIOCount(int controllerNo, ref ushort inputCount, ref ushort outputCount)
    {
        if (controllerNo != 0)
        {
            return false;
        }

        inputCount = IoCount;
        outputCount = IoCount;
        return true;
    }

    /// <summary>
    /// 配置一个气缸的阀/限位映射。<paramref name="retractOutput"/> 传 <c>-1</c> 表示单控。
    /// </summary>
    public void ConfigureCylinder(
        int extendOutput,
        int retractOutput,
        int extendedInput,
        int retractedInput)
    {
        lock (_gate)
        {
            _cylinders[extendOutput] = new SimulatedCylinder(
                extendOutput,
                retractOutput >= 0 ? retractOutput : null,
                extendedInput,
                retractedInput);

            _inputs[retractedInput] = true;
        }
    }

    /// <inheritdoc />
    public bool SetOut(int controllerNo, int ioNo, bool state)
    {
        if (controllerNo != 0 || ioNo < 0 || ioNo >= IoCount)
        {
            return false;
        }

        lock (_gate)
        {
            AdvanceLocked();
            _outputs[ioNo] = state;

            foreach (var cylinder in _cylinders.Values)
            {
                if (cylinder.ExtendOutput == ioNo || cylinder.RetractOutput == ioNo)
                {
                    cylinder.OnOutputChanged(_outputs);
                }
            }

            return true;
        }
    }

    /// <inheritdoc />
    public bool ReadIn(int controllerNo, int ioNo)
    {
        if (controllerNo != 0 || ioNo < 0 || ioNo >= IoCount)
        {
            return false;
        }

        lock (_gate)
        {
            AdvanceLocked();
            return _inputs[ioNo];
        }
    }

    /// <inheritdoc />
    public bool ReadOut(int controllerNo, int ioNo)
    {
        if (controllerNo != 0 || ioNo < 0 || ioNo >= IoCount)
        {
            return false;
        }

        lock (_gate)
        {
            return _outputs[ioNo];
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _initialized = false;
            Array.Clear(_outputs);
        }
    }

    private void AdvanceLocked()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var cylinder in _cylinders.Values)
        {
            cylinder.Advance(now, MotionDuration, _inputs);
        }
    }

    private sealed class SimulatedCylinder
    {
        private readonly int _extendedInput;
        private readonly int _retractedInput;

        private bool _extended;
        private bool _retracted = true;
        private bool? _targetExtended;
        private DateTimeOffset _startedAt;

        public SimulatedCylinder(
            int extendOutput,
            int? retractOutput,
            int extendedInput,
            int retractedInput)
        {
            ExtendOutput = extendOutput;
            RetractOutput = retractOutput;
            _extendedInput = extendedInput;
            _retractedInput = retractedInput;
        }

        public int ExtendOutput { get; }

        public int? RetractOutput { get; }

        public void OnOutputChanged(IReadOnlyList<bool> outputs)
        {
            var extendOn = outputs[ExtendOutput];
            var retractOn = RetractOutput is { } retract && outputs[retract];

            bool? target = (ExtendOutput, RetractOutput) switch
            {
                (_, null) => extendOn,                 // 单控：置位伸出，复位缩回
                _ when extendOn && !retractOn => true,
                _ when retractOn && !extendOn => false,
                _ => null                              // 双控都断开：保持原位
            };

            if (target != _targetExtended)
            {
                _targetExtended = target;
                _startedAt = default;
            }
        }

        public void Advance(DateTimeOffset now, TimeSpan duration, bool[] inputs)
        {
            if (_targetExtended is not { } target)
            {
                return;
            }

            // 已经在目标位：清掉待执行目标，不重复写限位。
            if (_extended == target && _retracted == !target)
            {
                _targetExtended = null;
                return;
            }

            if (_startedAt == default)
            {
                _startedAt = now;
            }

            if (now - _startedAt < duration)
            {
                return;
            }

            _extended = target;
            _retracted = !target;
            _targetExtended = null;

            inputs[_extendedInput] = _extended;
            inputs[_retractedInput] = _retracted;
        }
    }
}
