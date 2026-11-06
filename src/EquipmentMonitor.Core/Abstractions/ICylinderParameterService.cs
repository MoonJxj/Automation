using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 气缸参数设计/编辑服务：读取参数目录、校验、保存配置。
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="IAxisParameterService"/> 共用 <see cref="ElementParameterDescriptor"/> /
/// <see cref="ElementParameterSet"/>，但气缸没有控制器参数读写能力；当前所有参数都是
/// <see cref="ParameterApplyMode.Restart"/>，只支持保存配置，重新装配/重启后生效。
/// </para>
/// <para><b>权限</b>：读取需要 <see cref="Permission.MachineView"/>；修改按描述符的
/// <see cref="ElementParameterDescriptor.RequiredPermission"/> 校验。</para>
/// <para><b>审计</b>：记录元件、参数键、旧值、新值、模式与结果。</para>
/// </remarks>
public interface ICylinderParameterService
{
    /// <summary>读取单个气缸的参数描述符与当前值。</summary>
    Task<Result<ElementParameterSnapshot>> GetAsync(
        string elementId,
        CancellationToken cancellationToken = default);

    /// <summary>读取全部气缸的参数快照。</summary>
    Task<Result<IReadOnlyList<ElementParameterSnapshot>>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>校验一组待修改参数；不写配置、不碰控制器。</summary>
    Task<Result<ElementParameterValidationResult>> ValidateAsync(
        string elementId,
        ElementParameterSet proposed,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存参数。
    /// </summary>
    /// <remarks>
    /// 当前只有 <see cref="ParameterUpdateMode.SaveOnly"/> 可用；`ApplyOnly` / `SaveAndApply`
    /// 应返回 <see cref="ElementParameterErrorCodes.ControllerUnsupported"/>，直到气缸具备运行时参数能力。
    /// </remarks>
    Task<Result<ElementParameterChangeResult>> UpdateAsync(
        string elementId,
        ElementParameterSet proposed,
        ParameterUpdateMode mode,
        CancellationToken cancellationToken = default);
}
