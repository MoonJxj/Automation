using EquipmentMonitor.Core;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

public class AxisFeedbackExtensionsTests
{
    [Fact]
    public void ToAxisStatus_WhenMoving_ReportsMovingAndDerivesDiscreteState()
    {
        var feedback = new AxisFeedback
        {
            ControllerNo = 0,
            AxisNo = 0,
            ActualPosition = 12.5,
            ActualVelocity = 100.0,
            ActualTorque = 3.0,
            StatusWord = AxisStatusWord.Enabled | AxisStatusWord.Moving | AxisStatusWord.Positioning,
            FaultCode = 0
        };

        var status = feedback.ToAxisStatus(targetPosition: 20.0);

        status.IsMoving.Should().BeTrue();
        status.IsStandstill.Should().BeFalse();
        status.InPosition.Should().BeFalse();
        status.TargetPosition.Should().Be(20.0);
        status.UniaxialState.Should().Be(UniaxialState.Discrete);
    }

    [Fact]
    public void ToAxisStatus_WhenFaulted_ReportsErrorstopAndFaultCode()
    {
        var feedback = new AxisFeedback
        {
            ControllerNo = 0,
            AxisNo = 0,
            ActualPosition = 0,
            ActualVelocity = 0,
            ActualTorque = 0,
            StatusWord = AxisStatusWord.Enabled | AxisStatusWord.Fault,
            FaultCode = 17
        };

        var status = feedback.ToAxisStatus(0);

        status.IsFault.Should().BeTrue();
        status.IsErrorstop.Should().BeTrue();
        status.FaultCode.Should().Be(17);
        status.UniaxialState.Should().Be(UniaxialState.Errorstop);
    }

    [Fact]
    public void ToAxisStatus_WhenAlarmBit_ReportsWarningWithoutFault()
    {
        var feedback = new AxisFeedback
        {
            ControllerNo = 0,
            AxisNo = 0,
            ActualPosition = 0,
            ActualVelocity = 0,
            ActualTorque = 0,
            StatusWord = AxisStatusWord.Enabled | AxisStatusWord.Alarm,
            FaultCode = 0
        };

        var status = feedback.ToAxisStatus(0);

        status.IsWarning.Should().BeTrue();
        status.IsFault.Should().BeFalse();
        status.UniaxialState.Should().Be(UniaxialState.Standstill);
    }

    [Fact]
    public void ToAxisStatus_WhenEnabledAndIdle_ReportsStandstillAndHome()
    {
        var feedback = new AxisFeedback
        {
            ControllerNo = 0,
            AxisNo = 0,
            ActualPosition = 0,
            ActualVelocity = 0,
            ActualTorque = 0,
            StatusWord = AxisStatusWord.Enabled | AxisStatusWord.HomeCompleted | AxisStatusWord.InPosition,
            FaultCode = 0
        };

        var status = feedback.ToAxisStatus(0);

        status.IsHome.Should().BeTrue();
        status.IsStandstill.Should().BeTrue();
        status.InPosition.Should().BeTrue();
        status.UniaxialState.Should().Be(UniaxialState.Standstill);
    }
}
