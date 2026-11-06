using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 设备码表扩展点：每种设备/品牌注册自己的报警码定义。
/// </summary>
/// <remarks>
/// 实现由设备驱动提供（如 <c>AxisAlarmDefinitions</c>、<c>PlcAlarmDefinitions</c>），
/// 通过 DI 注册；<c>AlarmCatalog</c> 合并全部 provider，后注册覆盖先注册。
/// 新增设备不必改界面或报警服务。
/// </remarks>
public interface IAlarmDefinitionProvider
{
    /// <summary>返回本设备提供的全部报警码定义。</summary>
    IEnumerable<AlarmDefinition> GetDefinitions();
}
