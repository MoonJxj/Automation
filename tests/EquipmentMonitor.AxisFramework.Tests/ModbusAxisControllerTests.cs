using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure;
using EquipmentMonitor.Infrastructure.Axis;
using EquipmentMonitor.Infrastructure.Hardware.PLC.Modbus;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

public class ModbusAxisControllerTests
{
    [Fact]
    public async Task ServeOnAsync_WritesEnableOperationControlWord()
    {
        var registers = new FakeRegisterController();
        var controller = CreateController(registers);
        await controller.InitializeAsync();

        var result = await controller.ServeOnAsync(0, 0);

        result.IsSuccess.Should().BeTrue();
        registers.Registers[200].Should().Be(ModbusAxisRegisterLayout.ControlEnableOperation);
    }

    [Fact]
    public async Task MoveAbsAsync_WritesTargetBlockAndPulsesExecute()
    {
        var registers = new FakeRegisterController();
        var controller = CreateController(registers);
        await controller.InitializeAsync();

        var result = await controller.MoveAbsAsync(0, 0, 12.5);

        result.IsSuccess.Should().BeTrue();

        // +2/+3 目标位置 = 12.5 / 0.001 = 12500
        registers.Registers[202].Should().Be(0x0000);
        registers.Registers[203].Should().Be(12500);
        registers.Registers[204].Should().Be(0); // 绝对
        registers.Registers[205].Should().Be(10); // 默认速度
        registers.Registers[206].Should().Be(100); // 加速度
        registers.Registers[207].Should().Be(100); // 减速度
        registers.Registers[208].Should().Be(0); // 加加速度
        registers.Registers[201].Should().Be(ModbusAxisRegisterLayout.ModeProfilePosition);
        registers.Coils[10].Should().BeTrue();
    }

    [Fact]
    public async Task MoveRelAsync_WritesRelativeFlag()
    {
        var registers = new FakeRegisterController();
        var controller = CreateController(registers);
        await controller.InitializeAsync();

        var result = await controller.MoveRelAsync(0, 0, -5.0);

        result.IsSuccess.Should().BeTrue();
        registers.Registers[204].Should().Be(1);
        RegisterValueConverter.ToInt32([registers.Registers[202], registers.Registers[203]])
            .Should().Be(-5000);
    }

    [Fact]
    public async Task HomeAsync_WritesMethodModeAndPulse()
    {
        var registers = new FakeRegisterController();
        var controller = CreateController(registers);
        await controller.InitializeAsync();

        var result = await controller.HomeAsync(0, 0);

        result.IsSuccess.Should().BeTrue();
        registers.Registers[209].Should().Be(17);
        registers.Registers[201].Should().Be(ModbusAxisRegisterLayout.ModeHoming);
        registers.Coils[10].Should().BeTrue();
    }

    [Fact]
    public async Task ReadFeedbackAsync_ParsesPositionVelocityTorqueAndStatus()
    {
        var registers = new FakeRegisterController();
        var controller = CreateController(registers);

        var position = RegisterValueConverter.FromInt32(12500);
        registers.Registers[100] = position[0];
        registers.Registers[101] = position[1];
        registers.Registers[102] = unchecked((ushort)(short)100);
        registers.Registers[103] = unchecked((ushort)(short)10);
        registers.Registers[104] = (ushort)(AxisStatusWord.Enabled |
                                            AxisStatusWord.InPosition |
                                            AxisStatusWord.HomeCompleted);
        registers.Registers[105] = 7;
        await controller.InitializeAsync();

        var result = await controller.ReadFeedbackAsync(0, 0);

        result.IsSuccess.Should().BeTrue();
        result.Value.ActualPosition.Should().Be(12.5);
        result.Value.ActualVelocity.Should().Be(100.0);
        result.Value.ActualTorque.Should().Be(1.0);
        result.Value.IsEnabled.Should().BeTrue();
        result.Value.InPosition.Should().BeTrue();
        result.Value.IsHomeCompleted.Should().BeTrue();
        result.Value.FaultCode.Should().Be(7);
    }

    [Fact]
    public async Task ResetAxisAsync_WritesFaultResetThenRestoresEnable()
    {
        var registers = new FakeRegisterController();
        var controller = CreateController(registers);
        await controller.InitializeAsync();

        var result = await controller.ResetAxisAsync(0, 0);

        result.IsSuccess.Should().BeTrue();
        registers.Registers[200].Should().Be(ModbusAxisRegisterLayout.ControlEnableOperation);
        registers.WriteHistory.Should().ContainInOrder(
            (200, ModbusAxisRegisterLayout.ControlFaultReset),
            (200, ModbusAxisRegisterLayout.ControlEnableOperation));
    }

