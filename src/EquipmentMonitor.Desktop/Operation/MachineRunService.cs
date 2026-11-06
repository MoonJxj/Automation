using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Desktop.Monitoring;
using Microsoft.Extensions.Logging;

namespace EquipmentMonitor.Desktop.Operation;

/// <summary>
/// 机台级启停控制：把“启动设备 / 停止设备 / 复位轴故障”翻译成元件命令，并维护一份可观察的运行状态。
/// </summary>
/// <remarks>
/// <para><b>当前实现只到元件命令层</b>：启动 = 对全部启用轴下发 <c>MC_Power(true)</c>，
/// 停止 = 对全部启用轴下发 <c>MC_Halt</c>，复位 = 对故障轴下发 <c>MC_Reset</c>。
/// 没有 PLC 自动循环点位，因此不冒充“自动流程已开始”；界面必须标注这一限制。</para>
/// <para><b>为什么状态放在这里而不是只改一个字段</b>：启动/停止是否成功必须由每根轴的实际命令结果决定。
/// 只有全部轴成功才进入 Running，否则回到 Stopped（回滚成功）或 Faulted（回滚也失败），
/// 不能为了界面好看伪造“已运行”。</para>
/// <para><b>线程</b>：命令在 <see cref="AxisElement"/> 内异步执行，本类不阻塞 UI；
/// 同一时刻只允许一个机台级序列，重复点击返回 <c>MachineRun.Busy</c>。</para>
/// <para><b>安全边界</b>：本类不替代急停、安全门与 PLC 互锁。停止命令不等同于切断动力，
/// 轴保持使能；需要断电/断使能时使用操作台的“软件急停”或现场安全回路。</para>
/// </remarks>
public sealed class MachineRunService
{
    private readonly AxisSnapshotStore _axes;
    private readonly IPermissionService _permissions;
    private readonly IAuditService _audit;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MachineRunService> _logger;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _snapshotGate = new();

    private MachineRunSnapshot _snapshot = MachineRunSnapshot.Empty;

