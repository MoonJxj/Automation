namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 工位屏蔽状态的不可变快照：某工位是否在自动循环中被跳过、为什么、谁改的、何时改的。
/// </summary>
/// <remarks>
/// <para>未修改过的工位：<see cref="IsBypassed"/> 为 <c>false</c>，
/// <see cref="Reason"/> / <see cref="OperatorName"/> 为空串，
/// <see cref="ChangedAt"/> 为 <see cref="DateTimeOffset.MinValue"/>，界面据此显示“从未修改”。</para>
/// <para>快照会被 UI 线程与后台线程同时读取，因此不可变；写入由
/// <c>IStationBypassService</c> 串行化，读取方拿到的是数组副本。</para>
/// </remarks>
/// <param name="StationId">工位编号，见 <see cref="StationCatalog"/>。</param>
/// <param name="IsBypassed">是否跳过该工位。</param>
/// <param name="Reason">变更原因（屏蔽时必填）。</param>
/// <param name="OperatorName">操作者显示名；取自变更时的会话。</param>
/// <param name="ChangedAt">最近一次变更时间。</param>
public sealed record StationBypassState(
    int StationId,
    bool IsBypassed,
    string Reason,
    string OperatorName,
    DateTimeOffset ChangedAt)
{
    /// <summary>工位显示名（来自 <see cref="StationCatalog"/>）。</summary>
    public string StationName => StationCatalog.Name(StationId);
}
