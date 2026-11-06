using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Cylinder;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Desktop.Parameter;

/// <summary>
/// 模板用内存参数源：让参数页在没有后端服务时也能预览、编辑、保存。
/// </summary>
/// <remarks>
/// <para><b>这是骨架示例，不是生产存储</b>：值只在本进程内存中，重启丢失；
/// 接入真实参数服务后，用 <c>IAxisParameterService</c> / <c>ICylinderParameterService</c>
/// 的适配实现替换 DI 注册即可。</para>
/// <para>参数目录来自 Core，不在 UI 里硬编码字段；新增参数只要扩展目录。</para>
/// </remarks>
public sealed class TemplateParameterPageSource : IParameterPageSource
{
    private readonly object _gate = new();
    private readonly List<ParameterTarget> _targets =
    [
        new("Axis-0-0", "X 轴（工位1·模拟）", "Axis", 1),
        new("Axis-0-1", "Y 轴（工位2·模拟）", "Axis", 2),
        new("Cyl-1", "夹紧气缸（工位1·模拟）", "Cylinder", 1),
        new("Cyl-2", "顶升气缸（工位2·模拟）", "Cylinder", 2)
    ];

    private readonly Dictionary<string, ElementParameterSet> _values = new(StringComparer.Ordinal);

    /// <summary>创建模板源并生成示例值。</summary>
    public TemplateParameterPageSource()
    {
        foreach (var target in _targets.Where(item => item.ElementType == "Axis"))
        {
            _values[target.ElementId] = CreateAxisValues(target);
        }

        foreach (var target in _targets.Where(item => item.ElementType == "Cylinder"))
        {
            _values[target.ElementId] = CreateCylinderValues(target);
        }
    }

