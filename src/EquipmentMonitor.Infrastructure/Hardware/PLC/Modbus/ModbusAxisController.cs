using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure;
using EquipmentMonitor.Infrastructure.Axis;

namespace EquipmentMonitor.Infrastructure.Hardware.PLC.Modbus;

/// <summary>
/// Modbus 轴控制器：把 <see cref="IRegisterController"/> 的寄存器/线圈读写翻译成轴命令。
/// </summary>
/// <remarks>
/// <para>
/// <b>异步边界</b>：本类实现 <see cref="IAsyncAxisController"/>，不实现同步
/// <see cref="IAxisController"/>。同步厂商接口与异步 Modbus 的桥接由
/// <c>AsyncAxisControllerAdapter</c> 单独负责，阻塞只发生在桥里。
/// </para>
/// <para>
/// <b>寄存器布局</b>：见 <see cref="ModbusAxisRegisterLayout"/>。状态区 9 个寄存器，
/// 命令区 10 个寄存器 + Execute 线圈；每根轴的基址与缩放由 <see cref="ModbusAxisRegisterMap"/> 提供。
/// </para>
/// <para>
/// <b>写序列</b>：定位先写目标/速度/曲线（从命令区 +2 开始的一块），再写 Mode，
/// 最后 Execute 线圈先 0 后 1。控制字（+0）只在使能/下使能/复位/停止时写，
/// 避免定位时把控制字误写成 0 导致驱动器掉使能。
/// </para>
/// <para>
/// 一个实例对应一个控制器（<c>ControllerConfig</c> 一行）；<c>controllerNo</c> 参数仅用于诊断，
/// 轴选择以 <c>axisNo</c> 为准。
/// </para>
/// </remarks>
internal sealed class ModbusAxisController : IAsyncAxisController, IDisposable
{
    private readonly IRegisterController _registers;
    private readonly IReadOnlyDictionary<int, ModbusAxisRegisterMap> _axes;

    /// <summary>
    /// 创建 Modbus 轴控制器。
    /// </summary>
    /// <param name="registers">寄存器/线圈读写层。</param>
    /// <param name="axes">轴号 → 寄存器映射；至少一根轴。</param>
    public ModbusAxisController(
        IRegisterController registers,
        IReadOnlyDictionary<int, ModbusAxisRegisterMap> axes)
    {
        ArgumentNullException.ThrowIfNull(registers);
        ArgumentNullException.ThrowIfNull(axes);

        if (axes.Count == 0)
        {
            throw new ArgumentException("Modbus 轴控制器至少需要一根轴的寄存器映射。", nameof(axes));
        }

        foreach (var (axisNo, map) in axes)
        {
            ArgumentNullException.ThrowIfNull(map);
            map.Validate();
            if (axisNo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(axes), "轴号不能为负数。");
            }
        }

