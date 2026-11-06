using EquipmentMonitor.Common;
using EquipmentMonitor.Infrastructure;
using EquipmentMonitor.Infrastructure.Hardware.PLC.Modbus;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

public class ModbusRegisterControllerTests
{
    [Fact]
    public async Task WriteValueAsync_ThenReadValueAsync_RoundTripsInt32()
    {
        var protocol = new FakeModbusProtocol();
        var controller = new ModbusRegisterController(protocol);
        controller.Initialize().Should().BeTrue();

        var write = await controller.WriteValueAsync(0, 10, 0x12345678);
        var read = await controller.ReadValueAsync<int>(0, 10);

        write.IsSuccess.Should().BeTrue();
        read.IsSuccess.Should().BeTrue();
        read.Value.Should().Be(0x12345678);
        protocol.Registers[10].Should().Be(0x1234);
        protocol.Registers[11].Should().Be(0x5678);
    }

    [Fact]
    public async Task WriteValueAsync_WithLowWordFirst_RoundTripsInt32()
    {
        var protocol = new FakeModbusProtocol();
        var controller = new ModbusRegisterController(protocol);
        controller.Initialize();

        await controller.WriteValueAsync(
            0, 10, 0x12345678, RegisterWordOrder.LowWordFirst);
        var read = await controller.ReadValueAsync<int>(
            0, 10, RegisterWordOrder.LowWordFirst);

        read.IsSuccess.Should().BeTrue();
        read.Value.Should().Be(0x12345678);
    }

    [Fact]
    public async Task WriteRegistersAsync_WhenReadBackDoesNotMatch_Fails()
    {
        var protocol = new FakeModbusProtocol { DropWrites = true };
        var controller = new ModbusRegisterController(protocol);
        controller.Initialize();

        var result = await controller.WriteRegistersAsync(0, 10, [0x1111, 0x2222]);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("Modbus.WriteVerifyFailed");
    }

    [Fact]
    public async Task ReadRegistersAsync_WhenNotInitialized_Fails()
    {
        var controller = new ModbusRegisterController(new FakeModbusProtocol());

        var result = await controller.ReadRegistersAsync(0, 0, 2);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("Modbus.NotConnected");
    }

    [Fact]
    public async Task ReadRegistersAsync_WhenProtocolReturnsWrongCount_Fails()
    {
        var protocol = new FakeModbusProtocol { ReturnShortBlock = true };
        var controller = new ModbusRegisterController(protocol);
        controller.Initialize();

        var result = await controller.ReadRegistersAsync(0, 0, 4);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("Modbus.ReadFailed");
    }

    private sealed class FakeModbusProtocol : IModbusProtocol
    {
        public readonly Dictionary<ushort, ushort> Registers = new();

        public bool DropWrites { get; init; }
        public bool ReturnShortBlock { get; init; }

        public bool IsConnected { get; private set; }
        public string IP { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 502;
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 9600;
        public System.IO.Ports.Parity Parity { get; set; }
        public int DataBits { get; set; } = 8;
        public System.IO.Ports.StopBits StopBits { get; set; }
        public byte SlaveAddress { get; set; } = 1;
        public bool IsModbusTCP { get; set; } = true;

        public void Connect() => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public Task<T> ReadAsync<T>(ModbusRegisterType type, ushort address)
            => throw new NotSupportedException();

        public Task WriteAsync<T>(ModbusRegisterType type, ushort address, T value)
            => throw new NotSupportedException();

        public Task<Dictionary<ushort, object>> ReadBatchAsync(
            ModbusRegisterType type,
            ushort startAddress,
            ushort length)
        {
            var result = new Dictionary<ushort, object>();
            var count = ReturnShortBlock ? Math.Max(0, length - 1) : length;

            for (var i = 0; i < count; i++)
            {
                var address = (ushort)(startAddress + i);
                result[address] = Registers.TryGetValue(address, out var value) ? value : (ushort)0;
            }

            return Task.FromResult(result);
        }

        public Task WriteBatchAsync(
            ModbusRegisterType type,
            ushort startAddress,
            IEnumerable<object> values)
        {
            if (DropWrites)
            {
                return Task.CompletedTask;
            }

            var address = startAddress;
            foreach (var value in values)
            {
                Registers[address++] = Convert.ToUInt16(value);
            }

            return Task.CompletedTask;
        }
    }
}
