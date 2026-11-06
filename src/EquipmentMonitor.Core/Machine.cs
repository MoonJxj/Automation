using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Events;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core;

/// <summary>
/// 机台的默认实现：持有一组元件，负责初始化、刷新，并聚合出机台级状态。
/// </summary>
/// <remarks>
/// <para>
/// <b>当前阶段（交接基线）</b>：只实现「初始化 / 刷新 / 状态聚合 / 按 ID 取元件」。
/// <see cref="StartDeviceAsync"/>、<see cref="StopTheDeviceAsync"/>、<see cref="SetPausedAsync"/>、
/// <see cref="SetMaintenanceAsync"/>、<see cref="ResetDeviceAsync"/> 需要接入元件命令管线与审计，
/// 目前显式抛 <see cref="NotSupportedException"/>，避免调用方误以为设备已经动作。
/// </para>
/// <para>
/// <b>状态聚合是临时默认规则</b>：任一启用元件 Error → 机台 Error；否则任一 Warning → Warning；
/// 否则任一 Running → Running；全部 Ready → Idle；其余 → Unknown。
/// 设计文档要求聚合规则由配置驱动，接入配置后应替换 <see cref="EvaluateAggregateState"/>。
/// </para>
/// <para>
/// 线程模型：元数据与元件集合构造后只读；状态字段由 <c>_gate</c> 保护；
/// <see cref="StateChanged"/> 在调用 <see cref="RefreshAsync"/> / <see cref="InitializeDeviceAsync"/>
/// 的线程上触发，UI 订阅方自行 <c>BeginInvoke</c>。
/// </para>
/// </remarks>
public sealed class Machine : IMachine
{
    private readonly MachineMetadata _metadata;
    private readonly IReadOnlyCollection<IElement> _elements;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();

    private MachineState _state = MachineState.Unknown;
    private DateTimeOffset? _lastStateChangedAt;

