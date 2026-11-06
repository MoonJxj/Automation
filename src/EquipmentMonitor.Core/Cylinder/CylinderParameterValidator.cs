using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Cylinder;

/// <summary>
/// 气缸参数的领域校验：IO 点冲突、超时、必填名称。
/// </summary>
/// <remarks>
/// 与 <c>AxisParameterValidator</c> 分开：气缸没有速度/软限位/缩放，轴也没有 IO 点冲突。
/// 通用 <see cref="ElementParameterDescriptor"/> 只做单参数形状校验。
/// </remarks>
public static class CylinderParameterValidator
{
    /// <summary>校验气缸参数集；不修改输入。</summary>
    public static ElementParameterValidationResult Validate(ElementParameterSet values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var issues = new List<ElementParameterValidationIssue>();

        if (values.TryGetValue(CylinderParameterKeys.Name, out var nameValue)
            && nameValue.TryAsText(out var name)
            && string.IsNullOrWhiteSpace(name))
        {
            Add(issues, CylinderParameterKeys.Name, "CYLINDER_PARAM_NAME_REQUIRED",
                "气缸名称不能为空。");
        }

        var extendOutput = GetNumber(values, CylinderParameterKeys.ExtendOutput);
        var retractOutput = GetNumber(values, CylinderParameterKeys.RetractOutput);
        var extendedInput = GetNumber(values, CylinderParameterKeys.ExtendedInput);
        var retractedInput = GetNumber(values, CylinderParameterKeys.RetractedInput);
        var timeout = GetNumber(values, CylinderParameterKeys.ActionTimeoutSeconds);

        if (extendOutput is < 0)
        {
            Add(issues, CylinderParameterKeys.ExtendOutput, "CYLINDER_PARAM_OUTPUT_INVALID",
                "伸出电磁阀输出点不能为负数。");
        }

        if (retractOutput is < 0)
        {
            Add(issues, CylinderParameterKeys.RetractOutput, "CYLINDER_PARAM_OUTPUT_INVALID",
                "缩回电磁阀输出点不能为负数。");
        }

        if (extendedInput is < 0)
        {
            Add(issues, CylinderParameterKeys.ExtendedInput, "CYLINDER_PARAM_INPUT_INVALID",
                "伸出限位输入点不能为负数。");
        }

        if (retractedInput is < 0)
        {
            Add(issues, CylinderParameterKeys.RetractedInput, "CYLINDER_PARAM_INPUT_INVALID",
                "缩回限位输入点不能为负数。");
        }

        if (extendOutput is { } extend && retractOutput is { } retract && extend == retract)
        {
            Add(issues, CylinderParameterKeys.RetractOutput, "CYLINDER_PARAM_OUTPUT_CONFLICT",
                "伸出/缩回电磁阀不能使用同一个输出点。");
        }

        if (extendedInput is { } extended && retractedInput is { } retracted && extended == retracted)
        {
            Add(issues, CylinderParameterKeys.RetractedInput, "CYLINDER_PARAM_INPUT_CONFLICT",
                "伸出/缩回限位不能使用同一个输入点。");
        }

        if (timeout is <= 0)
        {
            Add(issues, CylinderParameterKeys.ActionTimeoutSeconds, "CYLINDER_PARAM_TIMEOUT_POSITIVE",
                "动作超时必须大于 0 秒。");
        }

        return new ElementParameterValidationResult(issues);
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
