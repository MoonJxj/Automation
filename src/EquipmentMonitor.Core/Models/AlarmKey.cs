namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 报警的去重键：同一来源 + 同一码 + 同一原始码 = 同一条报警。
/// </summary>
/// <remarks>
/// 把 <see cref="RawCode"/> 放进键，是为了让「故障码 16 → 17」产生新记录而不是
/// 静默改写同一条：现场需要知道到底报过哪些码。
/// </remarks>
/// <param name="SourceId">具体来源标识，如 <c>axis:1:0</c>、<c>cylinder:Cyl-1</c>。</param>
/// <param name="Code">稳定的报警码，如 <c>AXIS.FAULT</c>。</param>
/// <param name="RawCode">设备原始码；没有则为 <c>null</c>。</param>
public readonly record struct AlarmKey(string SourceId, string Code, int? RawCode = null);
