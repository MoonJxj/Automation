using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 报警码表：把 <see cref="AlarmSignal"/> 解析成等级/文本/建议。
/// </summary>
public interface IAlarmCatalog
{
    /// <summary>查找定义；未注册时返回通用回退定义（永不返回 <c>null</c>）。</summary>
    AlarmDefinition Resolve(AlarmSignal signal);
}
