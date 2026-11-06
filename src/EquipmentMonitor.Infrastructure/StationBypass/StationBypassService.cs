using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using Serilog;

namespace EquipmentMonitor.Infrastructure.StationBypass;

/// <summary>
/// 工位屏蔽的内存实现：进程内保存状态，重启后恢复为“全部正常”。
/// </summary>
/// <remarks>
/// <para><b>为什么先给内存实现</b>：自动循环由 PLC 控制，上位机写哪个寄存器、怎么回读尚未定
/// （见 <c>docs/module-design/16-function-selection.md</c>）。先把权限、校验、审计这条链路跑通；
/// 接设备时替换本类即可，页面与 <see cref="IStationBypassService"/> 契约不动。</para>
/// <para><b>线程</b>：<see cref="GetStates"/> 与 <see cref="SetAsync"/> 用同一把锁串行化；
/// 审计在锁外写，避免日志 IO 拖住状态读取。</para>
/// <para><b>操作者</b>：状态里记录会话显示名；审计的操作者由 <see cref="IAuditService"/> 实现补齐。</para>
/// </remarks>
public sealed class StationBypassService : IStationBypassService
{
    private const int MaxReasonLength = 200;

    private static readonly ILogger Logger = Log.ForContext<StationBypassService>();

    private readonly IPermissionService _permissions;
    private readonly IAuditService _audit;
    private readonly ISessionProvider? _session;
    private readonly object _gate = new();
    private readonly Dictionary<int, StationBypassState> _states;

    /// <summary>构造工位屏蔽服务。</summary>
    /// <param name="permissions">权限服务；写操作要求 <see cref="Permission.MachineConfig"/>。</param>
    /// <param name="audit">审计服务。</param>
    /// <param name="session">当前会话，用于记录操作者显示名；测试可不传。</param>
    public StationBypassService(
        IPermissionService permissions,
        IAuditService audit,
        ISessionProvider? session = null)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(audit);

        _permissions = permissions;
        _audit = audit;
        _session = session;

        _states = StationCatalog.All.ToDictionary(
            station => station.Id,
            station => new StationBypassState(
                station.Id,
                IsBypassed: false,
                Reason: string.Empty,
                OperatorName: string.Empty,
                ChangedAt: DateTimeOffset.MinValue));
    }

    /// <inheritdoc />
    public IReadOnlyList<StationBypassState> GetStates()
    {
        lock (_gate)
        {
            return _states.Values
                .OrderBy(state => state.StationId)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public async Task<Result> SetAsync(
        int stationId,
        bool bypassed,
        string reason,
        CancellationToken cancellationToken = default)
    {
        // 越权是编程错误（页面本不该给出入口），按仓库约定抛异常，不转成 Result。
        _permissions.RequirePermission(Permission.MachineConfig);
        cancellationToken.ThrowIfCancellationRequested();

        if (!StationCatalog.Contains(stationId))
        {
            return Result.Fail($"未知工位：{stationId}。", "STATION_UNKNOWN");
        }

        var reasonText = (reason ?? string.Empty).Trim();

        if (bypassed && reasonText.Length == 0)
        {
            return Result.Fail("屏蔽工位必须填写原因。", "STATION_BYPASS_REASON_REQUIRED");
        }

        if (reasonText.Length > MaxReasonLength)
        {
            return Result.Fail($"原因不能超过 {MaxReasonLength} 个字符。", "STATION_BYPASS_REASON_TOO_LONG");
        }

        var operatorName = _session?.Current?.DisplayName ?? "(未登录)";

        lock (_gate)
        {
            var current = _states[stationId];

            if (current.IsBypassed == bypassed)
            {
                // 幂等：重复点“保存”不重复写审计。
                return Result.Ok();
            }

            _states[stationId] = new StationBypassState(
                stationId,
                bypassed,
                reasonText,
                operatorName,
                DateTimeOffset.Now);
        }

        var detail = bypassed
            ? $"{StationCatalog.Name(stationId)} 屏蔽，原因：{reasonText}"
            : $"{StationCatalog.Name(stationId)} 恢复（原因：{(reasonText.Length == 0 ? "未填写" : reasonText)}）";

        try
        {
            await _audit
                .RecordAsync(AuditEventTypes.StationBypassChanged, detail, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 审计失败不得影响业务结果（IAuditService 的约定）；状态已经生效，不能回滚成“没改”。
            Logger.Warning(ex, "写工位屏蔽审计失败：工位 {StationId}", stationId);
        }

        Logger.Information(
            "工位屏蔽变更：工位 {StationId} -> {Bypassed}，操作者 {OperatorName}",
            stationId,
            bypassed,
            operatorName);

        return Result.Ok();
    }
}