    /// <summary>创建机台运行控制服务。</summary>
    /// <param name="axes">轴快照仓（启动时已挂载元件集合）。</param>
    /// <param name="permissions">权限服务；启停/复位入口统一校验。</param>
    /// <param name="audit">审计服务；记录机台级操作与结果。</param>
    /// <param name="logger">日志。</param>
    /// <param name="timeProvider">时间源；测试可注入假时钟。</param>
    public MachineRunService(
        AxisSnapshotStore axes,
        IPermissionService permissions,
        IAuditService audit,
        ILogger<MachineRunService> logger,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(axes);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(logger);

        _axes = axes;
        _permissions = permissions;
        _audit = audit;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>当前运行状态快照；任何线程可读。</summary>
    public MachineRunSnapshot Snapshot
    {
        get
        {
            lock (_snapshotGate)
            {
                return _snapshot;
            }
        }
    }

    /// <summary>
    /// 启动设备：校验轴可启动后逐根下发使能，全部成功才进入 <see cref="MachineRunState.Running"/>。
    /// </summary>
    /// <param name="reason">操作原因（写入审计）；为空时使用默认文案。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <exception cref="UnauthorizedAccessException">缺少 <see cref="Permission.MachineOperate"/>。</exception>
    public async Task<Result> StartAsync(string reason, CancellationToken cancellationToken = default)
    {
        _permissions.RequirePermission(Permission.MachineOperate);

        if (!await _operationGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return Result.Fail("设备操作正在进行，请稍候。", "MachineRun.Busy");
        }

        try
        {
            var current = Snapshot;

            if (current.State == MachineRunState.Running)
            {
                return Result.Fail("设备已在运行，无需重复启动。", "MachineRun.AlreadyRunning");
            }

            if (current.State == MachineRunState.Faulted)
            {
                return Result.Fail(
                    "上次启动/停止未完整成功，请先重新执行停止并确认设备实际状态。",
                    "MachineRun.Faulted");
            }

            var axes = EnabledAxes();

            if (axes.Length == 0)
            {
                return Result.Fail("没有已启用的伺服轴，无法启动设备。", "MachineRun.NoAxes");
            }

            var notInitialized = axes
                .Where(axis => !axis.IsInitialized)
                .Select(axis => axis.ElementMetadata.Name)
                .ToArray();

            if (notInitialized.Length > 0)
            {
                return Result.Fail(
                    $"以下轴尚未初始化：{string.Join("、", notInitialized)}。",
                    "MachineRun.NotInitialized");
            }

            var faulted = axes
                .Where(IsFaulted)
                .Select(axis => axis.ElementMetadata.Name)
                .ToArray();

            if (faulted.Length > 0)
            {
                return Result.Fail(
                    $"以下轴存在故障，请先复位：{string.Join("、", faulted)}。",
                    "MachineRun.AxisFault");
            }

            var moving = axes
                .Where(axis => axis.Status.IsMoving || axis.State == ElementState.Running)
                .Select(axis => axis.ElementMetadata.Name)
                .ToArray();

            if (moving.Length > 0)
            {
                return Result.Fail(
                    $"以下轴仍在运动，请先停止：{string.Join("、", moving)}。",
                    "MachineRun.AxisMoving");
            }

            SetActivity(MachineRunActivity.Starting);

            var enabledNow = new List<AxisElement>();
            var failures = new List<string>();

            try
            {
                foreach (var axis in axes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // 已经使能的轴不重复下发；但“已使能”本身不构成启动成功，
                    // 它只表示这一步不需要再动作。
                    if (axis.Status.IsEnabled)
                    {
                        continue;
                    }

                    var result = await axis.ServeOnAsync(cancellationToken).ConfigureAwait(false);

                    if (result.IsFailure)
                    {
                        // 失败即停：不再向后续轴下发使能，减少需要回滚的动作。
                        failures.Add($"{axis.ElementMetadata.Name}：{result.Error}");
                        break;
                    }

                    enabledNow.Add(axis);
                }
            }
            catch (OperationCanceledException)
            {
                var rollback = await RollbackAsync(enabledNow).ConfigureAwait(false);
                var error = "启动被取消" +
                            (rollback ? "，已回滚本次使能的轴。" : "，且回滚下使能失败，请人工确认设备状态。");

                SetSnapshot(
                    rollback ? MachineRunState.Stopped : MachineRunState.Faulted,
                    MachineRunActivity.None,
                    null,
                    error);
                await AuditAsync(AuditEventTypes.MachineRunFailed, reason, error).ConfigureAwait(false);

                return Result.Fail(error, "MachineRun.Cancelled");
            }
            catch (Exception ex)
            {
                var rollback = await RollbackAsync(enabledNow).ConfigureAwait(false);
                var error = $"启动过程中发生异常：{ex.Message}" +
                            (rollback ? "，已回滚本次使能的轴。" : "，且回滚下使能失败，请人工确认设备状态。");

                SetSnapshot(
                    rollback ? MachineRunState.Stopped : MachineRunState.Faulted,
                    MachineRunActivity.None,
                    null,
                    error);
                _logger.LogError(ex, "设备启动过程中发生异常。");
                await AuditAsync(AuditEventTypes.MachineRunFailed, reason, error).ConfigureAwait(false);

                return Result.Fail(error, "MachineRun.StartException", ex);
            }

            if (failures.Count > 0)
            {
                var rollback = await RollbackAsync(enabledNow).ConfigureAwait(false);
                var error = $"启动失败：{string.Join("；", failures)}";
                var state = rollback ? MachineRunState.Stopped : MachineRunState.Faulted;

                if (!rollback)
                {
                    error += "；回滚下使能也失败，请人工确认设备状态。";
                }

                SetSnapshot(state, MachineRunActivity.None, null, error);
                _logger.LogWarning("设备启动失败：{Error}", error);
                await AuditAsync(AuditEventTypes.MachineRunFailed, reason, error).ConfigureAwait(false);

                return Result.Fail(error, "MachineRun.StartFailed");
            }

            var message = $"已对 {axes.Length} 根轴确认伺服使能" +
                          (enabledNow.Count > 0 ? $"（本次新使能 {enabledNow.Count} 根）。" : "（原本均已使能）。");

            SetSnapshot(MachineRunState.Running, MachineRunActivity.None, message, null);
            _logger.LogInformation("设备启动完成：{Message} 原因={Reason}", message, NormalizeReason(reason));
            await AuditAsync(AuditEventTypes.MachineRunStarted, reason, message).ConfigureAwait(false);

            return Result.Ok();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>
    /// 停止设备：对全部启用轴下发停止命令（<c>MC_Halt</c>，保持使能）。
    /// </summary>
    /// <param name="reason">操作原因（写入审计）；为空时使用默认文案。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <exception cref="UnauthorizedAccessException">缺少 <see cref="Permission.MachineOperate"/>。</exception>
    public async Task<Result> StopAsync(string reason, CancellationToken cancellationToken = default)
    {
        _permissions.RequirePermission(Permission.MachineOperate);

        if (!await _operationGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return Result.Fail("设备操作正在进行，请稍候。", "MachineRun.Busy");
        }

        try
        {
            var axes = EnabledAxes();

            if (axes.Length == 0)
            {
                return Result.Fail("没有已启用的伺服轴，无法执行停止。", "MachineRun.NoAxes");
            }

            SetActivity(MachineRunActivity.Stopping);

            var failures = new List<string>();

            try
            {
                foreach (var axis in axes)
                {
                    // 停止类命令永远要尝试：即使轴未使能/已故障，“让它停下来”同样有意义。
                    var result = await axis.StopAsync(cancellationToken).ConfigureAwait(false);

                    if (result.IsFailure)
                    {
                        failures.Add($"{axis.ElementMetadata.Name}：{result.Error}");
                    }
                }
            }
            catch (Exception ex)
            {
                var error = $"停止过程中发生异常：{ex.Message}。设备可能仍在动作，请立即人工确认。";
                SetSnapshot(MachineRunState.Faulted, MachineRunActivity.None, null, error);
                _logger.LogError(ex, "设备停止过程中发生异常。");
                await AuditAsync(AuditEventTypes.MachineRunFailed, reason, error).ConfigureAwait(false);

                return Result.Fail(error, "MachineRun.StopException", ex);
            }

            if (failures.Count > 0)
            {
                var error = $"停止未完成：{string.Join("；", failures)}。设备可能仍在动作，请立即人工确认。";
                SetSnapshot(MachineRunState.Faulted, MachineRunActivity.None, null, error);
                _logger.LogError("设备停止未完成：{Error}", error);
                await AuditAsync(AuditEventTypes.MachineRunFailed, reason, error).ConfigureAwait(false);

                return Result.Fail(error, "MachineRun.StopFailed");
            }

            var message = $"已对 {axes.Length} 根轴下发 MC_Halt（轴保持使能）。";
            SetSnapshot(MachineRunState.Stopped, MachineRunActivity.None, message, null);
            _logger.LogInformation("设备停止完成：{Message} 原因={Reason}", message, NormalizeReason(reason));
            await AuditAsync(AuditEventTypes.MachineRunStopped, reason, message).ConfigureAwait(false);

            return Result.Ok();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>
    /// 复位全部故障轴（<c>MC_Reset</c>）。运行中拒绝执行，避免在运动过程中清故障。
    /// </summary>
    /// <param name="reason">操作原因（写入审计）；为空时使用默认文案。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <exception cref="UnauthorizedAccessException">缺少 <see cref="Permission.MachineOperate"/>。</exception>
    public async Task<Result> ResetAxisFaultsAsync(string reason, CancellationToken cancellationToken = default)
    {
        _permissions.RequirePermission(Permission.MachineOperate);

        if (!await _operationGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return Result.Fail("设备操作正在进行，请稍候。", "MachineRun.Busy");
        }

        try
        {
            if (Snapshot.State == MachineRunState.Running)
            {
                return Result.Fail("设备运行中不能复位轴故障，请先停止设备。", "MachineRun.Running");
            }

            var faulted = EnabledAxes().Where(IsFaulted).ToArray();

            if (faulted.Length == 0)
            {
                return Result.Ok();
            }

            SetActivity(MachineRunActivity.Resetting);

            var failures = new List<string>();

            try
            {
                foreach (var axis in faulted)
                {
                    var result = await axis.ResetAsync(cancellationToken).ConfigureAwait(false);

                    if (result.IsFailure)
                    {
                        failures.Add($"{axis.ElementMetadata.Name}：{result.Error}");
                    }
                }
            }
            catch (Exception ex)
            {
                var error = $"复位过程中发生异常：{ex.Message}";
                SetSnapshot(Snapshot.State, MachineRunActivity.None, null, error);
                _logger.LogError(ex, "轴故障复位过程中发生异常。");
                await AuditAsync(AuditEventTypes.MachineRunFailed, reason, error).ConfigureAwait(false);

                return Result.Fail(error, "MachineRun.ResetException", ex);
            }

            if (failures.Count > 0)
            {
                var error = $"复位未完成：{string.Join("；", failures)}";
                SetSnapshot(Snapshot.State, MachineRunActivity.None, null, error);
                await AuditAsync(AuditEventTypes.MachineRunFailed, reason, error).ConfigureAwait(false);

                return Result.Fail(error, "MachineRun.ResetFailed");
            }

            var message = $"已对 {faulted.Length} 根故障轴下发 MC_Reset。";
            SetSnapshot(Snapshot.State, MachineRunActivity.None, message, null);
            _logger.LogInformation("轴故障复位完成：{Message}", message);
            await AuditAsync(AuditEventTypes.MachineAxisReset, reason, message).ConfigureAwait(false);

            return Result.Ok();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private AxisElement[] EnabledAxes()
        => _axes.Elements.Where(axis => axis.ElementMetadata.IsEnabled).ToArray();

    private static bool IsFaulted(AxisElement axis)
        => axis.Status.IsFault || axis.State == ElementState.Error;

    /// <summary>
    /// 回滚本次新使能的轴。回滚用 <see cref="CancellationToken.None"/>：
    /// 启动被取消时也必须尝试让轴停下来。
    /// </summary>
    private static async Task<bool> RollbackAsync(IReadOnlyList<AxisElement> axes)
    {
        var ok = true;

        foreach (var axis in axes)
        {
            var result = await axis.ServeOffAsync(CancellationToken.None).ConfigureAwait(false);
            ok &= result.IsSuccess;
        }

        return ok;
    }

    private void SetActivity(MachineRunActivity activity)
    {
        var current = Snapshot;

        SetSnapshot(current.State, activity, current.Message, null);
    }

    private void SetSnapshot(
        MachineRunState state,
        MachineRunActivity activity,
        string? message,
        string? error)
    {
        var snapshot = new MachineRunSnapshot(
            state,
            activity,
            _timeProvider.GetLocalNow(),
            message,
            error);

        lock (_snapshotGate)
        {
            _snapshot = snapshot;
        }
    }

    private async Task AuditAsync(string eventType, string reason, string detail)
    {
        try
        {
            await _audit.RecordAsync(
                eventType,
                $"设备级操作（{NormalizeReason(reason)}）：{detail}").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 审计失败不得影响已经发生的设备动作。
            _logger.LogWarning(ex, "写设备级操作审计失败：{EventType}", eventType);
        }
    }

    private static string NormalizeReason(string reason)
        => string.IsNullOrWhiteSpace(reason) ? "未填写原因" : reason.Trim();
}
