using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Cylinder;

/// <summary>
/// 气缸元件：把 <see cref="IIoController"/> 的两个电磁阀输出、两个限位输入包装成
/// 读契约（<see cref="IElementCylinder"/>）与命令契约（<see cref="IElementCommandTarget"/>）。
/// </summary>
/// <remarks>
/// <para><b>命令语义是“已下发”，不是“已到位”</b>：<c>Extend</c>/<c>Retract</c> 只把电磁阀置位，
/// 是否到位由 <see cref="RefreshAsync"/> 读取限位判断；超过 <see cref="CylinderConfig.ActionTimeoutSeconds"/>
/// 仍未到位则置故障，必须显式复位。</para>
/// <para><b>停止的真实含义</b>：单控气缸断阀后弹簧复位（会缩回）；双控气缸断阀后停在原地，
/// 但中位机能由阀决定，不能承诺“停在中间”。界面与消息必须如实描述。</para>
/// <para><b>安全边界</b>：本类不替代安全回路。急停、安全门、机械硬限位由 PLC/电气兜底。</para>
/// </remarks>
public sealed class CylinderElement : IElementCylinder, IDisposable
{
    /// <summary>伸出。</summary>
    public const string CommandExtend = "Cylinder_Extend";

    /// <summary>缩回。</summary>
    public const string CommandRetract = "Cylinder_Retract";

    /// <summary>断阀/取消当前动作。</summary>
    public const string CommandHalt = "Cylinder_Halt";

    /// <summary>清除故障。</summary>
    public const string CommandReset = "Cylinder_Reset";

    private const int TimeoutFaultCode = 1001;
    private const int SensorConflictFaultCode = 1002;

    private readonly IIoController _controller;
    private readonly CylinderConfig _config;
    private readonly IPermissionService _permissions;
    private readonly IAuditService _audit;
    private readonly global::Common.ILoggerManager? _logger;
    private readonly IReadOnlyList<ElementCommandDescriptor> _commands;
    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private readonly object _stateGate = new();

    private CylinderStatus _status = CylinderStatus.Unknown;
    private ElementState _state = ElementState.Unknown;
    private string? _lastError;
    private bool _initialized;
    private bool _disposed;
    private CylinderAction _action;
    private DateTimeOffset _actionDeadline;

    /// <summary>创建气缸元件。</summary>
    /// <param name="config">气缸配置。</param>
    /// <param name="controller">底层 IO 控制器；生命周期由调用方/DI 管理。</param>
    /// <param name="permissions">权限服务。</param>
    /// <param name="audit">审计服务。</param>
    /// <param name="logger">可选日志；为 <c>null</c> 时不写日志。</param>
    public CylinderElement(
        CylinderConfig config,
        IIoController controller,
        IPermissionService permissions,
        IAuditService audit,
        global::Common.ILoggerManager? logger = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(audit);

        config.Validate();

        _config = config;
        _controller = controller;
        _permissions = permissions;
        _audit = audit;
        _logger = logger;
        _commands = BuildCommandTable();

        ElementMetadata = new ElementMetadata
        {
            ElementId = config.ElementId,
            ElementType = "Cylinder",
            Name = config.Name,
            Description = config.Description,
            IsEnabled = config.IsEnabled
        };
    }

    /// <inheritdoc />
    public ElementMetadata ElementMetadata { get; }

    /// <inheritdoc />
    public IReadOnlyList<ElementCommandDescriptor> Commands => _commands;

    /// <inheritdoc />
    public bool IsDoubleActing => _config.IsDoubleActing;

    /// <summary>控制器编号。</summary>
    public int ControllerNo => _config.ControllerNo;

    /// <summary>所属工位编号。</summary>
    public int StationId => _config.StationId;

    /// <inheritdoc />
    public double ActionTimeoutSeconds => _config.ActionTimeoutSeconds;

