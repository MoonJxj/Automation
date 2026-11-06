using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Models;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

public class AxisParameterValidatorTests
{
    [Fact]
    public void Validate_WhenAxisRulesHold_IsValid()
    {
        var result = AxisParameterValidator.Validate(new ElementParameterSet
        {
            ElementId = "Axis-0-0",
            Values = new Dictionary<string, CommandValue>
            {
                [AxisParameterKeys.DefaultVelocity] = CommandValue.FromNumber(10),
                [AxisParameterKeys.Acceleration] = CommandValue.FromNumber(100),
                [AxisParameterKeys.Deceleration] = CommandValue.FromNumber(100),
                [AxisParameterKeys.Jerk] = CommandValue.FromNumber(0),
                [AxisParameterKeys.MaxVelocity] = CommandValue.FromNumber(100),
                [AxisParameterKeys.SoftLimitMin] = CommandValue.FromNumber(-100),
                [AxisParameterKeys.SoftLimitMax] = CommandValue.FromNumber(100),
                [AxisParameterKeys.PositionScale] = CommandValue.FromNumber(1)
            }
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenMotionValuesInvalid_ReturnsAxisSpecificIssues()
    {
        var result = AxisParameterValidator.Validate(new ElementParameterSet
        {
            ElementId = "Axis-0-0",
            Values = new Dictionary<string, CommandValue>
            {
                [AxisParameterKeys.DefaultVelocity] = CommandValue.FromNumber(200),
                [AxisParameterKeys.Acceleration] = CommandValue.FromNumber(0),
                [AxisParameterKeys.MaxVelocity] = CommandValue.FromNumber(100),
                [AxisParameterKeys.SoftLimitMin] = CommandValue.FromNumber(100),
                [AxisParameterKeys.SoftLimitMax] = CommandValue.FromNumber(-100),
                [AxisParameterKeys.PositionScale] = CommandValue.FromNumber(0)
            }
        });

        result.IsValid.Should().BeFalse();
        result.Issues.Select(i => i.Code).Should().Contain(
            "AXIS_PARAM_DEFAULT_EXCEEDS_MAX",
            "AXIS_PARAM_ACCEL_POSITIVE",
            "AXIS_PARAM_SOFT_LIMIT_ORDER",
            "AXIS_PARAM_SCALE_POSITIVE");
    }
}
