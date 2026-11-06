using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 报警历史持久化。
/// </summary>
/// <remarks>
/// 写失败不得影响报警主流程；实现内部吞异常并记日志。
/// 一期用 JSONL（宿主尚未接数据库），二期换 SQLite 时替换实现即可。
/// </remarks>
public interface IAlarmHistoryStore
{
    /// <summary>追加一条记录（同 Id 的后续状态更新也追加新行）。</summary>
    Task AppendAsync(AlarmRecord record, CancellationToken cancellationToken = default);

    /// <summary>按条件查询；实现负责按 Id 折叠到最新状态并按时间倒序。</summary>
    Task<IReadOnlyList<AlarmRecord>> QueryAsync(
        AlarmHistoryQuery query,
        CancellationToken cancellationToken = default);
}