    /// <inheritdoc />
    public string? Banner => "模板示例：内存数据，未持久化；接入真实参数服务后替换 IParameterPageSource 实现。";

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<ParameterTarget>>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Result.Ok<IReadOnlyList<ParameterTarget>>(_targets.ToArray()));
    }

    /// <inheritdoc />
    public Task<Result<ElementParameterSnapshot>> GetAsync(
        string elementId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var target = _targets.FirstOrDefault(item => item.ElementId == elementId);

            if (target is null)
            {
                return Task.FromResult(Result.Fail<ElementParameterSnapshot>(
                    $"参数源中没有元件 {elementId}。",
                    "PARAM_TARGET_NOT_FOUND"));
            }

            if (!_values.TryGetValue(elementId, out var values))
            {
                return Task.FromResult(Result.Fail<ElementParameterSnapshot>(
                    $"元件 {elementId} 没有参数值。",
                    "PARAM_VALUES_NOT_FOUND"));
            }

            var descriptors = target.ElementType == "Axis"
                ? AxisParameterCatalog.Create()
                : CylinderParameterCatalog.Create();

            return Task.FromResult(Result.Ok(new ElementParameterSnapshot(
                target.ElementId,
                target.DisplayName,
                descriptors,
                values,
                null,
                DateTimeOffset.Now)));
        }
    }

    /// <inheritdoc />
    public Task<Result<ElementParameterChangeResult>> SaveAsync(
        string elementId,
        ElementParameterSet values,
        ParameterUpdateMode mode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_values.TryGetValue(elementId, out var current))
            {
                return Task.FromResult(Result.Fail<ElementParameterChangeResult>(
                    $"参数源中没有元件 {elementId}。",
                    "PARAM_TARGET_NOT_FOUND"));
            }

            var changed = values.Values
                .Where(pair => !current.Values.TryGetValue(pair.Key, out var old) || old != pair.Value)
                .Select(pair => pair.Key)
                .ToArray();

            var target = _targets.First(item => item.ElementId == elementId);
            var descriptors = target.ElementType == "Axis"
                ? AxisParameterCatalog.Create()
                : CylinderParameterCatalog.Create();

            var restart = changed.Any(key =>
                descriptors.FirstOrDefault(item => item.Key == key)?.ApplyMode == ParameterApplyMode.Restart);

            var persisted = mode is ParameterUpdateMode.SaveOnly or ParameterUpdateMode.SaveAndApply;
            var applied = mode is ParameterUpdateMode.ApplyOnly or ParameterUpdateMode.SaveAndApply;

            if (persisted)
            {
                _values[elementId] = values with { Version = NextVersion(current.Version) };
            }

            var result = new ElementParameterChangeResult(
                _values[elementId],
                changed,
                persisted,
                applied,
                restart);

            return Task.FromResult(Result.Ok(result));
        }
    }

    private static ElementParameterSet CreateAxisValues(ParameterTarget target)
    {
        var descriptors = AxisParameterCatalog.Create();
        var values = new Dictionary<string, CommandValue>(StringComparer.Ordinal);

        foreach (var descriptor in descriptors)
        {
            values[descriptor.Key] = AxisValue(target, descriptor);
        }

        return new ElementParameterSet { ElementId = target.ElementId, Values = values, Version = "1" };
    }

    private static ElementParameterSet CreateCylinderValues(ParameterTarget target)
    {
        var descriptors = CylinderParameterCatalog.Create();
        var values = new Dictionary<string, CommandValue>(StringComparer.Ordinal);

        foreach (var descriptor in descriptors)
        {
            values[descriptor.Key] = CylinderValue(target, descriptor);
        }

        return new ElementParameterSet { ElementId = target.ElementId, Values = values, Version = "1" };
    }

    private static CommandValue AxisValue(ParameterTarget target, ElementParameterDescriptor descriptor) => descriptor.Key switch
    {
        AxisParameterKeys.Name => CommandValue.FromText(target.DisplayName),
        AxisParameterKeys.Unit => CommandValue.FromText("mm"),
        AxisParameterKeys.StationId => CommandValue.FromNumber(target.StationId),
        AxisParameterKeys.IsEnabled => CommandValue.FromBool(true),
        AxisParameterKeys.DefaultVelocity => CommandValue.FromNumber(10),
        AxisParameterKeys.Acceleration => CommandValue.FromNumber(100),
        AxisParameterKeys.Deceleration => CommandValue.FromNumber(100),
        AxisParameterKeys.Jerk => CommandValue.FromNumber(0),
        AxisParameterKeys.MaxVelocity => CommandValue.FromNumber(1000),
        AxisParameterKeys.SoftLimitMin => CommandValue.FromNumber(-100),
        AxisParameterKeys.SoftLimitMax => CommandValue.FromNumber(100),
        AxisParameterKeys.HomeMethod => CommandValue.FromNumber(0),
        _ when descriptor.Key.EndsWith("Scale", StringComparison.Ordinal) => CommandValue.FromNumber(1),
        _ => CommandValue.FromNumber(descriptor.Minimum ?? 0)
    };

    private static CommandValue CylinderValue(ParameterTarget target, ElementParameterDescriptor descriptor) => descriptor.Key switch
    {
        CylinderParameterKeys.Name => CommandValue.FromText(target.DisplayName),
        CylinderParameterKeys.StationId => CommandValue.FromNumber(target.StationId),
        CylinderParameterKeys.IsEnabled => CommandValue.FromBool(true),
        CylinderParameterKeys.ExtendOutput => CommandValue.FromNumber(target.StationId == 1 ? 0 : 2),
        CylinderParameterKeys.RetractOutput => CommandValue.FromNumber(target.StationId == 1 ? 1 : 3),
        CylinderParameterKeys.ExtendedInput => CommandValue.FromNumber(target.StationId == 1 ? 0 : 2),
        CylinderParameterKeys.RetractedInput => CommandValue.FromNumber(target.StationId == 1 ? 1 : 3),
        CylinderParameterKeys.ActionTimeoutSeconds => CommandValue.FromNumber(2),
        CylinderParameterKeys.Description => CommandValue.FromText("模板示例"),
        _ => CommandValue.FromNumber(descriptor.Minimum ?? 0)
    };

    private static string NextVersion(string? version)
        => int.TryParse(version, out var number) ? (number + 1).ToString() : "1";
}
