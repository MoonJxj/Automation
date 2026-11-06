using EquipmentMonitor.Common;
using EquipmentMonitor.Infrastructure;

namespace EquipmentMonitor.Infrastructure.Hardware.PLC.Modbus;

/// <summary>
/// 基于 <see cref="IModbusProtocol"/> 的通用保持寄存器读写实现。
/// </summary>
/// <remarks>
/// <para>
/// 本类是 <see cref="IRegisterController"/> 的 Modbus 实现：只负责地址、寄存器块与类型转换，
/// 不关心某个轴/IO 点的语义。轴控制器（<c>ModbusAxisController</c>）应组合本类，
/// 而不是自己再写一套寄存器读写。
/// </para>
/// <para>
/// <b>写后回读</b>：<see cref="IModbusProtocol.WriteBatchAsync"/> 目前把异常吞掉、调用方拿不到失败；
/// 本类在写入后立即回读并比较，至少能把“写成功但设备没接受”暴露成 <see cref="Result.Fail"/>。
/// 这会多一次通讯往返，但轴控写错地址/值比多一次往返昂贵得多。
/// </para>
/// <para>
/// <b>连接归属</b>：协议实例由外部（DI/工厂）持有，本类不 <c>Dispose</c> 它，
/// 避免多个控制器共享同一个协议对象时互相把连接关掉。
/// </para>
/// </remarks>
public sealed class ModbusRegisterController : IRegisterController
{
    private readonly IModbusProtocol _protocol;
    private bool _initialized;

    /// <summary>
    /// 创建寄存器控制器。
    /// </summary>
    /// <param name="protocol">已配置好连接参数的 Modbus 协议实例。</param>
    public ModbusRegisterController(IModbusProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        _protocol = protocol;
    }

    /// <inheritdoc />
    public bool IsInitialize => _initialized && _protocol.IsConnected;

    /// <inheritdoc />
    public int ControllerCount => 1;

