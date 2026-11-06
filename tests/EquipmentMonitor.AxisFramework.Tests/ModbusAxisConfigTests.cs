using EquipmentMonitor.Infrastructure;
using EquipmentMonitor.Infrastructure.Hardware.PLC.Modbus;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

public class ModbusAxisConfigTests
{
    [Fact]
    public void ToModbusMap_MapsAddressesScalesAndDefaults()
    {
        var axis = new AxisConfig
        {
            AxisNo = 2,
            Name = "Z 轴",
            StateStartAddress = 100,
            CommandStartAddress = 200,
            ExecuteCoilAddress = 10,
            PositionScale = 0.001,
            VelocityScale = 0.1,
            TorqueScale = 0.5,
            AccelerationScale = 2.0,
            DecelerationScale = 3.0,
            JerkScale = 4.0,
            DefaultVelocity = 20.0,
            Acceleration = 200.0,
            Deceleration = 300.0,
            Jerk = 400.0,
            HomeMethod = 17
        };

        var map = axis.ToModbusMap();

        map.StateStartAddress.Should().Be(100);
        map.CommandStartAddress.Should().Be(200);
        map.ExecuteCoilAddress.Should().Be(10);
        map.PositionScale.Should().Be(0.001);
        map.VelocityScale.Should().Be(0.1);
        map.TorqueScale.Should().Be(0.5);
        map.DefaultVelocity.Should().Be(20.0);
        map.Acceleration.Should().Be(200.0);
        map.Deceleration.Should().Be(300.0);
        map.HomeMethod.Should().Be(17);
    }

    [Fact]
    public void ToModbusMap_WhenAddressMissing_Throws()
    {
        var axis = new AxisConfig
        {
            AxisNo = 0,
            Name = "X 轴",
            StateStartAddress = 100
            // CommandStartAddress / ExecuteCoilAddress 缺失
        };

        var act = () => axis.ToModbusMap();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*缺少*");
    }

    [Fact]
    public void ModbusProtocolPool_WithSamePlcEndpoint_ReusesProtocol()
    {
        using var pool = new ModbusProtocolPool();
        var first = new ControllerConfig
        {
            ConnectionType = ControllerConnectionType.ModbusTcp,
            IpAddress = "127.0.0.1",
            Port = 502,
            SlaveAddress = 1
        };
        var second = new ControllerConfig
        {
            ConnectionType = ControllerConnectionType.ModbusTcp,
            IpAddress = "127.0.0.1",
            Port = 502,
            SlaveAddress = 1
        };

        var protocol = pool.GetOrCreate(first);

        pool.GetOrCreate(second).Should().BeSameAs(protocol);
    }

    [Theory]
    [InlineData(ControllerConnectionType.ModbusTcp, true)]
    [InlineData(ControllerConnectionType.ModbusRtu, true)]
    [InlineData(ControllerConnectionType.PciCard, false)]
    public void ModbusAxisControllerFactory_CanCreate_OnlyForModbusConnections(
        ControllerConnectionType connectionType,
        bool expected)
    {
        var factory = new ModbusAxisControllerFactory(
            new ModbusRegisterControllerFactory(new ModbusProtocolPool()));

        factory.CanCreate(new ControllerConfig { ConnectionType = connectionType })
            .Should().Be(expected);
    }
}