        _registers = registers;
        _axes = axes;
    }

    /// <inheritdoc />
    public bool IsInitialize => _registers.IsInitialize;

    /// <inheritdoc />
    public int ControllerCount => _registers.ControllerCount;

    /// <inheritdoc />
    public int AxisCount => _axes.Count;

    /// <inheritdoc />
    public Task<Result> InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_registers.IsInitialize)
        {
            return Task.FromResult(Result.Ok());
        }

        return Task.FromResult(_registers.Initialize()
            ? Result.Ok()
            : Result.Fail("Modbus 轴控制器初始化失败：寄存器层连接未建立。", "ModbusAxis.InitializeFailed"));
    }

    /// <inheritdoc />
    public Task<Result> ServeOnAsync(
        int controllerNo,
        int axisNo,
        CancellationToken cancellationToken = default)
        => WriteControlWordAsync(controllerNo, axisNo, ModbusAxisRegisterLayout.ControlEnableOperation, cancellationToken);

    /// <inheritdoc />
    public Task<Result> ServeOffAsync(
        int controllerNo,
        int axisNo,
        CancellationToken cancellationToken = default)
        => WriteControlWordAsync(controllerNo, axisNo, ModbusAxisRegisterLayout.ControlSwitchOn, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> HomeAsync(
        int controllerNo,
        int axisNo,
        CancellationToken cancellationToken = default)
    {
        var map = GetMap(axisNo);

        var method = await WriteRegisterAsync(
            controllerNo,
            map.CommandStartAddress + ModbusAxisRegisterLayout.CommandHomingMethod,
            (ushort)Math.Clamp(map.HomeMethod, 0, ushort.MaxValue),
            cancellationToken).ConfigureAwait(false);
        if (method.IsFailure)
        {
            return method;
        }

        var mode = await WriteRegisterAsync(
            controllerNo,
            map.CommandStartAddress + ModbusAxisRegisterLayout.CommandMode,
            ModbusAxisRegisterLayout.ModeHoming,
            cancellationToken).ConfigureAwait(false);
        if (mode.IsFailure)
        {
            return mode;
        }

        return await PulseExecuteAsync(controllerNo, map, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Result> MoveAbsAsync(
        int controllerNo,
        int axisNo,
        double position,
        CancellationToken cancellationToken = default)
        => WriteMoveAsync(controllerNo, axisNo, position, relative: false, cancellationToken);

    /// <inheritdoc />
    public Task<Result> MoveRelAsync(
        int controllerNo,
        int axisNo,
        double distance,
        CancellationToken cancellationToken = default)
        => WriteMoveAsync(controllerNo, axisNo, distance, relative: true, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> MoveInPosAsync(
        int controllerNo,
        int axisNo,
        CancellationToken cancellationToken = default)
    {
        var map = GetMap(axisNo);

        var mode = await WriteRegisterAsync(
            controllerNo,
            map.CommandStartAddress + ModbusAxisRegisterLayout.CommandMode,
            ModbusAxisRegisterLayout.ModeProfilePosition,
            cancellationToken).ConfigureAwait(false);
        if (mode.IsFailure)
        {
            return mode;
        }

        return await PulseExecuteAsync(controllerNo, map, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result> JogAsync(
        int controllerNo,
        int axisNo,
        bool direction,
        CancellationToken cancellationToken = default)
    {
        var map = GetMap(axisNo);

        var velocity = direction ? map.DefaultVelocity : -map.DefaultVelocity;
        var velocityRaw = ToInt16(velocity / map.VelocityScale, "点动速度");

        var write = await WriteRegisterAsync(
            controllerNo,
            map.CommandStartAddress + ModbusAxisRegisterLayout.CommandTargetVelocity,
            unchecked((ushort)velocityRaw),
            cancellationToken).ConfigureAwait(false);
        if (write.IsFailure)
        {
            return write;
        }

        var mode = await WriteRegisterAsync(
            controllerNo,
            map.CommandStartAddress + ModbusAxisRegisterLayout.CommandMode,
            ModbusAxisRegisterLayout.ModeProfileVelocity,
            cancellationToken).ConfigureAwait(false);
        if (mode.IsFailure)
        {
            return mode;
        }

        return await PulseExecuteAsync(controllerNo, map, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Result> StopAsync(
        int controllerNo,
        int axisNo,
        CancellationToken cancellationToken = default)
        => WriteControlWordAsync(controllerNo, axisNo, ModbusAxisRegisterLayout.ControlQuickStop, cancellationToken);

    /// <inheritdoc />
    public async Task<Result> ResetAxisAsync(
        int controllerNo,
        int axisNo,
        CancellationToken cancellationToken = default)
    {
        var reset = await WriteControlWordAsync(
            controllerNo, axisNo, ModbusAxisRegisterLayout.ControlFaultReset, cancellationToken).ConfigureAwait(false);
        if (reset.IsFailure)
        {
            return reset;
        }

        // 复位后恢复使能意图；现场若要求复位后保持下使能，应在命令层再显式下使能，而不是让复位偷偷改变意图。
        return await WriteControlWordAsync(
            controllerNo, axisNo, ModbusAxisRegisterLayout.ControlEnableOperation, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result> ResetAllAsync(CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        foreach (var axisNo in _axes.Keys.OrderBy(x => x))
        {
            var result = await ResetAxisAsync(0, axisNo, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                errors.Add($"轴 {axisNo}：{result.Error}");
            }
        }

        return errors.Count == 0
            ? Result.Ok()
            : Result.Fail($"部分轴复位失败：{string.Join("；", errors)}", "ModbusAxis.ResetPartialFailed");
    }

    /// <inheritdoc />
    public async Task<Result<AxisFeedback>> ReadFeedbackAsync(
        int controllerNo,
        int axisNo,
        CancellationToken cancellationToken = default)
    {
        var map = GetMap(axisNo);

        var read = await _registers.ReadRegistersAsync(
            controllerNo,
            map.StateStartAddress,
            ModbusAxisRegisterLayout.StateRegisterCount,
            cancellationToken).ConfigureAwait(false);

        if (read.IsFailure)
        {
            return Result.Fail<AxisFeedback>(read.Error, read.ErrorCode, read.Exception);
        }

        try
        {
            var registers = read.Value;
            var positionRaw = RegisterValueConverter.ToInt32(registers.AsSpan(0, 2));
            var velocityRaw = RegisterValueConverter.ToInt16(registers[ModbusAxisRegisterLayout.StateVelocity]);
            var torqueRaw = RegisterValueConverter.ToInt16(registers[ModbusAxisRegisterLayout.StateTorque]);
            var statusWord = (AxisStatusWord)registers[ModbusAxisRegisterLayout.StateWord];
            var faultCode = registers[ModbusAxisRegisterLayout.StateFaultCode];

            return Result.Ok(new AxisFeedback
            {
                ControllerNo = controllerNo,
                AxisNo = axisNo,
                ActualPosition = positionRaw * map.PositionScale,
                ActualVelocity = velocityRaw * map.VelocityScale,
                ActualTorque = torqueRaw * map.TorqueScale,
                StatusWord = statusWord,
                FaultCode = faultCode
            });
        }
        catch (Exception ex)
        {
            return Result.Fail<AxisFeedback>(
                $"Modbus 轴 {axisNo} 反馈解析失败：{ex.Message}",
                "ModbusAxis.FeedbackParseFailed",
                ex);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // 寄存器层由外部持有；这里不关闭连接。
    }

    private async Task<Result> WriteMoveAsync(
        int controllerNo,
        int axisNo,
        double target,
        bool relative,
        CancellationToken cancellationToken)
    {
        var map = GetMap(axisNo);

        if (!double.IsFinite(target))
        {
            return Result.Fail($"轴 {axisNo} 的目标值不是有限数：{target}。", "ModbusAxis.InvalidTarget");
        }

        int targetRaw;
        short velocityRaw;
        ushort accelRaw;
        ushort decelRaw;
        ushort jerkRaw;

        try
        {
            targetRaw = checked((int)Math.Round(target / map.PositionScale));
            velocityRaw = ToInt16(map.DefaultVelocity / map.VelocityScale, "速度");
            accelRaw = ToUInt16(map.Acceleration / map.AccelerationScale, "加速度");
            decelRaw = ToUInt16(map.Deceleration / map.DecelerationScale, "减速度");
            jerkRaw = ToUInt16(map.Jerk / map.JerkScale, "加加速度");
        }
        catch (Exception ex)
        {
            return Result.Fail(
                $"轴 {axisNo} 的运动参数无法换算为寄存器值：{ex.Message}",
                "ModbusAxis.ParameterOutOfRange",
                ex);
        }

        var block = new ushort[7];
        var targetRegisters = RegisterValueConverter.FromInt32(targetRaw);
        block[0] = targetRegisters[0];
        block[1] = targetRegisters[1];
        block[2] = relative ? (ushort)1 : (ushort)0;
        block[3] = unchecked((ushort)velocityRaw);
        block[4] = accelRaw;
        block[5] = decelRaw;
        block[6] = jerkRaw;

        var write = await _registers.WriteRegistersAsync(
            controllerNo,
            (ushort)(map.CommandStartAddress + ModbusAxisRegisterLayout.CommandTargetPosition),
            block,
            cancellationToken,
            verifyReadBack: false).ConfigureAwait(false);

        if (write.IsFailure)
        {
            return write;
        }

        var mode = await WriteRegisterAsync(
            controllerNo,
            map.CommandStartAddress + ModbusAxisRegisterLayout.CommandMode,
            ModbusAxisRegisterLayout.ModeProfilePosition,
            cancellationToken).ConfigureAwait(false);

        if (mode.IsFailure)
        {
            return mode;
        }

        return await PulseExecuteAsync(controllerNo, map, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> WriteControlWordAsync(
        int controllerNo,
        int axisNo,
        ushort controlWord,
        CancellationToken cancellationToken)
    {
        var map = GetMap(axisNo);

        return await WriteRegisterAsync(
            controllerNo,
            map.CommandStartAddress + ModbusAxisRegisterLayout.CommandControlWord,
            controlWord,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> WriteRegisterAsync(
        int controllerNo,
        int address,
        ushort value,
        CancellationToken cancellationToken)
        => await _registers.WriteRegistersAsync(
            controllerNo, (ushort)address, new[] { value }, cancellationToken, verifyReadBack: false)
            .ConfigureAwait(false);

    private async Task<Result> PulseExecuteAsync(
        int controllerNo,
        ModbusAxisRegisterMap map,
        CancellationToken cancellationToken)
    {
        // 先写 0 再写 1：PLC 侧只需检测上升沿，不依赖上位机记忆上次写了什么。
        var reset = await _registers.WriteCoilAsync(
            controllerNo, map.ExecuteCoilAddress, false, cancellationToken, verifyReadBack: false)
            .ConfigureAwait(false);
        if (reset.IsFailure)
        {
            return reset;
        }

        return await _registers.WriteCoilAsync(
            controllerNo, map.ExecuteCoilAddress, true, cancellationToken, verifyReadBack: false)
            .ConfigureAwait(false);
    }

    private ModbusAxisRegisterMap GetMap(int axisNo)
    {
        if (_axes.TryGetValue(axisNo, out var map))
        {
            return map;
        }

        throw new InvalidOperationException(
            $"Modbus 轴控制器没有轴 {axisNo} 的寄存器映射。已配置轴：{string.Join(",", _axes.Keys.OrderBy(x => x))}。");
    }

    private static short ToInt16(double value, string name)
    {
        var rounded = Math.Round(value);
        if (rounded < short.MinValue || rounded > short.MaxValue)
        {
            throw new OverflowException($"{name} {value} 超出 i16 范围。");
        }

        return (short)rounded;
    }

    private static ushort ToUInt16(double value, string name)
    {
        var rounded = Math.Round(value);
        if (rounded < 0 || rounded > ushort.MaxValue)
        {
            throw new OverflowException($"{name} {value} 超出 u16 范围。");
        }

        return (ushort)rounded;
    }
}
