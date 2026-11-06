using System.Globalization;
using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 元件的静态元数据（<b>共有属性</b>），与运行时的动态值分离。
/// </summary>
/// <remarks>
/// <para><b>为什么要把「共有属性」与「特有属性」分开：</b></para>
/// <list type="bullet">
///   <item>
///     <b>共有属性</b>（本类）—— 所有元件都有，例如伺服和气缸都有
///     <see cref="ElementId"/>、<see cref="ChannelId"/>、<see cref="PollingIntervalMs"/>。
///     存入 SQLite，供大数据平台直接查询（无需联上位机）。
///   </item>
///   <item>
///     <b>特有属性</b>（<see cref="Attributes"/>）—— 伺服的「额定扭矩」、
///     传感器的「量程」。不同元件差异极大，用字典承载。
///   </item>
/// </list>
/// <para>
/// 之所以不把特有属性也做成强类型：那需要为每种元件建一张表，
/// 而元件的种类与属性会随现场变化无限增长。字典 + 文档约定更实际。
/// 代价是失去编译期检查，因此<b>取值一律走 <see cref="GetAttribute"/> 并处理缺省</b>。
/// </para>
/// </remarks>
public sealed record ElementMetadata
{
    /// <summary>元件标识。机台内唯一，用于动态获取该元件的更多信息。</summary>
    public required string ElementId { get; init; }

    /// <summary>元件类型。</summary>
    public required string ElementType { get; init; }

    /// <summary>元件显示名。</summary>
    public required string Name { get; init; }

    /// <summary>描述。</summary>
    public string? Description { get; init; }

    /// <summary>是否启用。禁用的元件不参与轮询，也不影响机台状态聚合。</summary>
    public bool IsEnabled { get; init; } = true;
}
