using EquipmentMonitor.Infrastructure;
using EquipmentMonitor.Infrastructure.Axis;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

public class AxisConfigExtensionsTests
{
    [Fact]
    public void ToElementConfig_MapsIdentityAndMotionDefaults()
    {
        var entity = new AxisConfig
        {
            AxisNo = 1,
            Name = "Y 轴",
            Unit = "mm",
            DefaultVelocity = 25.0,
            MaxVelocity = 100.0,
            Acceleration = 500.0,
            Deceleration = 400.0,
            SoftLimitMin = -50.0,
            SoftLimitMax = 200.0,
            HomeMethod = 17,
            IsEnabled = true
        };

        var config = entity.ToElementConfig(controllerNo: 2);

        config.ElementId.Should().Be("Axis-2-1");
        config.Name.Should().Be("Y 轴");
        config.ControllerNo.Should().Be(2);
        config.AxisNo.Should().Be(1);
        config.DefaultVelocity.Should().Be(25.0);
        config.MaxVelocity.Should().Be(100.0);
        config.HomeMethod.Should().Be(17);
        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ToElementConfig_WhenSoftLimitsReversed_DoesNotThrowBecauseHardwareOwnsThem()
    {
        var entity = new AxisConfig
        {
            AxisNo = 0,
            Name = "X",
            SoftLimitMin = 100.0,
            SoftLimitMax = -100.0
        };

        var act = () => entity.ToElementConfig(0);

        act.Should().NotThrow();
    }

    [Fact]
    public void ToElementConfig_WhenNameMissing_UsesStableFallback()
    {
        var entity = new AxisConfig
        {
            AxisNo = 3,
            Name = null
        };

        var config = entity.ToElementConfig(1);

        config.ElementId.Should().Be("Axis-1-3");
        config.Name.Should().Be("Axis-3");
    }
}
