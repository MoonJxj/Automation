using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.AxisAdapter.Sample;

/// <summary>
/// 第三方轴适配器的最小样例：只实现 Core 的 <see cref="IAxisController"/>。
/// </summary>
/// <remarks>
/// <para>
/// 本类型刻意不引用 Infrastructure：不依赖 NModbus、SqlSugar、Serilog 或参考 DI。
/// 外部开发者可以按同样方式把自己的厂商 DLL/PLC 协议适配成 <see cref="IAxisController"/>，
/// 再交给 <c>AxisElement</c>（Core.Axis）使用。
/// </para>
/// <para>
/// 这里的运动是内存模拟：命令只记录目标，位置立即到达；真实实现应按厂商语义下发并回读状态。
/// </para>
/// </remarks>
public sealed class SampleAxisController : IAxisController
{
    private readonly object _gate = new();
    private readonly SampleAxis[] _axes;

    private bool _initialized;

    /// <summary>创建样例控制器。</summary>
    /// <param name="axisCount">轴数量，必须大于 0。</param>
    public SampleAxisController(int axisCount = 1)
    {
        if (axisCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(axisCount));
        }

        _axes = Enumerable.Range(0, axisCount).Select(_ => new SampleAxis()).ToArray();
    }

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
    public int AxisCount => _axes.Length;

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
    public void GetAxisCount(int ControllerNo, ref uint axisCount)
    {
        if (ControllerNo == 0)
        {
            axisCount = (uint)_axes.Length;
        }
    }

    /// <inheritdoc />
    public bool ServeOn(int ControllerNo, int axisNo)
        => Update(axisNo, axis => axis.Enabled = true);

    /// <inheritdoc />
    public bool ServeOff(int ControllerNo, int axisNo)
        => Update(axisNo, axis => axis.Enabled = false);

    /// <inheritdoc />
    public bool Home(int ControllerNo, int axisNo)
        => Update(axisNo, axis =>
        {
            axis.Position = 0;
            axis.InPosition = true;
        });

    /// <inheritdoc />
    public bool MoveAbs(int ControllerNo, int axisNo, double position)
        => Update(axisNo, axis =>
        {
            axis.Position = position;
            axis.InPosition = true;
        });

    /// <inheritdoc />
    public bool MoveRel(int ControllerNo, int axisNo, double distance)
        => Update(axisNo, axis =>
        {
            axis.Position += distance;
            axis.InPosition = true;
        });

    /// <inheritdoc />
    public bool MoveInPos(int ControllerNo, int axisNo)
        => Update(axisNo, axis => axis.InPosition = true);

    /// <inheritdoc />
    public bool Jog(int ControllerNo, int axisNo, bool direction)
        => Update(axisNo, axis =>
        {
            axis.Position += direction ? 1 : -1;
            axis.Moving = true;
        });

    /// <inheritdoc />
    public bool Stop(int ControllerNo, int axisNo)
        => Update(axisNo, axis => axis.Moving = false);

    /// <inheritdoc />
    public bool ResetAxis(int ControllerNo, int axisNo)
        => Update(axisNo, axis =>
        {
            axis.FaultCode = 0;
            axis.Error = null;
        });

    /// <inheritdoc />
    public bool ResetAll()
    {
        lock (_gate)
        {
            foreach (var axis in _axes)
            {
                axis.FaultCode = 0;
                axis.Error = null;
            }

            return true;
        }
    }

    /// <inheritdoc />
    public double GetPos(int ControllerNo, int axisNo)
    {
        lock (_gate)
        {
            return GetAxis(axisNo).Position;
        }
    }

    /// <inheritdoc />
    public double GetVel(int ControllerNo, int axisNo)
    {
        lock (_gate)
        {
            return GetAxis(axisNo).Moving ? 1.0 : 0.0;
        }
    }

    /// <inheritdoc />
    public double GetTorque(int ControllerNo, int axisNo)
    {
        lock (_gate)
        {
            _ = GetAxis(axisNo);
            return 0.0;
        }
    }

    /// <inheritdoc />
    public bool ReadStateWord(int ControllerNo, int axisNo, string type, out int state)
    {
        lock (_gate)
        {
            var axis = GetAxis(axisNo);

            if (string.Equals(type, AxisStateWordTypes.FaultCode, StringComparison.Ordinal))
            {
                state = axis.FaultCode;
                return true;
            }

            if (string.Equals(type, AxisStateWordTypes.StateWord, StringComparison.Ordinal))
            {
                state = BuildStatusWord(axis);
                return true;
            }

            state = 0;
            return false;
        }
    }

    /// <inheritdoc />
    public AxisOperationError? GetLastError(int ControllerNo, int axisNo)
    {
        lock (_gate)
        {
            return GetAxis(axisNo).Error;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _initialized = false;
        }
    }

    private bool Update(int axisNo, Action<SampleAxis> update)
    {
        lock (_gate)
        {
            if (!_initialized)
            {
                return false;
            }

            var axis = GetAxis(axisNo);
            update(axis);
            return true;
        }
    }

    private SampleAxis GetAxis(int axisNo)
    {
        if (axisNo < 0 || axisNo >= _axes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(axisNo));
        }

        return _axes[axisNo];
    }

    private static int BuildStatusWord(SampleAxis axis)
    {
        var flags = AxisStatusWord.None;

        if (axis.Enabled)
        {
            flags |= AxisStatusWord.Enabled;
        }

        if (axis.Moving)
        {
            flags |= AxisStatusWord.Moving;
        }

        if (axis.InPosition)
        {
            flags |= AxisStatusWord.InPosition;
        }

        if (axis.FaultCode != 0)
        {
            flags |= AxisStatusWord.Fault;
        }

        return (int)flags;
    }

    private sealed class SampleAxis
    {
        public bool Enabled { get; set; }

        public bool Moving { get; set; }

        public bool InPosition { get; set; } = true;

        public double Position { get; set; }

        public int FaultCode { get; set; }

        public AxisOperationError? Error { get; set; }
    }
}
