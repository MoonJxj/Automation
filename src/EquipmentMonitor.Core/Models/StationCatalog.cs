namespace EquipmentMonitor.Core.Models;

/// <summary>工位定义（骨架阶段固定两个工位；具体设备在实施时替换为配置）。</summary>
/// <param name="Id">工位编号（从 1 开始）。</param>
/// <param name="Name">显示名。</param>
public sealed record StationInfo(int Id, string Name);

/// <summary>
/// 工位目录：骨架阶段固定“工位1 / 工位2”。
/// </summary>
/// <remarks>
/// <para>工位是<b>设备组织维度</b>，不是控制器/轴号：同一工位可以包含伺服、气缸、机械手。
/// 轴/气缸配置通过 <c>StationId</c> 归属到工位，手动操作页据此过滤。</para>
/// <para>具体设备实施时，把 <see cref="All"/> 换成配置/数据库读取即可；
/// 界面只依赖 <see cref="All"/> / <see cref="Name"/>，不硬编码工位数量。</para>
/// </remarks>
public static class StationCatalog
{
    /// <summary>工位1。</summary>
    public const int Station1 = 1;

    /// <summary>工位2。</summary>
    public const int Station2 = 2;

    /// <summary>全部工位（按编号排序）。</summary>
    public static IReadOnlyList<StationInfo> All { get; } =
    [
        new(Station1, "工位1"),
        new(Station2, "工位2")
    ];

    /// <summary>按编号取显示名；未知编号回退为 <c>工位{id}</c>。</summary>
    public static string Name(int id)
    {
        foreach (var station in All)
        {
            if (station.Id == id)
            {
                return station.Name;
            }
        }

        return $"工位{id}";
    }

    /// <summary>编号是否在目录内。</summary>
    public static bool Contains(int id)
    {
        foreach (var station in All)
        {
            if (station.Id == id)
            {
                return true;
            }
        }

        return false;
    }
}
