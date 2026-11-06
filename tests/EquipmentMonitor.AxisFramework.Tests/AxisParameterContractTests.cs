using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

public class AxisParameterContractTests
{
    [Fact]
    public void Catalog_ContainsMotionLimitAndAddressParameters()
    {
        var descriptors = AxisParameterCatalog.Create();

        descriptors.Should().Contain(d => d.Key == AxisParameterKeys.DefaultVelocity);
        descriptors.Should().Contain(d => d.Key == AxisParameterKeys.SoftLimitMin);
        descriptors.Should().Contain(d => d.Key == AxisParameterKeys.PositionScale);
        descriptors.Should().Contain(d => d.Key == AxisParameterKeys.CommandStartAddress);
        descriptors.Select(d => d.Key).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Catalog_MotionDefaultsAreProcessParameters()
    {
        var descriptor = AxisParameterCatalog
            .Create()
            .Single(d => d.Key == AxisParameterKeys.DefaultVelocity);

        descriptor.Scope.Should().Be(ParameterScope.Process);
        descriptor.RequiredPermission.Should().Be(Permission.ProcessConfig);
        descriptor.ApplyMode.Should().Be(ParameterApplyMode.NextCommand);
    }

    [Fact]
    public void Catalog_SoftLimitIsDeviceParameterAndHardwareApplied()
    {
        var descriptor = AxisParameterCatalog
            .Create()
            .Single(d => d.Key == AxisParameterKeys.SoftLimitMin);

        descriptor.Scope.Should().Be(ParameterScope.Device);
        descriptor.RequiredPermission.Should().Be(Permission.MachineConfig);
        descriptor.ApplyMode.Should().Be(ParameterApplyMode.Immediate);
    }

    [Fact]
    public void Descriptor_WhenRangeReversed_Throws()
    {
        var descriptor = new ElementParameterDescriptor
        {
            Key = "Test",
            ValueKind = CommandValueKind.Number,
            Minimum = 10,
            Maximum = 0
        };

        var act = () => descriptor.Validate();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Descriptor_WhenDefaultTypeDiffers_Throws()
    {
        var descriptor = new ElementParameterDescriptor
        {
            Key = "Test",
            ValueKind = CommandValueKind.Number,
            DefaultValue = CommandValue.FromBool(true)
        };

        var act = () => descriptor.Validate();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParameterSet_WithValue_DoesNotMutateOriginal()
    {
        var original = new ElementParameterSet
        {
            ElementId = "Axis-0-0",
            Values = new Dictionary<string, CommandValue>
            {
                [AxisParameterKeys.DefaultVelocity] = CommandValue.FromNumber(10)
            }
        };

        var updated = original.WithValue(AxisParameterKeys.DefaultVelocity, CommandValue.FromNumber(20));

        original.Values[AxisParameterKeys.DefaultVelocity].TryAsNumber(out var oldValue).Should().BeTrue();
        oldValue.Should().Be(10);
        updated.Values[AxisParameterKeys.DefaultVelocity].TryAsNumber(out var newValue).Should().BeTrue();
        newValue.Should().Be(20);
    }

    [Fact]
    public void ValidationResult_WhenNoIssues_IsValid()
    {
        ElementParameterValidationResult.Valid.IsValid.Should().BeTrue();
        new ElementParameterValidationResult(
            [new ElementParameterValidationIssue("Key", "AXIS_PARAM_RANGE", "越界")])
            .IsValid.Should().BeFalse();
    }
}
