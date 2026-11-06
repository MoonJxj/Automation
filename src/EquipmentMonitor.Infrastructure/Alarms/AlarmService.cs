using System.Globalization;
using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Events;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace EquipmentMonitor.Infrastructure.Alarms;

/// <summary>
/// 报警服务的唯一实现：统一的报警事实、确认、历史与事件。
/// </summary>
/// <remarks>
/// <para><b>单一入口</b>：设备通过 <see cref="IAlarmSource"/>（持续状态）或
/// <see cref="Report"/>（瞬时事件）进入；界面与历史只读 <see cref="Snapshot"/> /
/// <see cref="QueryHistoryAsync"/>。任何地方都不再维护第二份报警集合。</para>
/// <para><b>拉模型 + diff</b>：每轮读取全部源，按 <see cref="AlarmKey"/> 做上升沿/恢复；
/// 源不可读时冻结该组已有报警，并把“读不到”本身报成 <c>COMM.SOURCE_OFFLINE</c>。</para>
/// <para><b>线程</b>：内存状态由单一锁保护；锁内不做 I/O、不触发事件；
/// <see cref="Changed"/> 在调用线程（后台采集线程或命令线程）触发，订阅方自行切回。</para>
/// <para><b>失败</b>：历史与审计写入失败只记日志，不影响报警结果。</para>
/// </remarks>
public sealed class AlarmService : IAlarmService, IDisposable
{
    /// <summary>报警服务自身生成的“源离线”码。</summary>
    public const string SourceOfflineCode = "COMM.SOURCE_OFFLINE";

    private static readonly ILogger Logger = Log.ForContext<AlarmService>();

    private readonly IReadOnlyList<IAlarmSource> _sources;
    private readonly IAlarmCatalog _catalog;
    private readonly IPermissionService _permissions;
    private readonly IAuditService _audit;
    private readonly IAlarmHistoryStore _history;
    private readonly AlarmOptions _options;
    private readonly ISessionProvider? _session;
    private readonly TimeProvider _timeProvider;

    private readonly object _gate = new();
    private readonly Dictionary<AlarmKey, AlarmRecord> _active = new();
    private readonly Dictionary<AlarmKey, (AlarmRecord Record, DateTimeOffset ExpiresAt)> _recentlyCleared = new();
    private readonly Dictionary<AlarmKey, DateTimeOffset> _transientExpiry = new();
    private readonly Dictionary<string, SourceState> _sourceStates = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _evaluateGate = new(1, 1);

    private AlarmSnapshot _snapshot = AlarmSnapshot.Empty;
    private bool _disposed;

