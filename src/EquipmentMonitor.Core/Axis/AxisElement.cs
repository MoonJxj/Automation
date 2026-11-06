using EquipmentMonitor.Common;
using EquipmentMonitor.Core;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Axis;

/// <summary>
/// 通用轴元件：把 <see cref="IAxisController"/> 同时包装成读契约（<see cref="IElementAxis"/>）
/// 与命令契约（<see cref="IElementCommandTarget"/>）。
/// </summary>
/// <remarks>
/// <para><b>命令语义是“已下发”，不是“已到位”</b>：厂商控制卡与 PLC 都是“写目标/触发，设备自己走”的模型；
/// 到位由业务通过 <see cref="RefreshAsync"/> 轮询 <see cref="Status"/> 判断。
/// 因此成功下发返回 <see cref="CommandOutcome.Accepted"/>，而不是假称 <see cref="CommandOutcome.Done"/>。</para>
/// <para><b>单一入口</b>：<see cref="ServeOnAsync"/> 等所有命令方法都走
/// <see cref="ExecuteCommandAsync"/> —— 权限、审计、互锁、参数校验只实现一次。</para>
/// <para><b>线程边界</b>：命令在 <see cref="Task.Run(Func{Task})"/> 中执行，UI 线程只 await；
/// 同一元件上的命令由 <see cref="SemaphoreSlim"/> 串行化。</para>
/// <para><b>软限位</b>：上位机只做下发前的软件保护；急停、安全门、硬限位由 PLC/轴卡兜底，本类不承担安全功能。</para>
/// </remarks>
public sealed class AxisElement : IElementAxis, IElementCommandTarget, IDisposable
{
    /// <summary>使能 / 下使能（PLCopen MC_Power）。</summary>
    public const string CommandPower = "MC_Power";

    /// <summary>故障复位（MC_Reset）。</summary>
    public const string CommandReset = "MC_Reset";

    /// <summary>回零（MC_Home）。</summary>
    public const string CommandHome = "MC_Home";

    /// <summary>绝对定位（MC_MoveAbsolute）。</summary>
    public const string CommandMoveAbsolute = "MC_MoveAbsolute";

    /// <summary>相对定位（MC_MoveRelative）。</summary>
    public const string CommandMoveRelative = "MC_MoveRelative";

    /// <summary>减速停止，保持使能（MC_Halt）。</summary>
    public const string CommandHalt = "MC_Halt";

    /// <summary>软件急停：停止 + 下使能（本项目补充，不是安全功能）。</summary>
    public const string CommandEStop = "EStop";

    /// <summary>正向点动（按住走、松开停）。</summary>
    public const string CommandJogForward = "JogForward";

    /// <summary>反向点动（按住走、松开停）。</summary>
    public const string CommandJogBackward = "JogBackward";

    private readonly IAxisController _controller;
    private readonly AxisElementConfig _config;
    private readonly IPermissionService _permissions;
    private readonly IAuditService _audit;
    private readonly global::Common.ILoggerManager? _logger;
    private readonly IReadOnlyList<ElementCommandDescriptor> _commands;
    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private readonly object _stateGate = new();

    private AxisStatus _status = new();
    private ElementState _state = ElementState.Unknown;
    private AxisOperationError? _lastError;
    private bool _initialized;
    private bool _disposed;
    private double _lastTarget;

    /// <summary>创建轴元件。</summary>
    /// <param name="config">轴配置快照。</param>
    /// <param name="controller">底层控制器；生命周期由调用方/DI 管理，本类不负责释放。</param>
    /// <param name="permissions">权限服务：命令入口统一校验。</param>
    /// <param name="audit">审计服务：每条命令成败都记录。</param>
    /// <param name="logger">可选日志；为 <c>null</c> 时只保存审计失败，不写日志。</param>
    /// <exception cref="InvalidOperationException">配置或命令表非法。</exception>
    public AxisElement(
        AxisElementConfig config,
        IAxisController controller,
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
        _commands = BuildCommandTable(config);

        ControllerNo = config.ControllerNo;
        AxisNo = config.AxisNo;

        ElementMetadata = new ElementMetadata
        {
            ElementId = config.ElementId,
            ElementType = "Axis",
            Name = config.Name,
            IsEnabled = config.IsEnabled
        };
    }

