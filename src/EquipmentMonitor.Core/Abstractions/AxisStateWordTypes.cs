namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// <see cref="IAxisController.ReadStateWord"/> 的 <c>type</c> 参数取值。
/// </summary>
/// <remarks>
/// 用常量而不是枚举：厂商状态字的种类会随控制器增加（状态字、故障码、报警码、
/// 回零状态……），而接口是 <c>string type</c>。常量集中在这里，
/// 至少让“拼错字符串”在编译期暴露，而不是运行期返回 false。
/// </remarks>
public static class AxisStateWordTypes
{
    /// <summary>16 位轴状态字，位定义见 <c>AxisStatusWord</c>。</summary>
    public const string StateWord = "StateWord";

    /// <summary>故障码，0 表示无故障。</summary>
    public const string FaultCode = "FaultCode";
}