    /// <summary>
    /// 创建机台。
    /// </summary>
    /// <param name="metadata">静态元数据。</param>
    /// <param name="elements">机台下的元件；构造后不再增删。</param>
    /// <param name="timeProvider">时间源；测试注入 <c>FakeTimeProvider</c>，生产传 <c>null</c>。</param>
    public Machine(
        MachineMetadata metadata,
        IEnumerable<IElement> elements,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(elements);

        _metadata = metadata;
        _elements = elements.ToArray();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public MachineMetadata Metadata => _metadata;

    /// <inheritdoc />
    public MachineState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IElement> Elements => _elements;

    /// <inheritdoc />
    public DateTimeOffset? LastStateChangedAt
    {
        get
        {
            lock (_gate)
            {
                return _lastStateChangedAt;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<MachineStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// 初始化所有启用的元件，成功后把机台状态聚合为 <see cref="MachineState.Idle"/>。
    /// </summary>
    public async Task<Result> InitializeDeviceAsync(CancellationToken ct = default)
    {
        var failures = new List<string>();

        foreach (var element in EnabledElements())
        {
            ct.ThrowIfCancellationRequested();

            var result = await element.InitializeAsync().ConfigureAwait(false);
            if (result.IsFailure)
            {
                failures.Add($"{element.ElementMetadata.ElementId}: {result.Error}");
            }
        }

        if (failures.Count > 0)
        {
            TransitionTo(MachineState.Error, "元件初始化失败", sourceElementId: null, isManual: false);
            return Result.Fail(
                $"机台 {_metadata.MachineCode} 有 {failures.Count} 个元件初始化失败：{string.Join("; ", failures)}",
                "Machine.InitializeFailed");
        }

        var aggregate = EvaluateAggregateState(out var sourceElementId);
        TransitionTo(aggregate, "初始化完成", sourceElementId, isManual: false);
        return Result.Ok();
    }

    /// <summary>
    /// 刷新所有启用的元件，并按聚合规则更新机台状态。
    /// </summary>
    public async Task<Result> RefreshAsync()
    {
        var failures = new List<string>();

        foreach (var element in EnabledElements())
        {
            var result = await element.RefreshAsync().ConfigureAwait(false);
            if (result.IsFailure)
            {
                failures.Add($"{element.ElementMetadata.ElementId}: {result.Error}");
            }
        }

        if (failures.Count > 0)
        {
            TransitionTo(MachineState.Error, "元件刷新失败", sourceElementId: null, isManual: false);
            return Result.Fail(
                $"机台 {_metadata.MachineCode} 有 {failures.Count} 个元件刷新失败：{string.Join("; ", failures)}",
                "Machine.RefreshFailed");
        }

        var aggregate = EvaluateAggregateState(out var sourceElementId);
        TransitionTo(aggregate, "元件状态聚合", sourceElementId, isManual: false);
        return Result.Ok();
    }

    /// <inheritdoc />
    public Result<T> GetElement<T>(string elementId) where T : class, IElement
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);

        var element = _elements.FirstOrDefault(
            e => string.Equals(e.ElementMetadata.ElementId, elementId, StringComparison.Ordinal));

        if (element is null)
        {
            return Result.Fail<T>(
                $"机台 {_metadata.MachineCode} 下不存在元件 {elementId}。",
                "Machine.ElementNotFound");
        }

        if (element is not T typed)
        {
            return Result.Fail<T>(
                $"元件 {elementId} 的实际类型为 {element.GetType().Name}，不能按 {typeof(T).Name} 使用。",
                "Machine.ElementTypeMismatch");
        }

        return Result.Ok(typed);
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// 启动需要接入元件命令管线与审计，当前基线尚未实现。
    /// </exception>
    public Task<Result> StartDeviceAsync(string reason, CancellationToken ct = default)
        => throw new NotSupportedException(
            "机台启动尚未实现：需要先接入元件命令管线与启动审计，不能只改状态位。");

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// 停止需要等待硬件静止并接入审计，当前基线尚未实现。
    /// </exception>
    public Task<Result> StopTheDeviceAsync(string reason, CancellationToken ct = default)
        => throw new NotSupportedException(
            "机台停止尚未实现：需要接入元件停止命令并等待硬件静止，不能只改状态位。");

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// 暂停/恢复需要与自动流程状态机联动，当前基线尚未实现。
    /// </exception>
    public Task<Result> SetPausedAsync(bool paused, string reason, CancellationToken ct = default)
        => throw new NotSupportedException(
            "机台暂停/恢复尚未实现：需要接入自动流程状态机，不能只改状态位。");

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// 维护切换需要与权限及审计联动，当前基线尚未实现。
    /// </exception>
    public Task<Result> SetMaintenanceAsync(bool entering, string reason, CancellationToken ct = default)
        => throw new NotSupportedException(
            "机台维护切换尚未实现：需要接入权限校验与审计，不能只改状态位。");

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// 复位需要按元件顺序执行并校验结果，当前基线尚未实现。
    /// </exception>
    public Task<Result> ResetDeviceAsync(CancellationToken ct = default)
        => throw new NotSupportedException(
            "机台复位尚未实现：需要按元件顺序执行复位命令并校验回读结果。");

    private IElement[] EnabledElements()
        => _elements.Where(e => e.ElementMetadata.IsEnabled).ToArray();

    /// <summary>
    /// 按临时默认规则聚合机台状态，并给出触发聚合的元件。
    /// </summary>
    private MachineState EvaluateAggregateState(out string? sourceElementId)
    {
        sourceElementId = null;
        var elements = EnabledElements();

        var error = elements.FirstOrDefault(e => e.State == ElementState.Error);
        if (error is not null)
        {
            sourceElementId = error.ElementMetadata.ElementId;
            return MachineState.Error;
        }

        var warning = elements.FirstOrDefault(e => e.State == ElementState.Warning);
        if (warning is not null)
        {
            sourceElementId = warning.ElementMetadata.ElementId;
            return MachineState.Warning;
        }

        var running = elements.FirstOrDefault(e => e.State == ElementState.Running);
        if (running is not null)
        {
            sourceElementId = running.ElementMetadata.ElementId;
            return MachineState.Running;
        }

        if (elements.Length > 0 && elements.All(e => e.State == ElementState.Ready))
        {
            return MachineState.Idle;
        }

        return MachineState.Unknown;
    }

    /// <summary>
    /// 更新状态并在变化时触发 <see cref="StateChanged"/>。事件在锁外触发，避免订阅方死锁。
    /// </summary>
    private void TransitionTo(
        MachineState newState,
        string trigger,
        string? sourceElementId,
        bool isManual)
    {
        MachineState oldState;
        DateTimeOffset now;
        DateTimeOffset? oldChangedAt;

        lock (_gate)
        {
            oldState = _state;
            if (oldState == newState)
            {
                return;
            }

            oldChangedAt = _lastStateChangedAt;
            now = _timeProvider.GetUtcNow();
            _state = newState;
            _lastStateChangedAt = now;
        }

        StateChanged?.Invoke(
            this,
            new MachineStateChangedEventArgs
            {
                MachineCode = _metadata.MachineCode,
                OldState = oldState,
                NewState = newState,
                Trigger = trigger,
                SourceElementId = sourceElementId,
                DurationMs = oldChangedAt is null ? null : (now - oldChangedAt.Value).TotalMilliseconds,
                Timestamp = now,
                IsManual = isManual
            });
    }
}
