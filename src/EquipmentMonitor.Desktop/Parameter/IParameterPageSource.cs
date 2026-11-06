using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Desktop.Parameter;

/// <summary>
/// 参数页的元素条目：左侧列表显示，右侧编辑。
/// </summary>
/// <param name="ElementId">稳定元件标识。</param>
/// <param name="DisplayName">显示名。</param>
/// <param name="ElementType">元件类型（Axis / Cylinder）。</param>
/// <param name="StationId">所属工位；0 表示未分组。</param>
public sealed record ParameterTarget(
    string ElementId,
    string DisplayName,
    string ElementType,
    int StationId);

/// <summary>
/// 参数页的数据源端口：通用参数页只依赖本接口，不直接依赖轴/气缸服务。
/// </summary>
/// <remarks>
/// <para>骨架阶段提供 <see cref="TemplateParameterPageSource"/> 作为内存示例；
/// 真正接入后实现本接口，包装 <c>IAxisParameterService</c> / <c>ICylinderParameterService</c>，
/// 页面与控件不需要改。</para>
/// <para>实现必须负责权限校验、校验问题与审计；UI 只展示结果。</para>
/// </remarks>
public interface IParameterPageSource
{
    /// <summary>数据源提示；为 <c>null</c> 时页面不显示提示条。</summary>
    string? Banner { get; }

    /// <summary>列出可编辑的元件。</summary>
    Task<Result<IReadOnlyList<ParameterTarget>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>读取一个元件的参数快照。</summary>
    Task<Result<ElementParameterSnapshot>> GetAsync(
        string elementId,
        CancellationToken cancellationToken = default);

    /// <summary>保存/更新参数。</summary>
    Task<Result<ElementParameterChangeResult>> SaveAsync(
        string elementId,
        ElementParameterSet values,
        ParameterUpdateMode mode,
        CancellationToken cancellationToken = default);
}