    /// <summary>创建报警服务。</summary>
    /// <param name="sources">持续型报警源；没有源时服务仍可用（只处理瞬时上报）。</param>
    /// <param name="catalog">报警码表。</param>
    /// <param name="permissions">权限服务：确认入口校验。</param>
    /// <param name="audit">审计服务：确认动作留痕。</param>
    /// <param name="history">历史存储。</param>
    /// <param name="options">报警配置。</param>
    /// <param name="session">会话；确认人从当前会话补齐，测试可传 <c>null</c>。</param>
    /// <param name="timeProvider">时间源；测试可注入固定时间。</param>
    public AlarmService(
        IEnumerable<IAlarmSource> sources,
        IAlarmCatalog catalog,
        IPermissionService permissions,
        IAuditService audit,
        IAlarmHistoryStore history,
        IOptions<AlarmOptions> options,
        ISessionProvider? session = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(options);

        _sources = sources.ToArray();
        _catalog = catalog;
        _permissions = permissions;
        _audit = audit;
        _history = history;
        _options = options.Value;
        _session = session;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public AlarmSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<AlarmChangedEventArgs>? Changed;

    /// <inheritdoc />
    public async Task EvaluateAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _evaluateGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var readings = new List<(IAlarmSource Source, AlarmSourceReading Reading)>(_sources.Count);

            foreach (var source in _sources)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    readings.Add((source, await source.ReadAsync(cancellationToken).ConfigureAwait(false)));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // 单源失败隔离：其余源照常评估。
                    Logger.Warning(ex, "读取报警源 {SourceId} 失败。", source.SourceId);
                    readings.Add((source, AlarmSourceReading.Unavailable(ex.Message)));
                }
            }

            ApplyReadings(readings);
        }
        finally
        {
            _evaluateGate.Release();
        }
    }

    /// <inheritdoc />
    public Result Report(AlarmSignal signal)
    {
        if (signal is null)
        {
            return Result.Fail("报警数据为空。", "Alarm.NullSignal");
        }

        if (string.IsNullOrWhiteSpace(signal.SourceId))
        {
            return Result.Fail("报警 SourceId 为空。", "Alarm.InvalidSource");
        }

        if (string.IsNullOrWhiteSpace(signal.Code))
        {
            return Result.Fail("报警码为空。", "Alarm.InvalidCode");
        }

        var events = new List<(AlarmChangeKind Kind, AlarmRecord Record)>();
        var toPersist = new List<AlarmRecord>();
        AlarmSnapshot snapshot;

        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            var key = new AlarmKey(signal.SourceId, signal.Code, signal.RawCode);

            Upsert(key, signal, now, incrementOnRepeat: true, events, toPersist);

            if (_active.ContainsKey(key))
            {
                // 瞬时报警没有“源恢复”这条路径：有 AutoClearAfter 就到期自动恢复，
                // 没有就由人工确认归档（MaxValue 只作为“这是瞬时报警”的标记）。
                _transientExpiry[key] = signal.AutoClearAfter is { } delay && delay > TimeSpan.Zero
                    ? now + delay
                    : DateTimeOffset.MaxValue;
            }

            snapshot = BuildSnapshot(now);
            _snapshot = snapshot;
        }

        Publish(events, snapshot);
        PersistAll(toPersist);

        return Result.Ok();
    }

    /// <inheritdoc />
    public Result Acknowledge(string alarmId)
    {
        if (string.IsNullOrWhiteSpace(alarmId))
        {
            return Result.Fail("报警 Id 为空。", "Alarm.InvalidId");
        }

        _permissions.RequirePermission(Permission.AlarmAcknowledge);

        var events = new List<(AlarmChangeKind Kind, AlarmRecord Record)>();
        var toPersist = new List<AlarmRecord>();
        AlarmSnapshot snapshot;

        lock (_gate)
        {
            if (!TryFindById(alarmId, out var key, out var record))
            {
                return Result.Fail("报警不存在或已归档。", "Alarm.NotFound");
            }

            // 幂等：已确认不重复写审计/历史。
            if (record.AcknowledgedAt is not null)
            {
                return Result.Ok();
            }

            var now = _timeProvider.GetUtcNow();
            var acknowledged = record with
            {
                AcknowledgedAt = now,
                AcknowledgedBy = CurrentOperator()
            };

            if (acknowledged.ClearedAt is not null || _transientExpiry.ContainsKey(key))
            {
                // 瞬时报警没有源来“恢复”，确认即归档；已恢复未确认的记录确认后也归档。
                acknowledged = acknowledged with { ClearedAt = acknowledged.ClearedAt ?? now };
                _active.Remove(key);
                _transientExpiry.Remove(key);
                _recentlyCleared[key] = (acknowledged, now + ChatterWindow);
            }
            else
            {
                _active[key] = acknowledged;
            }

            events.Add((AlarmChangeKind.Acknowledged, acknowledged));
            toPersist.Add(acknowledged);
            snapshot = BuildSnapshot(now);
            _snapshot = snapshot;
        }

        Publish(events, snapshot);
        PersistAll(toPersist);
        QueueAudit(
            AuditEventTypes.AlarmAcknowledged,
            $"确认报警 {alarmId}（{events[0].Record.Code}）");

        return Result.Ok();
    }

    /// <inheritdoc />
    public Result AcknowledgeAll(string? sourceId = null)
    {
        _permissions.RequirePermission(Permission.AlarmAcknowledge);

        var events = new List<(AlarmChangeKind Kind, AlarmRecord Record)>();
        var toPersist = new List<AlarmRecord>();
        AlarmSnapshot snapshot;
        var count = 0;

        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            var operatorName = CurrentOperator();

            foreach (var key in _active.Keys.ToArray())
            {
                var record = _active[key];

                if (record.AcknowledgedAt is not null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(sourceId) && !MatchesSource(record, sourceId))
                {
                    continue;
                }

                var acknowledged = record with
                {
                    AcknowledgedAt = now,
                    AcknowledgedBy = operatorName
                };

                if (acknowledged.ClearedAt is not null || _transientExpiry.ContainsKey(key))
                {
                    acknowledged = acknowledged with { ClearedAt = acknowledged.ClearedAt ?? now };
                    _active.Remove(key);
                    _transientExpiry.Remove(key);
                    _recentlyCleared[key] = (acknowledged, now + ChatterWindow);
                }
                else
                {
                    _active[key] = acknowledged;
                }

                events.Add((AlarmChangeKind.Acknowledged, acknowledged));
                toPersist.Add(acknowledged);
                count++;
            }

            snapshot = BuildSnapshot(now);
            _snapshot = snapshot;
        }

        Publish(events, snapshot);
        PersistAll(toPersist);

        if (count > 0)
        {
            QueueAudit(AuditEventTypes.AlarmAcknowledgedAll, $"批量确认报警 {count} 条。");
        }

        return Result.Ok();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AlarmRecord>> QueryHistoryAsync(
        AlarmHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _history.QueryAsync(query, cancellationToken);
    }

    /// <summary>释放评估信号量。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _evaluateGate.Dispose();
    }

    // ───────────────────────── 内部实现 ─────────────────────────

    private void ApplyReadings(List<(IAlarmSource Source, AlarmSourceReading Reading)> readings)
    {
        var events = new List<(AlarmChangeKind Kind, AlarmRecord Record)>();
        var toPersist = new List<AlarmRecord>();
        AlarmSnapshot snapshot;

        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            CleanupExpired(now, events, toPersist);

            foreach (var (source, reading) in readings)
            {
                var state = GetOrCreateSourceState(source.SourceId);

                if (!reading.IsAvailable)
                {
                    // 启动宽限：从未成功读过的源不报离线，避免初始化顺序造成“满屏离线”。
                    if (!state.HasEverSucceeded)
                    {
                        continue;
                    }

                    state.IsOffline = true;
                    Upsert(
                        new AlarmKey(source.SourceId, SourceOfflineCode),
                        new AlarmSignal
                        {
                            SourceId = source.SourceId,
                            SourceKind = source.Kind,
                            Code = SourceOfflineCode,
                            Level = AlarmLevel.Error,
                            Detail = reading.Error
                        },
                        now,
                        incrementOnRepeat: false,
                        events,
                        toPersist);

                    // 冻结：不清除该源已有报警，防止断线时误以为设备恢复。
                    continue;
                }

                state.HasEverSucceeded = true;

                if (state.IsOffline)
                {
                    state.IsOffline = false;
                    ClearKey(new AlarmKey(source.SourceId, SourceOfflineCode), now, events, toPersist);
                }

                var currentKeys = new HashSet<AlarmKey>();

                foreach (var raw in reading.Alarms)
                {
                    var signal = Normalize(raw, source);
                    var key = new AlarmKey(signal.SourceId, signal.Code, signal.RawCode);

                    currentKeys.Add(key);
                    Upsert(key, signal, now, incrementOnRepeat: false, events, toPersist);
                }

                // 该源组中不再出现的记录 → 恢复。冻结期间不会走到这里。
                var missing = _active.Keys
                    .Where(key => BelongsTo(key, source.SourceId) && !currentKeys.Contains(key))
                    .ToArray();

                foreach (var key in missing)
                {
                    ClearKey(key, now, events, toPersist);
                }
            }

            snapshot = BuildSnapshot(now);
            _snapshot = snapshot;
        }

        Publish(events, snapshot);
        PersistAll(toPersist);
    }

    private void Upsert(
        AlarmKey key,
        AlarmSignal signal,
        DateTimeOffset now,
        bool incrementOnRepeat,
        List<(AlarmChangeKind Kind, AlarmRecord Record)> events,
        List<AlarmRecord> toPersist)
    {
        var definition = _catalog.Resolve(signal);

        if (_active.TryGetValue(key, out var existing))
        {
            if (existing.ClearedAt is not null)
            {
                // 恢复未确认的记录再次发生：原地复活，保留 Id 与历史。
                var reactivated = existing with
                {
                    ClearedAt = null,
                    LastSeenAt = now,
                    RaiseCount = existing.RaiseCount + 1,
                    Title = definition.Title,
                    Message = FormatMessage(definition, signal),
                    Level = definition.Level,
                    Remedy = definition.Remedy,
                    ElementId = signal.ElementId ?? existing.ElementId,
                    ElementName = signal.ElementName ?? existing.ElementName
                };

                _active[key] = reactivated;
                events.Add((AlarmChangeKind.Raised, reactivated));
                toPersist.Add(reactivated);
                return;
            }

            var updated = existing with
            {
                LastSeenAt = now,
                RaiseCount = incrementOnRepeat ? existing.RaiseCount + 1 : existing.RaiseCount,
                Title = definition.Title,
                Message = FormatMessage(definition, signal),
                Level = definition.Level,
                Remedy = definition.Remedy,
                ElementId = signal.ElementId ?? existing.ElementId,
                ElementName = signal.ElementName ?? existing.ElementName
            };

            _active[key] = updated;

            var changed = incrementOnRepeat
                          || updated.Message != existing.Message
                          || updated.Level != existing.Level
                          || updated.Title != existing.Title;

            if (changed)
            {
                events.Add((AlarmChangeKind.Updated, updated));
                toPersist.Add(updated);
            }

            return;
        }

        if (_recentlyCleared.TryGetValue(key, out var cached))
        {
            if (cached.ExpiresAt > now)
            {
                // 抖动窗口内复用原记录：Id 不变，但必须清空上一次的确认信息。
                _recentlyCleared.Remove(key);

                var reactivated = cached.Record with
                {
                    ClearedAt = null,
                    AcknowledgedAt = null,
                    AcknowledgedBy = null,
                    LastSeenAt = now,
                    RaiseCount = cached.Record.RaiseCount + 1,
                    Title = definition.Title,
                    Message = FormatMessage(definition, signal),
                    Level = definition.Level,
                    Remedy = definition.Remedy,
                    ElementId = signal.ElementId ?? cached.Record.ElementId,
                    ElementName = signal.ElementName ?? cached.Record.ElementName
                };

                _active[key] = reactivated;
                events.Add((AlarmChangeKind.Raised, reactivated));
                toPersist.Add(reactivated);
                return;
            }

            _recentlyCleared.Remove(key);
        }

        if (_options.MaxActiveAlarms > 0 && _active.Count >= _options.MaxActiveAlarms)
        {
            Logger.Warning(
                "活动报警已达上限 {MaxActiveAlarms}，丢弃新报警 {Code}（{SourceId}）。",
                _options.MaxActiveAlarms,
                signal.Code,
                signal.SourceId);
            return;
        }

        var record = new AlarmRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Key = key,
            Level = definition.Level,
            SourceKind = signal.SourceKind,
            Title = definition.Title,
            Message = FormatMessage(definition, signal),
            Remedy = definition.Remedy,
            ElementId = signal.ElementId,
            ElementName = signal.ElementName,
            RaisedAt = now,
            LastSeenAt = now,
            RaiseCount = 1,
            RequiresAcknowledge = definition.RequiresAcknowledge
        };

        _active[key] = record;
        events.Add((AlarmChangeKind.Raised, record));
        toPersist.Add(record);
    }

    private void ClearKey(
        AlarmKey key,
        DateTimeOffset now,
        List<(AlarmChangeKind Kind, AlarmRecord Record)> events,
        List<AlarmRecord> toPersist)
    {
        if (!_active.TryGetValue(key, out var record) || record.ClearedAt is not null)
        {
            return;
        }

        var cleared = record with { ClearedAt = now };

        if (_options.RetainUnacknowledgedOnRecovery && record.AcknowledgedAt is null)
        {
            // 恢复未确认：留在活动列表，等操作员确认后才归档。
            _active[key] = cleared;
        }
        else
        {
            _active.Remove(key);
            _recentlyCleared[key] = (cleared, now + ChatterWindow);
        }

        events.Add((AlarmChangeKind.Recovered, cleared));
        toPersist.Add(cleared);
    }

    private void CleanupExpired(
        DateTimeOffset now,
        List<(AlarmChangeKind Kind, AlarmRecord Record)> events,
        List<AlarmRecord> toPersist)
    {
        foreach (var key in _transientExpiry.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
        {
            _transientExpiry.Remove(key);
            ClearKey(key, now, events, toPersist);
        }

        foreach (var key in _recentlyCleared.Where(pair => pair.Value.ExpiresAt <= now).Select(pair => pair.Key).ToArray())
        {
            _recentlyCleared.Remove(key);
        }
    }

    private AlarmSnapshot BuildSnapshot(DateTimeOffset now)
    {
        var active = _active.Values
            .Where(record => record.State != AlarmState.Cleared)
            .OrderByDescending(record => record.Level)
            .ThenBy(record => record.RaisedAt)
            .ToArray();

        var unacknowledged = active.Count(record => record.AcknowledgedAt is null);
        var highest = active.Length == 0 ? AlarmLevel.Info : active.Max(record => record.Level);

        return new AlarmSnapshot(active, unacknowledged, highest, now);
    }

    private void Publish(
        List<(AlarmChangeKind Kind, AlarmRecord Record)> events,
        AlarmSnapshot snapshot)
    {
        foreach (var (kind, record) in events)
        {
            RaiseChanged(kind, record, snapshot);
        }
    }

    private void RaiseChanged(AlarmChangeKind kind, AlarmRecord record, AlarmSnapshot snapshot)
    {
        var handler = Changed;

        if (handler is null)
        {
            return;
        }

        var args = new AlarmChangedEventArgs
        {
            Kind = kind,
            Record = record,
            Snapshot = snapshot
        };

        foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler<AlarmChangedEventArgs>>())
        {
            try
            {
                subscriber(this, args);
            }
            catch (Exception ex)
            {
                // 订阅者异常不能影响报警服务和其他订阅者。
                Logger.Error(ex, "报警事件订阅者抛出异常：{AlarmId}", record.Id);
            }
        }
    }

    private void PersistAll(List<AlarmRecord> records)
    {
        foreach (var record in records)
        {
            _ = PersistAsync(record);
        }
    }

    private async Task PersistAsync(AlarmRecord record)
    {
        try
        {
            await _history.AppendAsync(record).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "写报警历史失败：{AlarmId}", record.Id);
        }
    }

    private void QueueAudit(string eventType, string detail)
    {
        _ = AuditAsync(eventType, detail);
    }

    private async Task AuditAsync(string eventType, string detail)
    {
        try
        {
            await _audit.RecordAsync(eventType, detail).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 审计失败不得影响确认结果。
            Logger.Warning(ex, "写报警审计失败：{AuditEventType}", eventType);
        }
    }

    private bool TryFindById(string alarmId, out AlarmKey key, out AlarmRecord record)
    {
        foreach (var pair in _active)
        {
            if (string.Equals(pair.Value.Id, alarmId, StringComparison.Ordinal))
            {
                key = pair.Key;
                record = pair.Value;
                return true;
            }
        }

        key = default;
        record = null!;
        return false;
    }

    private AlarmSignal Normalize(AlarmSignal signal, IAlarmSource source)
    {
        if (string.IsNullOrWhiteSpace(signal.SourceId))
        {
            return signal with { SourceId = source.SourceId };
        }

        if (signal.SourceId.Equals(source.SourceId, StringComparison.Ordinal) ||
            signal.SourceId.StartsWith(source.SourceId + ":", StringComparison.Ordinal))
        {
            return signal;
        }

        Logger.Warning(
            "报警源 {SourceGroup} 上报了不属于自己的 SourceId {SourceId}，已归入源组。",
            source.SourceId,
            signal.SourceId);

        return signal with { SourceId = source.SourceId };
    }

    private static bool BelongsTo(AlarmKey key, string sourceGroup)
        => key.SourceId.Equals(sourceGroup, StringComparison.Ordinal)
           || key.SourceId.StartsWith(sourceGroup + ":", StringComparison.Ordinal);

    private static bool MatchesSource(AlarmRecord record, string sourceId)
        => record.SourceId.Equals(sourceId, StringComparison.Ordinal)
           || record.SourceId.StartsWith(sourceId + ":", StringComparison.Ordinal);

    private string CurrentOperator() => _session?.Current?.UserName ?? "(未登录)";

    private TimeSpan ChatterWindow => TimeSpan.FromSeconds(Math.Max(0, _options.ChatterWindowSeconds));

    private static string FormatMessage(AlarmDefinition definition, AlarmSignal signal)
    {
        var template = string.IsNullOrWhiteSpace(definition.MessageTemplate)
            ? "{ElementName} 报警（{Code}，原始码 {RawCode}）"
            : definition.MessageTemplate;

        return template
            .Replace("{ElementName}", signal.ElementName ?? signal.ElementId ?? string.Empty, StringComparison.Ordinal)
            .Replace("{SourceId}", signal.SourceId, StringComparison.Ordinal)
            .Replace("{Code}", signal.Code, StringComparison.Ordinal)
            .Replace("{RawCode}", signal.RawCode?.ToString(CultureInfo.InvariantCulture) ?? "-", StringComparison.Ordinal)
            .Replace("{Detail}", signal.Detail ?? string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private SourceState GetOrCreateSourceState(string sourceId)
    {
        if (!_sourceStates.TryGetValue(sourceId, out var state))
        {
            state = new SourceState();
            _sourceStates[sourceId] = state;
        }

        return state;
    }

    private sealed class SourceState
    {
        public bool HasEverSucceeded { get; set; }

        public bool IsOffline { get; set; }
    }
}
