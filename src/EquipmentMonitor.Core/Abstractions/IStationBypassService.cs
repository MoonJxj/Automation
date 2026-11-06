using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 工位屏蔽：让自动循环跳过某个工位（维修、缺料、单工位调试）。
/// </summary>
/// <remarks>
/// <para><b>权限</b>：<see cref="SetAsync"/> 入口必须
/// <c>RequirePermission(Permission.MachineConfig)</c>（失败抛异常）；读取状态不额外校验，
/// 页面本身已由 <c>MachineView</c> 控制。</para>
/// <para><b>审计</b>：每次实际变更写一条
/// <see cref="AuditEventTypes.StationBypassChanged"/>，内容包括工位、目标状态与原因；
/// 审计失败不回滚（见 <see cref="IAuditService"/>）。</para>
/// <para><b>生效方式</b>：本契约只表达“状态”，不规定下发路径。
/// 接设备的实现应在返回成功前完成 PLC/配置写入并回读，而不是先返回成功再异步补写。</para>
/// <para><b>线程</b>：<see cref="GetStates"/> 可在任意线程调用，返回不可变快照。</para>
/// </remarks>
public interface IStationBypassService
{
    /// <summary>读取全部工位的屏蔽状态（按工位编号排序）。</summary>
    IReadOnlyList<StationBypassState> GetStates();

    /// <summary>设置工位屏蔽状态。</summary>
    /// <param name="stationId">工位编号，必须在 <see cref="StationCatalog"/> 内。</param>
    /// <param name="bypassed"><c>true</c> = 屏蔽；<c>false</c> = 恢复。</param>
    /// <param name="reason">原因；屏蔽时必填，恢复时可为空。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>参数非法时返回失败；权限不足抛 <see cref="UnauthorizedAccessException"/>。</returns>
    Task<Result> SetAsync(
        int stationId,
        bool bypassed,
        string reason,
        CancellationToken cancellationToken = default);
}
