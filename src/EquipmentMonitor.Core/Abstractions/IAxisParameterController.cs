using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 控制器/驱动器侧的可选参数读写能力。
/// </summary>
/// <remarks>
/// <para><b>为什么不加进 <see cref="IAxisController"/></b>：厂商 DLL 的参数能力差异极大，
/// 有的只能读状态，有的能设速度/增益，有的只能通过 PLC 寄存器改。把参数方法塞进基础轴接口，
/// 会逼所有实现（包括不支持的）写一堆 <c>NotSupportedException</c>。</para>
/// <para>本接口是能力探测点：服务发现控制器实现了它，才下发参数；否则只能保存配置、
/// 等重启/重新装配生效。</para>
/// </remarks>
public interface IAxisParameterController
{
    /// <summary>控制器支持的参数键集合。服务只下发此集合内的键。</summary>
    IReadOnlySet<string> SupportedParameterKeys { get; }

    /// <summary>读取控制器侧当前参数值；不支持的键可以省略。</summary>
    Task<Result<IReadOnlyDictionary<string, CommandValue>>> ReadParametersAsync(
        int controllerNo,
        int axisNo,
        CancellationToken cancellationToken = default);

    /// <summary>写入控制器侧参数值。只应传 <see cref="SupportedParameterKeys"/> 内的键。</summary>
    Task<Result> WriteParametersAsync(
        int controllerNo,
        int axisNo,
        IReadOnlyDictionary<string, CommandValue> values,
        CancellationToken cancellationToken = default);
}
