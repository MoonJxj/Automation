using EquipmentMonitor.Core.Cylinder;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

public class CylinderParameterContractTests
{
    [Fact]
    public void Catalog_ContainsIoTimeoutAndIdentityParameters()
    {
        var descriptors = CylinderParameterCatalog.Create();

        descriptors.Should().Contain(d => d.Key == CylinderParameterKeys.ExtendOutput);
        descriptors.Should().Contain(d => d.Key == CylinderParameterKeys.RetractOutput);
        descriptors.Should().Contain(d => d.Key == CylinderParameterKeys.ExtendedInput);
        descriptors.Should().Contain(d => d.Key == CylinderParameterKeys.RetractedInput);
        descriptors.Should().Contain(d => d.Key == CylinderParameterKeys.ActionTimeoutSeconds);
        descriptors.Select(d => d.Key).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Catalog_CylinderParametersAreDeviceConfigAndRequireRestart()
    {
        foreach (var descriptor in CylinderParameterCatalog.Create())
        {
            descriptor.Scope.Should().Be(ParameterScope.Device);
            descriptor.RequiredPermission.Should().Be(Permission.MachineConfig);
            descriptor.ApplyMode.Should().Be(ParameterApplyMode.Restart);
        }
    }

    [Fact]
    public void Catalog_RetractOutputExplainsSingleActing()
    {
        var descriptor = CylinderParameterCatalog
            .Create()
            .Single(d => d.Key == CylinderParameterKeys.RetractOutput);

        descriptor.Description.Should().Contain("单控");
        descriptor.Minimum.Should().Be(0);
    }

    [Fact]
    public void Catalog_ActionTimeoutHasPositiveMinimum()
    {
        var descriptor = CylinderParameterCatalog
            .Create()
            .Single(d => d.Key == CylinderParameterKeys.ActionTimeoutSeconds);

        descriptor.Minimum.Should().BePositive();
        descriptor.Unit.Should().Be("s");
        descriptor.ValueKind.Should().Be(CommandValueKind.Number);
    }
}