    /// <inheritdoc />
    public bool Initialize()
    {
        try
        {
            _protocol.Connect();
            _initialized = _protocol.IsConnected;
            return _initialized;
        }
        catch (Exception)
        {
            _initialized = false;
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<Result<ushort[]>> ReadRegistersAsync(
        int controllerNo,
        ushort startAddress,
        ushort count,
        CancellationToken cancellationToken = default)
    {
        if (!IsInitialize)
        {
            return Result.Fail<ushort[]>(
                $"Modbus 寄存器读取失败：控制器 {controllerNo} 未连接。",
                "Modbus.NotConnected");
        }

        if (count == 0)
        {
            return Result.Fail<ushort[]>(
                "Modbus 寄存器读取失败：读取数量必须大于 0。",
                "Modbus.InvalidCount");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var values = await _protocol.ReadBatchAsync(
                ModbusRegisterType.HoldingRegister, startAddress, count).ConfigureAwait(false);

            if (values is null || values.Count != count)
            {
                return Result.Fail<ushort[]>(
                    $"Modbus 寄存器读取失败：请求 {count} 个，实际返回 " +
                    $"{(values is null ? "null" : values.Count.ToString())} 个。",
                    "Modbus.ReadFailed");
            }

            var registers = new ushort[count];
            for (var i = 0; i < count; i++)
            {
                if (!values.TryGetValue((ushort)(startAddress + i), out var raw) || raw is null)
                {
                    return Result.Fail<ushort[]>(
                        $"Modbus 寄存器读取失败：缺少地址 {startAddress + i} 的值。",
                        "Modbus.ReadIncomplete");
                }

                registers[i] = Convert.ToUInt16(raw);
            }

            return Result.Ok(registers);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<ushort[]>(
                $"Modbus 寄存器读取异常：{ex.Message}",
                "Modbus.ReadException",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> WriteRegistersAsync(
        int controllerNo,
        ushort startAddress,
        ushort[] values,
        CancellationToken cancellationToken = default,
        bool verifyReadBack = true)
    {
        if (!IsInitialize)
        {
            return Result.Fail(
                $"Modbus 寄存器写入失败：控制器 {controllerNo} 未连接。",
                "Modbus.NotConnected");
        }

        if (values is null || values.Length == 0)
        {
            return Result.Fail(
                "Modbus 寄存器写入失败：写入值不能为空。",
                "Modbus.InvalidValues");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _protocol.WriteBatchAsync(
                ModbusRegisterType.HoldingRegister,
                startAddress,
                values.Cast<object>()).ConfigureAwait(false);

            if (!verifyReadBack)
            {
                return Result.Ok();
            }

            // 写后回读：协议层吞异常，只有回读才能证明设备真的接受了写入。
            var readBack = await ReadRegistersAsync(
                controllerNo, startAddress, (ushort)values.Length, cancellationToken).ConfigureAwait(false);

            if (readBack.IsFailure)
            {
                return Result.Fail(
                    $"Modbus 写入后回读失败：{readBack.Error}",
                    readBack.ErrorCode,
                    readBack.Exception);
            }

            if (!readBack.Value.AsSpan().SequenceEqual(values))
            {
                return Result.Fail(
                    $"Modbus 写入后回读不一致：地址 {startAddress}，写入 " +
                    $"[{string.Join(",", values)}]，回读 [{string.Join(",", readBack.Value)}]。",
                    "Modbus.WriteVerifyFailed");
            }

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail(
                $"Modbus 寄存器写入异常：{ex.Message}",
                "Modbus.WriteException",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool[]>> ReadCoilsAsync(
        int controllerNo,
        ushort startAddress,
        ushort count,
        CancellationToken cancellationToken = default)
    {
        if (!IsInitialize)
        {
            return Result.Fail<bool[]>(
                $"Modbus 线圈读取失败：控制器 {controllerNo} 未连接。",
                "Modbus.NotConnected");
        }

        if (count == 0)
        {
            return Result.Fail<bool[]>(
                "Modbus 线圈读取失败：读取数量必须大于 0。",
                "Modbus.InvalidCount");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var values = await _protocol.ReadBatchAsync(
                ModbusRegisterType.Coil, startAddress, count).ConfigureAwait(false);

            if (values is null || values.Count != count)
            {
                return Result.Fail<bool[]>(
                    $"Modbus 线圈读取失败：请求 {count} 个，实际返回 " +
                    $"{(values is null ? "null" : values.Count.ToString())} 个。",
                    "Modbus.ReadFailed");
            }

            var coils = new bool[count];
            for (var i = 0; i < count; i++)
            {
                if (!values.TryGetValue((ushort)(startAddress + i), out var raw) || raw is null)
                {
                    return Result.Fail<bool[]>(
                        $"Modbus 线圈读取失败：缺少地址 {startAddress + i} 的值。",
                        "Modbus.ReadIncomplete");
                }

                coils[i] = Convert.ToBoolean(raw);
            }

            return Result.Ok(coils);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail<bool[]>(
                $"Modbus 线圈读取异常：{ex.Message}",
                "Modbus.ReadException",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> WriteCoilAsync(
        int controllerNo,
        ushort address,
        bool value,
        CancellationToken cancellationToken = default,
        bool verifyReadBack = true)
    {
        if (!IsInitialize)
        {
            return Result.Fail(
                $"Modbus 线圈写入失败：控制器 {controllerNo} 未连接。",
                "Modbus.NotConnected");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _protocol.WriteBatchAsync(
                ModbusRegisterType.Coil, address, new object[] { value }).ConfigureAwait(false);

            if (!verifyReadBack)
            {
                return Result.Ok();
            }

            var readBack = await ReadCoilsAsync(controllerNo, address, 1, cancellationToken)
                .ConfigureAwait(false);

            if (readBack.IsFailure)
            {
                return Result.Fail(
                    $"Modbus 线圈写入后回读失败：{readBack.Error}",
                    readBack.ErrorCode,
                    readBack.Exception);
            }

            if (readBack.Value[0] != value)
            {
                return Result.Fail(
                    $"Modbus 线圈写入后回读不一致：地址 {address}，写入 {value}，回读 {readBack.Value[0]}。",
                    "Modbus.WriteVerifyFailed");
            }

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result.Fail(
                $"Modbus 线圈写入异常：{ex.Message}",
                "Modbus.WriteException",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<T>> ReadValueAsync<T>(
        int controllerNo,
        ushort address,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst,
        bool highByte = false,
        CancellationToken cancellationToken = default)
        where T : struct
    {
        var count = RegisterValueConverter.GetRegisterCount<T>();
        var read = await ReadRegistersAsync(
            controllerNo, address, (ushort)count, cancellationToken).ConfigureAwait(false);

        if (read.IsFailure)
        {
            return Result.Fail<T>(read.Error, read.ErrorCode, read.Exception);
        }

        try
        {
            return Result.Ok(RegisterValueConverter.ToValue<T>(read.Value, wordOrder, highByte));
        }
        catch (Exception ex)
        {
            return Result.Fail<T>(
                $"Modbus 寄存器值转换为 {typeof(T).Name} 失败：{ex.Message}",
                "Modbus.ConvertFailed",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result> WriteValueAsync<T>(
        int controllerNo,
        ushort address,
        T value,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst,
        bool highByte = false,
        CancellationToken cancellationToken = default)
        where T : struct
    {
        ushort[] registers;
        try
        {
            registers = RegisterValueConverter.FromValue(value, wordOrder, highByte);
        }
        catch (Exception ex)
        {
            return Result.Fail(
                $"Modbus 写入值 {typeof(T).Name} 拆分寄存器失败：{ex.Message}",
                "Modbus.ConvertFailed",
                ex);
        }

        return await WriteRegistersAsync(controllerNo, address, registers, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // 协议实例由外部持有；这里不关闭连接，避免共享协议时误伤其它控制器。
        _initialized = false;
    }
}
