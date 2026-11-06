using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Axis;

/// <summary>
/// 轴参数的领域校验：只校验轴自己的规则，不放进通用 <see cref="ElementParameterDescriptor"/>。
/// </summary>
/// <remarks>
/// 通用描述符只检查“单个参数项的形状”（类型/范围/默认值）；跨字段规则属于轴领域，
/// 由本类负责。气缸有独立的 <c>CylinderParameterValidator</c>，两者不共享规则。
/// </remarks>
public static class AxisParameterValidator
{
    /// <summary>校验轴参数集；不修改输入。</summary>
    public static ElementParameterValidationResult Validate(ElementParameterSet values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var issues = new List<ElementParameterValidationIssue>();
        var defaultVelocity = GetNumber(values, AxisParameterKeys.DefaultVelocity);
        var acceleration = GetNumber(values, AxisParameterKeys.Acceleration);
        var deceleration = GetNumber(values, AxisParameterKeys.Deceleration);
        var jerk = GetNumber(values, AxisParameterKeys.Jerk);
        var maxVelocity = GetNumber(values, AxisParameterKeys.MaxVelocity);
        var softMin = GetNumber(values, AxisParameterKeys.SoftLimitMin);
        var softMax = GetNumber(values, AxisParameterKeys.SoftLimitMax);

        if (defaultVelocity is <= 0)
        {
            Add(issues, AxisParameterKeys.DefaultVelocity, "AXIS_PARAM_VELOCITY_POSITIVE",
                "默认速度必须大于 0。");
        }

        if (acceleration is <= 0)
        {
            Add(issues, AxisParameterKeys.Acceleration, "AXIS_PARAM_ACCEL_POSITIVE",
                "加速度必须大于 0。");
        }

        if (deceleration is <= 0)
        {
            Add(issues, AxisParameterKeys.Deceleration, "AXIS_PARAM_DECEL_POSITIVE",
                "减速度必须大于 0。");
        }

        if (jerk is < 0)
        {
            Add(issues, AxisParameterKeys.Jerk, "AXIS_PARAM_JERK_NON_NEGATIVE",
                "加加速度不能为负数。");
        }

        if (maxVelocity is <= 0)
        {
            Add(issues, AxisParameterKeys.MaxVelocity, "AXIS_PARAM_MAX_VELOCITY_POSITIVE",
                "最大速度必须大于 0。");
        }

        if (defaultVelocity is { } velocity && maxVelocity is { } max && velocity > max)
        {
            Add(issues, AxisParameterKeys.DefaultVelocity, "AXIS_PARAM_DEFAULT_EXCEEDS_MAX",
                $"默认速度 {velocity} 超过最大速度 {max}。");
        }

        if (softMin is { } min && softMax is { } maxLimit && min >= maxLimit)
        {
            Add(issues, AxisParameterKeys.SoftLimitMin, "AXIS_PARAM_SOFT_LIMIT_ORDER",
                $"软限位下限 {min} 必须小于上限 {maxLimit}。");
        }

        // 缩放为 0 会把所有工程值压成 0，属于配置错误；只校验存在的键。
        ValidatePositiveScale(values, AxisParameterKeys.PositionScale, "位置缩放", issues);
        ValidatePositiveScale(values, AxisParameterKeys.VelocityScale, "速度缩放", issues);
        ValidatePositiveScale(values, AxisParameterKeys.TorqueScale, "扭矩缩放", issues);
        ValidatePositiveScale(values, AxisParameterKeys.AccelerationScale, "加速度缩放", issues);
        ValidatePositiveScale(values, AxisParameterKeys.DecelerationScale, "减速度缩放", issues);
        ValidatePositiveScale(values, AxisParameterKeys.JerkScale, "加加速度缩放", issues);

        return new ElementParameterValidationResult(issues);
    }

    private static void ValidatePositiveScale(
        ElementParameterSet values,
        string key,
        string displayName,
        List<ElementParameterValidationIssue> issues)
    {
        if (GetNumber(values, key) is { } scale && scale <= 0)
        {
            Add(issues, key, "AXIS_PARAM_SCALE_POSITIVE", $"{displayName}必须大于 0。");
        }
    }

    private static double? GetNumber(ElementParameterSet values, string key)
        => values.TryGetValue(key, out var value) && value.TryAsNumber(out var number)
            ? number
            : null;

    private static void Add(
        List<ElementParameterValidationIssue> issues,
        string key,
        string code,
        string message)
        => issues.Add(new ElementParameterValidationIssue(key, code, message));
}
