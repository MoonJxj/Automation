using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Cylinder;

/// <summary>
/// 气缸参数目录：声明框架内置的可设计参数。
/// </summary>
/// <remarks>
/// <para>
/// 气缸参数主要是设备配置：IO 点号、动作超时、启用状态。与轴参数不同，气缸没有
/// 控制器参数读写能力，当前所有改动都需要重新装配/重启后生效。
/// </para>
/// <para>
/// 参数目录是代码事实，不是配置文件；配置文件只提供值。
/// </para>
/// </remarks>
public static class CylinderParameterCatalog
{
    /// <summary>生成内置参数描述符；描述符自身非法时抛 <see cref="InvalidOperationException"/>。</summary>
    public static IReadOnlyList<ElementParameterDescriptor> Create()
    {
        var descriptors = new List<ElementParameterDescriptor>
        {
            new()
            {
                Key = CylinderParameterKeys.Name,
                Name = "气缸名称",
                Group = "身份",
                ValueKind = CommandValueKind.Text,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart,
                Description = "界面显示名；修改后需重新装配/重启生效。"
            },
            new()
            {
                Key = CylinderParameterKeys.StationId,
                Name = "所属工位",
                Group = "身份",
                ValueKind = CommandValueKind.Number,
                Precision = 0,
                Minimum = 1,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart
            },
            new()
            {
                Key = CylinderParameterKeys.IsEnabled,
                Name = "启用",
                Group = "身份",
                ValueKind = CommandValueKind.Bool,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart,
                Description = "禁用后不参与控制与采集。"
            },
            new()
            {
                Key = CylinderParameterKeys.ExtendOutput,
                Name = "伸出电磁阀输出点",
                Group = "IO 点",
                ValueKind = CommandValueKind.Number,
                Precision = 0,
                Minimum = 0,
                Maximum = ushort.MaxValue,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart
            },
            new()
            {
                Key = CylinderParameterKeys.RetractOutput,
                Name = "缩回电磁阀输出点",
                Group = "IO 点",
                ValueKind = CommandValueKind.Number,
                Precision = 0,
                Minimum = 0,
                Maximum = ushort.MaxValue,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart,
                Description = "不设置表示单控气缸（弹簧复位）；双控时必须与伸出点不同。"
            },
            new()
            {
                Key = CylinderParameterKeys.ExtendedInput,
                Name = "伸出到位限位输入点",
                Group = "IO 点",
                ValueKind = CommandValueKind.Number,
                Precision = 0,
                Minimum = 0,
                Maximum = ushort.MaxValue,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart
            },
            new()
            {
                Key = CylinderParameterKeys.RetractedInput,
                Name = "缩回到位限位输入点",
                Group = "IO 点",
                ValueKind = CommandValueKind.Number,
                Precision = 0,
                Minimum = 0,
                Maximum = ushort.MaxValue,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart,
                Description = "必须与伸出限位使用不同的输入点。"
            },
            new()
            {
                Key = CylinderParameterKeys.ActionTimeoutSeconds,
                Name = "动作超时",
                Group = "动作",
                ValueKind = CommandValueKind.Number,
                Unit = "s",
                Precision = 3,
                Minimum = 0.001,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart,
                Description = "超时后置故障，必须显式复位；按阀门/机械行程留足余量。"
            },
            new()
            {
                Key = CylinderParameterKeys.Description,
                Name = "备注",
                Group = "身份",
                ValueKind = CommandValueKind.Text,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart
            }
        };

        foreach (var descriptor in descriptors)
        {
            descriptor.Validate();
        }

        return descriptors;
    }
}