    /// <inheritdoc />
    public ElementMetadata ElementMetadata { get; }

    /// <inheritdoc />
    public int ControllerNo { get; init; }

    /// <inheritdoc />
    public int AxisNo { get; init; }

    /// <inheritdoc />
    public IReadOnlyList<ElementCommandDescriptor> Commands => _commands;

    /// <summary>工程单位（来自轴配置，供界面显示）。</summary>
    public string Unit => _config.Unit;

    /// <summary>所属工位编号。</summary>
    public int StationId => _config.StationId;

    /// <summary>默认定位速度（来自轴配置；单次命令暂不支持覆盖）。</summary>
    public double DefaultVelocity => _config.DefaultVelocity;

    /// <summary>默认加速度（来自轴配置）。</summary>
    public double Acceleration => _config.Acceleration;

    /// <summary>默认减速度（来自轴配置）。</summary>
    public double Deceleration => _config.Deceleration;

    /// <summary>默认加加速度（来自轴配置；0 表示不使用 S 曲线）。</summary>
    public double Jerk => _config.Jerk;

    /// <inheritdoc />
    public AxisStatus Status
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
    public AxisOperationError? LastError
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
            var error = new AxisOperationError(
                "Axis.ControllerInitializeFailed",
                $"轴 {_config.ElementId} 的控制器初始化失败。");
            SetLastError(error);
            return Task.FromResult(Result.Fail(error.Message, error.Code));
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
                $"轴 {_config.ElementId} 尚未初始化，无法刷新状态。",
                "Axis.NotInitialized"));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var snapshot = ReadStatus();
            SetStatus(snapshot);
            SetState(ComputeElementState(snapshot));
            return Task.FromResult(Result.Ok());
        }
        catch (AxisControllerException ex)
        {
            SetState(ElementState.Error);
            SetLastError(new AxisOperationError(ex.ErrorCode ?? "Axis.CommandException", ex.Message, Exception: ex));
            return Task.FromResult(Result.Fail(ex.Message, ex.ErrorCode, ex));
        }
        catch (Exception ex)
        {
            SetState(ElementState.Error);
            SetLastError(new AxisOperationError("Axis.RefreshException", ex.Message, Exception: ex));
            return Task.FromResult(Result.Fail(
                $"轴 {_config.ElementId} 刷新状态时发生异常：{ex.Message}",
                "Axis.RefreshException",
                ex));
        }
    }

    // ───────────────────────── 命令契约 ─────────────────────────

    /// <inheritdoc />
    public CommandAvailability EvaluateCommand(string commandKey)
    {
        if (!_commands.Any(c => string.Equals(c.Key, commandKey, StringComparison.Ordinal)))
        {
            return CommandAvailability.Blocked($"未知命令 {commandKey}。");
        }

        return EvaluateCore(commandKey, Status, powerEnable: true);
    }

    /// <inheritdoc />
    /// <exception cref="UnauthorizedAccessException">当前会话无 <see cref="Permission.MachineOperate"/>。</exception>
    public Task<Result<ElementCommandResult>> ExecuteCommandAsync(
        ElementCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 权限在调用线程上同步校验：越权是调用方的编程错误，必须立刻可见，
        // 不能被 Task.Run 的异常包装拖到 await 之后才暴露。
        _permissions.RequirePermission(Permission.MachineOperate);

        return Task.Run(() => ExecuteCoreAsync(request, cancellationToken), CancellationToken.None);
    }

    private async Task<Result<ElementCommandResult>> ExecuteCoreAsync(
        ElementCommandRequest request,
        CancellationToken cancellationToken)
    {
        var descriptor = _commands.FirstOrDefault(
            c => string.Equals(c.Key, request.CommandKey, StringComparison.Ordinal));

        if (descriptor is null)
        {
            return await RejectAsync(
                request,
                CommandRejectionCodes.UnknownCommand,
                $"未知命令 {request.CommandKey}。").ConfigureAwait(false);
        }

        var argumentError = ValidateArguments(descriptor, request);
        if (argumentError is not null)
        {
            return await RejectAsync(
                request,
                CommandRejectionCodes.InvalidArgument,
                argumentError).ConfigureAwait(false);
        }

        if (_disposed)
        {
            return await RejectAsync(request, CommandRejectionCodes.NotReady, "轴已释放。").ConfigureAwait(false);
        }

        if (!IsInitialized)
        {
            return await RejectAsync(request, CommandRejectionCodes.NotReady, "轴尚未初始化。").ConfigureAwait(false);
        }

        await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            AxisStatus status;
            try
            {
                status = ReadStatus();
            }
            catch (Exception ex)
            {
                SetState(ElementState.Error);
                SetLastError(new AxisOperationError("Axis.CommandReadStatusFailed", ex.Message, Exception: ex));
                return Result.Fail<ElementCommandResult>(
                    $"执行 {request.CommandKey} 前读取轴状态失败：{ex.Message}",
                    "Axis.CommandReadStatusFailed",
                    ex);
            }

            var availability = EvaluateCore(request.CommandKey, status, ResolvePowerEnable(request));
            if (!availability.IsAvailable)
            {
                return await RejectAsync(
                    request,
                    CommandRejectionCodes.Blocked,
                    availability.Reason ?? "互锁拒绝。").ConfigureAwait(false);
            }

            var result = DispatchAsync(descriptor, request, status, cancellationToken);

            await AuditAsync(
                result.Outcome == CommandOutcome.Accepted
                    ? AuditEventTypes.CommandExecuted
                    : AuditEventTypes.CommandFailed,
                Describe(request, result)).ConfigureAwait(false);

            return Result.Ok(result);
        }
        catch (OperationCanceledException)
        {
            await AuditAsync(
                AuditEventTypes.CommandFailed,
                $"{request.CommandKey} 等待被取消。").ConfigureAwait(false);

            return Result.Fail<ElementCommandResult>("命令等待已取消。", CommandRejectionCodes.Cancelled);
        }
        finally
        {
            _commandGate.Release();
        }
    }

    private ElementCommandResult DispatchAsync(
        ElementCommandDescriptor descriptor,
        ElementCommandRequest request,
        AxisStatus status,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.Now;

        try
        {
            switch (descriptor.Key)
            {
                case CommandPower:
                {
                    var enable = request.TryGetBool(CommandParameterKeys.Enable, out var value) && value;
                    var accepted = enable
                        ? _controller.ServeOn(ControllerNo, AxisNo)
                        : _controller.ServeOff(ControllerNo, AxisNo);

                    return accepted
                        ? ElementCommandResult.Accepted(
                            descriptor.Key, now, enable ? "使能命令已下发。" : "下使能命令已下发。")
                        : Failed(descriptor.Key, now, "Axis.ServeRejected", enable ? "使能" : "下使能");
                }

                case CommandReset:
                    return _controller.ResetAxis(ControllerNo, AxisNo)
                        ? ElementCommandResult.Accepted(descriptor.Key, now, "复位命令已下发。")
                        : Failed(descriptor.Key, now, "Axis.ResetRejected", "复位");

                case CommandHome:
                    return _controller.Home(ControllerNo, AxisNo)
                        ? ElementCommandResult.Accepted(descriptor.Key, now, "回零命令已下发，到位由状态观察。")
                        : Failed(descriptor.Key, now, "Axis.HomeRejected", "回零");

                case CommandMoveAbsolute:
                {
                    request.TryGetNumber(CommandParameterKeys.Position, out var position);
                    _lastTarget = position;

                    return _controller.MoveAbs(ControllerNo, AxisNo, position)
                        ? ElementCommandResult.Accepted(
                            descriptor.Key, now, $"绝对定位已下发：目标 {position:F3}。")
                        : Failed(descriptor.Key, now, "Axis.MoveAbsRejected", "绝对定位");
                }

                case CommandMoveRelative:
                {
                    request.TryGetNumber(CommandParameterKeys.Distance, out var distance);
                    _lastTarget = status.ActualPosition + distance;

                    return _controller.MoveRel(ControllerNo, AxisNo, distance)
                        ? ElementCommandResult.Accepted(
                            descriptor.Key, now,
                            $"相对定位已下发：位移 {distance:F3}，目标 {_lastTarget:F3}。")
                        : Failed(descriptor.Key, now, "Axis.MoveRelRejected", "相对定位");
                }

                case CommandHalt:
                    return _controller.Stop(ControllerNo, AxisNo)
                        ? ElementCommandResult.Accepted(descriptor.Key, now, "停止命令已下发。")
                        : Failed(descriptor.Key, now, "Axis.StopRejected", "停止");

                case CommandEStop:
                {
                    // 两个动作都要尝试，不能用短路 && 跳过下使能。
                    var stopped = _controller.Stop(ControllerNo, AxisNo);
                    var disabled = _controller.ServeOff(ControllerNo, AxisNo);

                    return stopped && disabled
                        ? ElementCommandResult.Accepted(descriptor.Key, now, "急停命令已下发（停止 + 下使能）。")
                        : Failed(descriptor.Key, now, "Axis.EStopRejected", "急停");
                }

                case CommandJogForward:
                    return _controller.Jog(ControllerNo, AxisNo, direction: true)
                        ? ElementCommandResult.Accepted(descriptor.Key, now, "正向点动已启动，松开后必须停止。")
                        : Failed(descriptor.Key, now, "Axis.JogRejected", "正向点动");

                case CommandJogBackward:
                    return _controller.Jog(ControllerNo, AxisNo, direction: false)
                        ? ElementCommandResult.Accepted(descriptor.Key, now, "反向点动已启动，松开后必须停止。")
                        : Failed(descriptor.Key, now, "Axis.JogRejected", "反向点动");

                default:
                    return ElementCommandResult.Error(
                        descriptor.Key, now, "COMMAND_UNSUPPORTED", $"命令 {descriptor.Key} 未实现。");
            }
        }
        catch (AxisControllerException ex)
        {
            SetState(ElementState.Error);
            SetLastError(new AxisOperationError(
                ex.ErrorCode ?? "Axis.CommandException", ex.Message, Exception: ex));

            return ElementCommandResult.Error(
                descriptor.Key, now, ex.ErrorCode ?? "Axis.CommandException", ex.Message);
        }
        catch (Exception ex)
        {
            SetState(ElementState.Error);
            SetLastError(new AxisOperationError(
                "Axis.CommandException", ex.Message, Exception: ex));

            return ElementCommandResult.Error(
                descriptor.Key, now, "Axis.CommandException",
                $"执行 {descriptor.Key} 时发生异常：{ex.Message}");
        }
    }

    // ───────────────────────── IElementAxis 命令入口 ─────────────────────────

    /// <inheritdoc />
    public async Task<Result> ServeOnAsync(CancellationToken cancellationToken = default)
        => ToResult(await ExecuteCommandAsync(
            Request(CommandPower, (CommandParameterKeys.Enable, CommandValue.FromBool(true))),
            cancellationToken).ConfigureAwait(false));

    /// <inheritdoc />
    public async Task<Result> ServeOffAsync(CancellationToken cancellationToken = default)
        => ToResult(await ExecuteCommandAsync(
            Request(CommandPower, (CommandParameterKeys.Enable, CommandValue.FromBool(false))),
            cancellationToken).ConfigureAwait(false));

    /// <inheritdoc />
    public async Task<Result> HomeAsync(CancellationToken cancellationToken = default)
        => ToResult(await ExecuteCommandAsync(Request(CommandHome), cancellationToken).ConfigureAwait(false));

    /// <inheritdoc />
    public async Task<Result> MoveAbsAsync(double position, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(position))
        {
            return Result.Fail(
                $"轴 {_config.ElementId} 的目标位置不是有限数：{position}。",
                "Axis.InvalidPosition");
        }

        return ToResult(await ExecuteCommandAsync(
            Request(CommandMoveAbsolute, (CommandParameterKeys.Position, CommandValue.FromNumber(position))),
            cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<Result> MoveRelAsync(double offset, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(offset))
        {
            return Result.Fail(
                $"轴 {_config.ElementId} 的相对位移不是有限数：{offset}。",
                "Axis.InvalidPosition");
        }

        return ToResult(await ExecuteCommandAsync(
            Request(CommandMoveRelative, (CommandParameterKeys.Distance, CommandValue.FromNumber(offset))),
            cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<Result> ForwardJogAsync(CancellationToken cancellationToken = default)
        => ToResult(await ExecuteCommandAsync(Request(CommandJogForward), cancellationToken).ConfigureAwait(false));

    /// <inheritdoc />
    public async Task<Result> AfterJogAsync(CancellationToken cancellationToken = default)
        => ToResult(await ExecuteCommandAsync(Request(CommandJogBackward), cancellationToken).ConfigureAwait(false));

    /// <inheritdoc />
    public async Task<Result> StopAsync(CancellationToken cancellationToken = default)
        => ToResult(await ExecuteCommandAsync(Request(CommandHalt), cancellationToken).ConfigureAwait(false));

    /// <inheritdoc />
    public async Task<Result> EStopAsync(CancellationToken cancellationToken = default)
        => ToResult(await ExecuteCommandAsync(Request(CommandEStop), cancellationToken).ConfigureAwait(false));

    /// <inheritdoc />
    public async Task<Result> ResetAsync(CancellationToken cancellationToken = default)
        => ToResult(await ExecuteCommandAsync(Request(CommandReset), cancellationToken).ConfigureAwait(false));

    /// <inheritdoc />
    public Task<Result<int>> ReadStateWordAsync(
        string type,
        CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            return Task.FromResult(Result.Fail<int>(
                $"轴 {_config.ElementId} 尚未初始化，无法读取状态字。",
                "Axis.NotInitialized"));
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return Task.FromResult(
                _controller.ReadStateWord(ControllerNo, AxisNo, type, out var state)
                    ? Result.Ok(state)
                    : Result.Fail<int>(
                        $"轴 {_config.ElementId} 读取状态字 {type} 失败。",
                        "Axis.ReadStateWordFailed"));
        }
        catch (AxisControllerException ex)
        {
            return Task.FromResult(Result.Fail<int>(ex.Message, ex.ErrorCode, ex));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Fail<int>(
                $"轴 {_config.ElementId} 读取状态字 {type} 时发生异常：{ex.Message}",
                "Axis.ReadStateWordException",
                ex));
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

    // ───────────────────────── 内部实现 ─────────────────────────

    private static IReadOnlyList<ElementCommandDescriptor> BuildCommandTable(AxisElementConfig config)
    {
        // 软限位由轴卡/PLC 兜底；颠倒或不完整时只当作“没有范围提示”，不在元件层炸配置。
        var hasSoftLimits = config.SoftLimitMin is { } min
                            && config.SoftLimitMax is { } max
                            && min < max;

        var commands = new List<ElementCommandDescriptor>
        {
            new()
            {
                Key = CommandPower,
                Name = "使能",
                Description = "电平使能 / 下使能（PLCopen MC_Power）。",
                Activation = ElementCommandActivation.Enable,
                Risk = ElementCommandRisk.Normal,
                Parameters =
                [
                    new CommandParameterDescriptor
                    {
                        Key = CommandParameterKeys.Enable,
                        Name = "使能",
                        ValueKind = CommandValueKind.Bool,
                        Description = "true = 使能，false = 下使能。"
                    }
                ]
            },
            new()
            {
                Key = CommandReset,
                Name = "复位",
                Description = "清除驱动器故障（MC_Reset）。",
                Activation = ElementCommandActivation.Execute,
                Risk = ElementCommandRisk.Normal
            },
            new()
            {
                Key = CommandHome,
                Name = "回零",
                Description = "执行回零（MC_Home）；到位由状态观察。",
                Activation = ElementCommandActivation.Execute,
                Risk = ElementCommandRisk.Confirm
            },
            new()
            {
                Key = CommandMoveAbsolute,
                Name = "绝对定位",
                Description = "移动到绝对位置（MC_MoveAbsolute）。",
                Activation = ElementCommandActivation.Execute,
                Risk = ElementCommandRisk.Confirm,
                Parameters =
                [
                    new CommandParameterDescriptor
                    {
                        Key = CommandParameterKeys.Position,
                        Name = "目标位置",
                        ValueKind = CommandValueKind.Number,
                        Unit = config.Unit,
                        Precision = 3,
                        Minimum = hasSoftLimits ? config.SoftLimitMin : null,
                        Maximum = hasSoftLimits ? config.SoftLimitMax : null
                    }
                ]
            },
            new()
            {
                Key = CommandMoveRelative,
                Name = "相对定位",
                Description = "相对当前位置移动（MC_MoveRelative）。",
                Activation = ElementCommandActivation.Execute,
                Risk = ElementCommandRisk.Confirm,
                Parameters =
                [
                    new CommandParameterDescriptor
                    {
                        Key = CommandParameterKeys.Distance,
                        Name = "相对位移",
                        ValueKind = CommandValueKind.Number,
                        Unit = config.Unit,
                        Precision = 3
                    }
                ]
            },
            new()
            {
                Key = CommandHalt,
                Name = "停止",
                Description = "减速停止并保持使能（MC_Halt）。",
                Activation = ElementCommandActivation.Execute,
                Risk = ElementCommandRisk.Normal
            },
            new()
            {
                Key = CommandEStop,
                Name = "软件急停",
                Description = "停止并下使能（本项目补充，不是安全功能）。",
                Activation = ElementCommandActivation.Execute,
                Risk = ElementCommandRisk.Normal
            },
            new()
            {
                Key = CommandJogForward,
                Name = "正向点动",
                Description = "按住走、松开停。",
                Activation = ElementCommandActivation.Execute,
                Risk = ElementCommandRisk.Normal
            },
            new()
            {
                Key = CommandJogBackward,
                Name = "反向点动",
                Description = "按住走、松开停。",
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

    private CommandAvailability EvaluateCore(string commandKey, AxisStatus status, bool powerEnable)
    {
        if (_disposed)
        {
            return CommandAvailability.Blocked("轴已释放。");
        }

        if (!IsInitialized)
        {
            return CommandAvailability.Blocked("轴尚未初始化。");
        }

        if (commandKey == CommandPower)
        {
            return powerEnable && status.IsFault
                ? CommandAvailability.Blocked($"轴故障（{status.FaultCode}）：先执行 MC_Reset。")
                : CommandAvailability.Available;
        }

        if (commandKey is CommandReset or CommandHalt or CommandEStop)
        {
            return CommandAvailability.Available;
        }

        if (status.IsFault)
        {
            return CommandAvailability.Blocked($"轴故障（{status.FaultCode}）：先执行 MC_Reset。");
        }

        if (!status.IsEnabled)
        {
            return CommandAvailability.Blocked("轴未使能：先执行 MC_Power。");
        }

        return CommandAvailability.Available;
    }

    private static bool ResolvePowerEnable(ElementCommandRequest request)
        => !request.TryGetBool(CommandParameterKeys.Enable, out var enable) || enable;

    private static string? ValidateArguments(ElementCommandDescriptor descriptor, ElementCommandRequest request)
    {
        foreach (var key in request.Arguments.Keys)
        {
            if (descriptor.FindParameter(key) is null)
            {
                return $"命令 {descriptor.Key} 不支持参数 {key}。";
            }
        }

        foreach (var parameter in descriptor.Parameters)
        {
            if (!request.TryGetValue(parameter.Key, out var value))
            {
                if (parameter.IsRequired)
                {
                    return $"命令 {descriptor.Key} 缺少参数 {parameter.DisplayName}。";
                }

                continue;
            }

            switch (parameter.ValueKind)
            {
                case CommandValueKind.Bool when !value.TryAsBool(out _):
                    return $"命令 {descriptor.Key} 的参数 {parameter.DisplayName} 必须是布尔值。";

                case CommandValueKind.Number:
                {
                    if (!value.TryAsNumber(out var number))
                    {
                        return $"命令 {descriptor.Key} 的参数 {parameter.DisplayName} 必须是数值。";
                    }

                    if (parameter.IsOutOfRange(number))
                    {
                        return $"命令 {descriptor.Key} 的参数 {parameter.DisplayName}={number} " +
                               $"超出范围 [{parameter.Minimum}, {parameter.Maximum}]。";
                    }

                    break;
                }

                case CommandValueKind.Text:
                {
                    if (!value.TryAsText(out var text))
                    {
                        return $"命令 {descriptor.Key} 的参数 {parameter.DisplayName} 必须是文本。";
                    }

                    if (parameter.Choices.Count > 0 &&
                        !parameter.Choices.Contains(text, StringComparer.Ordinal))
                    {
                        return $"命令 {descriptor.Key} 的参数 {parameter.DisplayName} 取值 {text} 不在可选列表内。";
                    }

                    break;
                }
            }
        }

        return null;
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
            _logger?.Warn($"写审计失败：{eventType} {detail}{Environment.NewLine}{ex}");
        }
    }

    private static string Describe(ElementCommandRequest request, ElementCommandResult result)
    {
        var arguments = request.Arguments.Count == 0
            ? "无参数"
            : string.Join(", ", request.Arguments.Select(pair => $"{pair.Key}={pair.Value}"));

        return $"{request.CommandKey}（{arguments}）→ {result.Outcome}" +
               (string.IsNullOrWhiteSpace(result.Message) ? string.Empty : $"：{result.Message}");
    }

    private ElementCommandResult Failed(string commandKey, DateTimeOffset now, string code, string operation)
    {
        var error = _controller.GetLastError(ControllerNo, AxisNo)
                    ?? new AxisOperationError(code, $"轴 {_config.ElementId} {operation}命令被控制器拒绝。");

        SetLastError(error);
        return ElementCommandResult.Error(
            commandKey, now, error.Code, error.Message, error.VendorCode);
    }

    private static ElementCommandRequest Request(
        string commandKey,
        params (string Key, CommandValue Value)[] arguments)
    {
        var map = new Dictionary<string, CommandValue>(arguments.Length, StringComparer.Ordinal);

        foreach (var (key, value) in arguments)
        {
            map[key] = value;
        }

        return new ElementCommandRequest { CommandKey = commandKey, Arguments = map };
    }

    private static Result ToResult(Result<ElementCommandResult> result)
    {
        if (result.IsFailure)
        {
            return Result.Fail(result.Error, result.ErrorCode, result.Exception);
        }

        var command = result.Value;

        return command.Outcome == CommandOutcome.Accepted
            ? Result.Ok()
            : Result.Fail(
                command.Message ?? $"命令 {command.CommandKey} 未完成（{command.Outcome}）。",
                command.ErrorCode ?? "COMMAND_FAILED");
    }

    private AxisStatus ReadStatus()
    {
        var position = _controller.GetPos(ControllerNo, AxisNo);
        var velocity = _controller.GetVel(ControllerNo, AxisNo);
        var torque = _controller.GetTorque(ControllerNo, AxisNo);

        if (!_controller.ReadStateWord(
                ControllerNo, AxisNo, AxisStateWordTypes.StateWord, out var rawState))
        {
            throw new InvalidOperationException($"轴 {_config.ElementId} 读取状态字失败。");
        }

        // 故障码不是所有控制器都支持；读不到按 0 处理。
        var faultCode = _controller.ReadStateWord(
            ControllerNo, AxisNo, AxisStateWordTypes.FaultCode, out var code)
            ? code
            : 0;

        var feedback = new AxisFeedback
        {
            ControllerNo = ControllerNo,
            AxisNo = AxisNo,
            ActualPosition = position,
            ActualVelocity = velocity,
            ActualTorque = torque,
            StatusWord = (AxisStatusWord)rawState,
            FaultCode = faultCode
        };

        return feedback.ToAxisStatus(feedback.IsMoving ? _lastTarget : position);
    }

    private ElementState ComputeElementState(AxisStatus status)
    {
        if (!_config.IsEnabled)
        {
            return ElementState.Disabled;
        }

        if (!IsInitialized)
        {
            return ElementState.Unknown;
        }

        if (status.IsFault)
        {
            return ElementState.Error;
        }

        // 状态字 bit4：可继续运行但需要关注。以前这里直接落到 Ready/Running，
        // 报警位被静默丢弃；报警服务与总览页都需要看到它。
        if (status.IsWarning)
        {
            return ElementState.Warning;
        }

        if (status.IsHoming || status.IsMoving)
        {
            return ElementState.Running;
        }

        return ElementState.Ready;
    }

    private void SetStatus(AxisStatus status)
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

    private void SetLastError(AxisOperationError? error)
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
}