    [Fact]
    public async Task AsyncAxisControllerAdapter_ExposesAsyncControllerAsSyncAxisController()
    {
        var registers = new FakeRegisterController();
        var asyncController = CreateController(registers);
        var adapter = new AsyncAxisControllerAdapter(asyncController);

        adapter.Initialize().Should().BeTrue();
        adapter.ServeOn(0, 0).Should().BeTrue();
        adapter.MoveAbs(0, 0, 12.5).Should().BeTrue();
        adapter.ReadStateWord(0, 0, AxisStateWordTypes.StateWord, out var state).Should().BeTrue();

        registers.Registers[200].Should().Be(ModbusAxisRegisterLayout.ControlEnableOperation);
        state.Should().Be((int)AxisStatusWord.None);
    }

    [Fact]
    public async Task AsyncAxisControllerAdapter_WhenAsyncFailure_ThrowsControllerException()
    {
        var registers = new FakeRegisterController();
        var asyncController = CreateController(registers);
        var adapter = new AsyncAxisControllerAdapter(asyncController);

        var act = () => adapter.ServeOn(0, 0);

        act.Should().Throw<AxisControllerException>()
            .Which.ErrorCode.Should().Be("Modbus.NotConnected");
    }

    private static ModbusAxisController CreateController(FakeRegisterController registers)
        => new(
            registers,
            new Dictionary<int, ModbusAxisRegisterMap>
            {
                [0] = new()
                {
                    StateStartAddress = 100,
                    CommandStartAddress = 200,
                    ExecuteCoilAddress = 10,
                    PositionScale = 0.001,
                    VelocityScale = 1.0,
                    TorqueScale = 0.1,
                    AccelerationScale = 1.0,
                    DecelerationScale = 1.0,
                    JerkScale = 1.0,
                    DefaultVelocity = 10.0,
                    Acceleration = 100.0,
                    Deceleration = 100.0,
                    HomeMethod = 17
                }
            });

    private sealed class FakeRegisterController : IRegisterController
    {
        public readonly Dictionary<ushort, ushort> Registers = new();
        public readonly Dictionary<ushort, bool> Coils = new();
        public readonly List<(ushort Address, ushort Value)> WriteHistory = new();

        public bool IsInitialize { get; private set; }
        public int ControllerCount => 1;

        public bool Initialize()
        {
            IsInitialize = true;
            return true;
        }

        public Task<Result<ushort[]>> ReadRegistersAsync(
            int controllerNo,
            ushort startAddress,
            ushort count,
            CancellationToken cancellationToken = default)
        {
            if (!IsInitialize)
            {
                return Task.FromResult(Result.Fail<ushort[]>("未连接", "Modbus.NotConnected"));
            }

            var result = new ushort[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = Registers.TryGetValue((ushort)(startAddress + i), out var value) ? value : (ushort)0;
            }

            return Task.FromResult(Result.Ok(result));
        }

        public Task<Result> WriteRegistersAsync(
            int controllerNo,
            ushort startAddress,
            ushort[] values,
            CancellationToken cancellationToken = default,
            bool verifyReadBack = true)
        {
            if (!IsInitialize)
            {
                return Task.FromResult(Result.Fail("未连接", "Modbus.NotConnected"));
            }

            for (var i = 0; i < values.Length; i++)
            {
                var address = (ushort)(startAddress + i);
                Registers[address] = values[i];
                WriteHistory.Add((address, values[i]));
            }

            return Task.FromResult(Result.Ok());
        }

        public Task<Result<bool[]>> ReadCoilsAsync(
            int controllerNo,
            ushort startAddress,
            ushort count,
            CancellationToken cancellationToken = default)
        {
            var result = new bool[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = Coils.TryGetValue((ushort)(startAddress + i), out var value) && value;
            }

            return Task.FromResult(Result.Ok(result));
        }

        public Task<Result> WriteCoilAsync(
            int controllerNo,
            ushort address,
            bool value,
            CancellationToken cancellationToken = default,
            bool verifyReadBack = true)
        {
            Coils[address] = value;
            return Task.FromResult(Result.Ok());
        }

        public async Task<Result<T>> ReadValueAsync<T>(
            int controllerNo,
            ushort address,
            RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst,
            bool highByte = false,
            CancellationToken cancellationToken = default)
            where T : struct
        {
            var read = await ReadRegistersAsync(
                controllerNo, address, (ushort)RegisterValueConverter.GetRegisterCount<T>(), cancellationToken);

            if (read.IsFailure)
            {
                return Result.Fail<T>(read.Error, read.ErrorCode, read.Exception);
            }

            return Result.Ok(RegisterValueConverter.ToValue<T>(read.Value, wordOrder, highByte));
        }

        public async Task<Result> WriteValueAsync<T>(
            int controllerNo,
            ushort address,
            T value,
            RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst,
            bool highByte = false,
            CancellationToken cancellationToken = default)
            where T : struct
        {
            var registers = RegisterValueConverter.FromValue(value, wordOrder, highByte);
            return await WriteRegistersAsync(controllerNo, address, registers, cancellationToken);
        }

        public void Dispose()
        {
        }
    }
}
