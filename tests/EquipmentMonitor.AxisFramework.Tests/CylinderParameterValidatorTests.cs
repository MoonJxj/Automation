using EquipmentMonitor.Core.Cylinder;
using EquipmentMonitor.Core.Models;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

public class CylinderParameterValidatorTests
{
    [Fact]
    public void Validate_WhenCylinderRulesHold_IsValid()
    {
        var result = CylinderParameterValidator.Validate(new ElementParameterSet
        {
            ElementId = "Cylinder-0-0",
            Values = new Dictionary<string, CommandValue>
            {
                [CylinderParameterKeys.Name] = CommandValue.FromText("取料气缸"),
                [CylinderParameterKeys.ExtendOutput] = CommandValue.FromNumber(1),
                [CylinderParameterKeys.RetractOutput] = CommandValue.FromNumber(2),
                [CylinderParameterKeys.ExtendedInput] = CommandValue.FromNumber(3),
                [CylinderParameterKeys.RetractedInput] = CommandValue.FromNumber(4),
                [CylinderParameterKeys.ActionTimeoutSeconds] = CommandValue.FromNumber(3)
            }
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenOutputsInputsAndTimeoutConflict_ReturnsCylinderSpecificIssues()
    {
        var result = CylinderParameterValidator.Validate(new ElementParameterSet
        {
            ElementId = "Cylinder-0-0",
            Values = new Dictionary<string, CommandValue>
            {
                [CylinderParameterKeys.ExtendOutput] = CommandValue.FromNumber(1),
                [CylinderParameterKeys.RetractOutput] = CommandValue.FromNumber(1),
                [CylinderParameterKeys.ExtendedInput] = CommandValue.FromNumber(3),
                [CylinderParameterKeys.RetractedInput] = CommandValue.FromNumber(3),
                [CylinderParameterKeys.ActionTimeoutSeconds] = CommandValue.FromNumber(0)
            }
        });

        result.IsValid.Should().BeFalse();
        result.Issues.Select(i => i.Code).Should().Contain(
            "CYLINDER_PARAM_OUTPUT_CONFLICT",
            "CYLINDER_PARAM_INPUT_CONFLICT",
            "CYLINDER_PARAM_TIMEOUT_POSITIVE");
    }
}
