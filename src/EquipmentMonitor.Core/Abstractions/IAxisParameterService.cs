using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 轴参数设计/编辑服务：读取参数目录、校验、保存配置、按需下发控制器。
/// </summary>
/// <remarks>
/// <para><b>职责</b>：把“参数描述符 + 当前值 + 权限 + 校验 + 持久化 + 控制器下发 + 审计”
/// 收在一个入口；UI 只负责收集输入和展示结果，不直接读配置文件、不直接调控制器。</para>
/// <para><b>权限</b>：读取需要 <see cref="Permission.MachineView"/>；每个被修改的参数按
/// <see cref="ElementParameterDescriptor.RequiredPermission"/> 校验，混合权限时取最严。</para>
/// <para><b>审计</b>：每次成功/失败/拒绝都记录参数键、旧值、新值、模式与结果；
/// <b>不记录</b>凭据类字段（当前轴参数没有凭据，但接口约定先写死）。</para>
/// <para><b>更新顺序</b>：<see cref="ParameterUpdateMode.SaveAndApply"/> 先写控制器、
/// 成功后保存配置；控制器写失败则不保存。若控制器成功但保存失败，返回失败并说明
/// “已下发、未落盘”，由调用方决定重试保存或回滚。</para>
/// <para><b>生效方式</b>：<see cref="ParameterApplyMode.Restart"/> 的参数只保存，
/// 返回结果 <see cref="ElementParameterChangeResult.RestartRequired"/> 为 true；
/// <see cref="ParameterApplyMode.Immediate"/> / <see cref="ParameterApplyMode.NextCommand"/>
/// 需要控制器实现 <see cref="IAxisParameterController"/>，否则拒绝 Apply 模式。</para>
/// </remarks>
public interface IAxisParameterService
{
    /// <summary>读取单根轴的参数描述符与当前值。</summary>
    Task<Result<ElementParameterSnapshot>> GetAsync(
        string axisId,
        CancellationToken cancellationToken = default);

    /// <summary>读取全部轴的参数快照。</summary>
    Task<Result<IReadOnlyList<ElementParameterSnapshot>>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 校验一组待修改参数；不写配置、不碰控制器。
    /// </summary>
    /// <param name="axisId">轴标识。</param>
    /// <param name="proposed">待校验的完整参数集或变更集（缺省字段按当前值补齐）。</param>
    Task<Result<ElementParameterValidationResult>> ValidateAsync(
        string axisId,
        ElementParameterSet proposed,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存/下发参数。
    /// </summary>
    /// <param name="axisId">轴标识。</param>
    /// <param name="proposed">待更新的参数集。</param>
    /// <param name="mode">保存、下发或两者都做。</param>
    Task<Result<ElementParameterChangeResult>> UpdateAsync(
        string axisId,
        ElementParameterSet proposed,
        ParameterUpdateMode mode,
        CancellationToken cancellationToken = default);
}
