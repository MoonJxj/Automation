using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Axis;

/// <summary>
/// 当前轴参数目录：声明框架内置的可设计参数。
/// </summary>
/// <remarks>
/// <para>
/// 参数目录是**代码事实**，不是配置文件：键名、类型、范围、权限、生效方式由代码决定；
/// 配置文件只提供值。这样 UI 不会因为现场少填一个字段就少一个输入框。
/// </para>
/// <para>
/// 软限位/最大速度的实时职责仍在轴卡/PLC；这里把它们作为设备参数暴露，
/// 是为了让参数设计页能下发到控制器，而不是让 <c>AxisElement</c> 用 0.1s 轮询拦截。
/// </para>
/// </remarks>
public static class AxisParameterCatalog
{
    /// <summary>生成内置参数描述符；描述符自身非法时抛 <see cref="InvalidOperationException"/>。</summary>
    public static IReadOnlyList<ElementParameterDescriptor> Create()
    {
        var descriptors = new List<ElementParameterDescriptor>
        {
            new()
            {
                Key = AxisParameterKeys.Name,
                Name = "轴名称",
                Group = "身份",
                ValueKind = CommandValueKind.Text,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart,
                Description = "界面显示名；修改后需重新装配/重启生效。"
            },
            new()
            {
                Key = AxisParameterKeys.Unit,
                Name = "工程单位",
                Group = "身份",
                ValueKind = CommandValueKind.Text,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart,
                Description = "mm / deg 等；只影响显示与参数单位，不改变控制器缩放。"
            },
            new()
            {
                Key = AxisParameterKeys.StationId,
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
                Key = AxisParameterKeys.IsEnabled,
                Name = "启用",
                Group = "身份",
                ValueKind = CommandValueKind.Bool,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart,
                Description = "禁用后不参与初始化与轮询。"
            },
            new()
            {
                Key = AxisParameterKeys.DefaultVelocity,
                Name = "默认速度",
                Group = "运动",
                ValueKind = CommandValueKind.Number,
                Unit = "mm/s",
                Precision = 3,
                Minimum = 0.001,
                Scope = ParameterScope.Process,
                RequiredPermission = Permission.ProcessConfig,
                ApplyMode = ParameterApplyMode.NextCommand
            },
            new()
            {
                Key = AxisParameterKeys.Acceleration,
                Name = "默认加速度",
                Group = "运动",
                ValueKind = CommandValueKind.Number,
                Unit = "mm/s²",
                Precision = 3,
                Minimum = 0.001,
                Scope = ParameterScope.Process,
                RequiredPermission = Permission.ProcessConfig,
                ApplyMode = ParameterApplyMode.NextCommand
            },
            new()
            {
                Key = AxisParameterKeys.Deceleration,
                Name = "默认减速度",
                Group = "运动",
                ValueKind = CommandValueKind.Number,
                Unit = "mm/s²",
                Precision = 3,
                Minimum = 0.001,
                Scope = ParameterScope.Process,
                RequiredPermission = Permission.ProcessConfig,
                ApplyMode = ParameterApplyMode.NextCommand
            },
            new()
            {
                Key = AxisParameterKeys.Jerk,
                Name = "默认加加速度",
                Group = "运动",
                ValueKind = CommandValueKind.Number,
                Unit = "mm/s³",
                Precision = 3,
                Minimum = 0,
                Scope = ParameterScope.Process,
                RequiredPermission = Permission.ProcessConfig,
                ApplyMode = ParameterApplyMode.NextCommand,
                Description = "0 表示不使用 S 曲线。"
            },
            new()
            {
                Key = AxisParameterKeys.MaxVelocity,
                Name = "最大速度",
                Group = "限位",
                ValueKind = CommandValueKind.Number,
                Unit = "mm/s",
                Precision = 3,
                Minimum = 0.001,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Immediate
            },
            new()
            {
                Key = AxisParameterKeys.SoftLimitMin,
                Name = "软限位下限",
                Group = "限位",
                ValueKind = CommandValueKind.Number,
                Unit = "mm",
                Precision = 3,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Immediate,
                Description = "由轴卡/PLC 执行；上位机不下发运动拦截。"
            },
            new()
            {
                Key = AxisParameterKeys.SoftLimitMax,
                Name = "软限位上限",
                Group = "限位",
                ValueKind = CommandValueKind.Number,
                Unit = "mm",
                Precision = 3,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Immediate,
                Description = "由轴卡/PLC 执行；上位机不下发运动拦截。"
            },
            new()
            {
                Key = AxisParameterKeys.HomeMethod,
                Name = "回零方式",
                Group = "回零",
                ValueKind = CommandValueKind.Number,
                Precision = 0,
                Minimum = 0,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Immediate
            },
            new()
            {
                Key = AxisParameterKeys.PositionScale,
                Name = "位置缩放",
                Group = "缩放",
                ValueKind = CommandValueKind.Number,
                Precision = 6,
                Minimum = 0.000001,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart
            },
            new()
            {
                Key = AxisParameterKeys.VelocityScale,
                Name = "速度缩放",
                Group = "缩放",
                ValueKind = CommandValueKind.Number,
                Precision = 6,
                Minimum = 0.000001,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart
            },
            new()
            {
                Key = AxisParameterKeys.TorqueScale,
                Name = "扭矩缩放",
                Group = "缩放",
                ValueKind = CommandValueKind.Number,
                Precision = 6,
                Minimum = 0.000001,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart
            },
            new()
            {
                Key = AxisParameterKeys.AccelerationScale,
                Name = "加速度缩放",
                Group = "缩放",
                ValueKind = CommandValueKind.Number,
                Precision = 6,
                Minimum = 0.000001,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart
            },
            new()
            {
                Key = AxisParameterKeys.DecelerationScale,
                Name = "减速度缩放",
                Group = "缩放",
                ValueKind = CommandValueKind.Number,
                Precision = 6,
                Minimum = 0.000001,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart
            },
            new()
            {
                Key = AxisParameterKeys.JerkScale,
                Name = "加加速度缩放",
                Group = "缩放",
                ValueKind = CommandValueKind.Number,
                Precision = 6,
                Minimum = 0.000001,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart
            },
            new()
            {
                Key = AxisParameterKeys.StateStartAddress,
                Name = "状态区起始地址",
                Group = "地址",
                ValueKind = CommandValueKind.Number,
                Precision = 0,
                Minimum = 0,
                Maximum = ushort.MaxValue,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart,
                Description = "仅 Modbus 轴使用；零基地址。"
            },
            new()
            {
                Key = AxisParameterKeys.CommandStartAddress,
                Name = "命令区起始地址",
                Group = "地址",
                ValueKind = CommandValueKind.Number,
                Precision = 0,
                Minimum = 0,
                Maximum = ushort.MaxValue,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart,
                Description = "仅 Modbus 轴使用；零基地址。"
            },
            new()
            {
                Key = AxisParameterKeys.ExecuteCoilAddress,
                Name = "Execute 线圈地址",
                Group = "地址",
                ValueKind = CommandValueKind.Number,
                Precision = 0,
                Minimum = 0,
                Maximum = ushort.MaxValue,
                Scope = ParameterScope.Device,
                RequiredPermission = Permission.MachineConfig,
                ApplyMode = ParameterApplyMode.Restart,
                Description = "仅 Modbus 轴使用；零基地址。"
            }
        };

        foreach (var descriptor in descriptors)
        {
            descriptor.Validate();
        }

        return descriptors;
    }
}