    /// <inheritdoc />
    public CylinderStatus Status
    {
        get
        {
            lock (_stateGate)
            {
                return _status;
            }
        }
    }

    /// <inheritdoc />
    public string? LastError
    {
        get
        {
            lock (_stateGate)
            {
                return _lastError;
            }
        }
    }

    /// <inheritdoc />
    public ElementState State
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    /// <inheritdoc />
    public bool IsInitialized
    {
        get
        {
            lock (_stateGate)
            {
                return _initialized;
            }
        }
    }

    /// <inheritdoc />
    public Task<Result> InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsInitialized)
        {
            return Task.FromResult(Result.Ok());
        }

        if (!_config.IsEnabled)
        {
            SetState(ElementState.Disabled);
            SetInitialized();
            return Task.FromResult(Result.Ok());
        }

        if (!_controller.IsInitialize && !_controller.Initialize())
        {
            SetState(ElementState.Error);
            SetLastError($"气缸 {_config.ElementId} 的 IO 控制器初始化失败。");
            return Task.FromResult(Result.Fail(
                $"气缸 {_config.ElementId} 的 IO 控制器初始化失败。",
                "Cylinder.ControllerInitializeFailed"));
        }

        SetInitialized();
        return RefreshAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.IsEnabled)
        {
            SetState(ElementState.Disabled);
            return Task.FromResult(Result.Ok());
        }

        if (!IsInitialized || !_controller.IsInitialize)
        {
            return Task.FromResult(Result.Fail(
                $"气缸 {_config.ElementId} 尚未初始化，无法刷新状态。",
                "Cylinder.NotInitialized"));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var now = DateTimeOffset.Now;
            var extended = _controller.ReadIn(_config.ControllerNo, _config.ExtendedInput);
            var retracted = _controller.ReadIn(_config.ControllerNo, _config.RetractedInput);

            var fault = false;
            var faultCode = 0;
            string? error = null;
            CylinderPosition position;

            if (extended && retracted)
            {
                // 两个限位同时亮：可能短路、机械卡死或传感器故障，位置不可信。
                position = CylinderPosition.Intermediate;
                fault = true;
                faultCode = SensorConflictFaultCode;
                error = "伸出/缩回限位同时为 ON，位置不可信。";
            }
            else if (extended)
            {
                position = CylinderPosition.Extended;
            }
            else if (retracted)
            {
                position = CylinderPosition.Retracted;
            }
            else
            {
                position = _action == CylinderAction.None
                    ? CylinderPosition.Intermediate
                    : CylinderPosition.Moving;
            }

            if (_action != CylinderAction.None && now > _actionDeadline && !fault)
            {
                position = CylinderPosition.Fault;
                fault = true;
                faultCode = TimeoutFaultCode;
                error = $"气缸 {_config.ElementId} 动作超时（{_config.ActionTimeoutSeconds:F1}s）仍未到位。";
            }

            // 目标位已到：清掉进行中的动作。
            if (_action == CylinderAction.Extending && extended && !retracted)
            {
                _action = CylinderAction.None;
            }
            else if (_action == CylinderAction.Retracting && retracted && !extended)
            {
                _action = CylinderAction.None;
            }

            var status = new CylinderStatus(
                _config.IsEnabled,
                position,
                extended,
                retracted,
                _action != CylinderAction.None,
                fault,
                faultCode,
                error,
                now);

            SetStatus(status);

            if (fault)
            {
                SetLastError(error);
                SetState(ElementState.Error);
            }
            else
            {
                SetState(_action != CylinderAction.None
                    ? ElementState.Running
                    : position == CylinderPosition.Intermediate
                        ? ElementState.Warning
                        : ElementState.Ready);
            }

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            SetState(ElementState.Error);
            SetLastError($"气缸 {_config.ElementId} 刷新状态异常：{ex.Message}");

            return Task.FromResult(Result.Fail(
                $"气缸 {_config.ElementId} 刷新状态异常：{ex.Message}",
                "Cylinder.RefreshException",
                ex));
        }
    }

    /// <inheritdoc />
    public async Task<Result> ResetAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteCommandAsync(
            new ElementCommandRequest { CommandKey = CommandReset },
            cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result.Fail(result.Error, result.ErrorCode, result.Exception);
        }

        return result.Value.Outcome == CommandOutcome.Accepted
            ? Result.Ok()
            : Result.Fail(
                result.Value.Message ?? $"气缸复位未完成（{result.Value.Outcome}）。",
                result.Value.ErrorCode ?? "Cylinder.ResetFailed");
    }

    /// <inheritdoc />
    public CommandAvailability EvaluateCommand(string commandKey)
    {
        if (_commands.All(command => !string.Equals(command.Key, commandKey, StringComparison.Ordinal)))
        {
            return CommandAvailability.Blocked($"未知命令 {commandKey}。");
        }

        return EvaluateCore(commandKey, Status);
    }

    /// <inheritdoc />
    public Task<Result<ElementCommandResult>> ExecuteCommandAsync(
        ElementCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _permissions.RequirePermission(Permission.MachineOperate);

        return Task.Run(() => ExecuteCoreAsync(request, cancellationToken), CancellationToken.None);
    }

    private async Task<Result<ElementCommandResult>> ExecuteCoreAsync(
        ElementCommandRequest request,
        CancellationToken cancellationToken)
    {
        var descriptor = _commands.FirstOrDefault(
            command => string.Equals(command.Key, request.CommandKey, StringComparison.Ordinal));

        if (descriptor is null)
        {
            return await RejectAsync(request, CommandRejectionCodes.UnknownCommand, $"未知命令 {request.CommandKey}。")
                .ConfigureAwait(false);
        }

        if (request.Arguments.Count > 0)
        {
            return await RejectAsync(
                request,
                CommandRejectionCodes.InvalidArgument,
                $"命令 {request.CommandKey} 不支持参数。").ConfigureAwait(false);
        }

        if (_disposed)
        {
            return await RejectAsync(request, CommandRejectionCodes.NotReady, "气缸已释放。").ConfigureAwait(false);
        }

        if (!IsInitialized)
        {
            return await RejectAsync(request, CommandRejectionCodes.NotReady, "气缸尚未初始化。").ConfigureAwait(false);
        }

        await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var availability = EvaluateCore(request.CommandKey, Status);
            if (!availability.IsAvailable)
            {
                return await RejectAsync(
                    request,
                    CommandRejectionCodes.Blocked,
                    availability.Reason ?? "互锁拒绝。").ConfigureAwait(false);
            }

            var result = Dispatch(descriptor);

            await AuditAsync(
                result.Outcome == CommandOutcome.Accepted
                    ? AuditEventTypes.CommandExecuted
                    : AuditEventTypes.CommandFailed,
                $"{descriptor.Key} → {result.Outcome}" +
                (string.IsNullOrWhiteSpace(result.Message) ? string.Empty : $"：{result.Message}"))
                .ConfigureAwait(false);

            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail<ElementCommandResult>("命令等待已取消。", CommandRejectionCodes.Cancelled);
        }
        finally
        {
            _commandGate.Release();
        }
    }

    private ElementCommandResult Dispatch(ElementCommandDescriptor descriptor)
    {
        var now = DateTimeOffset.Now;

        try
        {
            switch (descriptor.Key)
            {
                case CommandExtend:
                {
                    var accepted = WriteValves(extend: true);
                    if (!accepted)
                    {
                        return Fail(descriptor.Key, now, "Cylinder.ExtendRejected", "伸出");
                    }

                    StartAction(CylinderAction.Extending, now);
                    return ElementCommandResult.Accepted(descriptor.Key, now, "伸出命令已下发，等待伸出到位。");
                }

                case CommandRetract:
                {
                    var accepted = WriteValves(extend: false);
                    if (!accepted)
                    {
                        return Fail(descriptor.Key, now, "Cylinder.RetractRejected", "缩回");
                    }

                    StartAction(CylinderAction.Retracting, now);
                    return ElementCommandResult.Accepted(descriptor.Key, now, "缩回命令已下发，等待缩回到位。");
                }

                case CommandHalt:
                {
                    var accepted = _config.IsDoubleActing
                        ? _controller.SetOut(_config.ControllerNo, _config.ExtendOutput, false)
                          & _controller.SetOut(_config.ControllerNo, _config.RetractOutput!.Value, false)
                        : _controller.SetOut(_config.ControllerNo, _config.ExtendOutput, false);

                    if (!accepted)
                    {
                        return Fail(descriptor.Key, now, "Cylinder.HaltRejected", "断阀");
                    }

                    _action = CylinderAction.None;
                    return ElementCommandResult.Accepted(
                        descriptor.Key,
                        now,
                        _config.IsDoubleActing
                            ? "已断阀；双控气缸停在原地（由阀的中位机能决定）。"
                            : "已断阀；单控气缸将弹簧复位。");
                }

                case CommandReset:
                    _action = CylinderAction.None;
                    SetLastError(null);

                    // 立即清掉状态里的故障位，否则复位后按钮仍按旧快照置灰，直到下一轮采集。
                    var current = Status;
                    var position = current.ExtendedSensor && !current.RetractedSensor
                        ? CylinderPosition.Extended
                        : current.RetractedSensor && !current.ExtendedSensor
                            ? CylinderPosition.Retracted
                            : CylinderPosition.Intermediate;

                    SetStatus(current with
                    {
                        Position = position,
                        IsFault = false,
                        FaultCode = 0,
                        LastError = null,
                        IsMoving = false,
                        UpdatedAt = now
                    });

                    SetState(ElementState.Ready);
                    return ElementCommandResult.Accepted(descriptor.Key, now, "故障已复位。");

                default:
                    return ElementCommandResult.Error(
                        descriptor.Key, now, "COMMAND_UNSUPPORTED", $"命令 {descriptor.Key} 未实现。");
            }
        }
        catch (Exception ex)
        {
            SetState(ElementState.Error);
            SetLastError($"执行 {descriptor.Key} 异常：{ex.Message}");

            return ElementCommandResult.Error(
                descriptor.Key, now, "Cylinder.CommandException", $"执行 {descriptor.Key} 异常：{ex.Message}");
        }
    }

    private bool WriteValves(bool extend)
    {
        if (!_config.IsDoubleActing)
        {
            // 单控：置位伸出，复位缩回（弹簧）。
            return _controller.SetOut(_config.ControllerNo, _config.ExtendOutput, extend);
        }

        var extendOutput = _config.ExtendOutput;
        var retractOutput = _config.RetractOutput!.Value;

        // 双控：先断开另一侧，再置位目标侧，避免两个线圈同时得电。
        var released = _controller.SetOut(_config.ControllerNo, extend ? retractOutput : extendOutput, false);

        return released & _controller.SetOut(_config.ControllerNo, extend ? extendOutput : retractOutput, true);
    }

    private void StartAction(CylinderAction action, DateTimeOffset now)
    {
        _action = action;
        _actionDeadline = now + TimeSpan.FromSeconds(_config.ActionTimeoutSeconds);
    }

    private CommandAvailability EvaluateCore(string commandKey, CylinderStatus status)
    {
        if (_disposed)
        {
            return CommandAvailability.Blocked("气缸已释放。");
        }

        if (!IsInitialized)
        {
            return CommandAvailability.Blocked("气缸尚未初始化。");
        }

        if (!status.IsEnabled)
        {
            return CommandAvailability.Blocked("气缸已在配置中禁用。");
        }

        if (commandKey == CommandReset)
        {
            return CommandAvailability.Available;
        }

        if (commandKey == CommandHalt)
        {
            return CommandAvailability.Available;
        }

        if (status.IsFault)
        {
            return CommandAvailability.Blocked(
                string.IsNullOrWhiteSpace(status.LastError)
                    ? "气缸故障：先执行复位。"
                    : $"气缸故障：{status.LastError} 先执行复位。");
        }

        if (status.ExtendedSensor && status.RetractedSensor)
        {
            return CommandAvailability.Blocked("伸出/缩回限位同时为 ON，位置不可信：先检查传感器。");
        }

        if (status.IsMoving)
        {
            return CommandAvailability.Blocked("气缸正在动作：先等待到位或断阀。");
        }

        if (commandKey == CommandExtend && status.IsExtended)
        {
            return CommandAvailability.Blocked("气缸已伸出到位。");
        }

        if (commandKey == CommandRetract && status.IsRetracted)
        {
            return CommandAvailability.Blocked("气缸已缩回到位。");
        }

        return CommandAvailability.Available;
    }

    private IReadOnlyList<ElementCommandDescriptor> BuildCommandTable()
    {
        var commands = new List<ElementCommandDescriptor>
        {
            new()
            {
                Key = CommandExtend,
                Name = "伸出",
                Description = "伸出电磁阀置位，等待伸出到位。",
                Activation = ElementCommandActivation.Execute,
                Risk = ElementCommandRisk.Confirm
            },
            new()
            {
                Key = CommandRetract,
                Name = "缩回",
                Description = "缩回电磁阀置位，等待缩回到位。",
                Activation = ElementCommandActivation.Execute,
                Risk = ElementCommandRisk.Confirm
            },
            new()
            {
                Key = CommandHalt,
                Name = "断阀",
                Description = "两个电磁阀都断开；单控弹簧复位，双控停在原地。",
                Activation = ElementCommandActivation.Execute,
                Risk = ElementCommandRisk.Normal
            },
            new()
            {
                Key = CommandReset,
                Name = "复位",
                Description = "清除超时/限位矛盾故障。",
                Activation = ElementCommandActivation.Execute,
                Risk = ElementCommandRisk.Normal
            }
        };

        foreach (var command in commands)
        {
            command.Validate();
        }

        return commands;
    }

    private ElementCommandResult Fail(string commandKey, DateTimeOffset now, string code, string action)
    {
        SetLastError($"气缸 {_config.ElementId} {action}命令被 IO 控制器拒绝。");
        return ElementCommandResult.Error(
            commandKey, now, code, $"气缸 {_config.ElementId} {action}命令被 IO 控制器拒绝。");
    }

    private async Task<Result<ElementCommandResult>> RejectAsync(
        ElementCommandRequest request,
        string errorCode,
        string message)
    {
        await AuditAsync(AuditEventTypes.CommandRejected, $"{request.CommandKey}：{message}（{errorCode}）")
            .ConfigureAwait(false);

        return Result.Fail<ElementCommandResult>(message, errorCode);
    }

    private async Task AuditAsync(string eventType, string detail)
    {
        try
        {
            await _audit.RecordAsync(eventType, $"[{_config.ElementId}] {detail}").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 审计失败不得影响命令结果。
            _logger?.Warn($"写气缸审计失败：{eventType} {detail}{Environment.NewLine}{ex}");
        }
    }

    private void SetStatus(CylinderStatus status)
    {
        lock (_stateGate)
        {
            _status = status;
        }
    }

    private void SetState(ElementState state)
    {
        lock (_stateGate)
        {
            _state = state;
        }
    }

    private void SetLastError(string? error)
    {
        lock (_stateGate)
        {
            _lastError = error;
        }
    }

    private void SetInitialized()
    {
        lock (_stateGate)
        {
            _initialized = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _commandGate.Dispose();
    }

    private enum CylinderAction
    {
        None = 0,
        Extending = 1,
        Retracting = 2
    }
}
