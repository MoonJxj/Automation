using EquipmentMonitor.Core;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// <see cref="AxisFeedback"/>（控制器原始反馈）→ <see cref="AxisStatus"/>（领域快照）的映射。
/// </summary>
/// <remarks>
/// <para>
/// 两个类型职责不同：<see cref="AxisFeedback"/> 保留控制器读到的原始值（位置/速度/扭矩/状态字/故障码），
/// <see cref="AxisStatus"/> 是业务与 UI 使用的领域状态（含到位、静止、单轴状态等推导值）。
/// 映射集中在这里，避免每个控制器实现各写一套位解释。
/// </para>
/// <para>
/// 上下限位目前不在 <see cref="AxisStatusWord"/> 的位定义里（见 <c>docs/axis-command-interface.md</c>），
/// 因此这里先置 <c>false</c>；等 PLC 契约增加限位位后再补，不要用“位置接近软限位”去猜。
/// </para>
/// </remarks>
public static class AxisFeedbackExtensions
{
    /// <summary>
    /// 转换为轴状态快照。
    /// </summary>
    /// <param name="feedback">控制器反馈。</param>
    /// <param name="targetPosition">目标位置；无进行中的定位命令时传当前位置。</param>
    public static AxisStatus ToAxisStatus(this AxisFeedback feedback, double targetPosition)
    {
        ArgumentNullException.ThrowIfNull(feedback);

        var isStandstill = feedback.IsEnabled && !feedback.IsMoving && !feedback.IsHoming;

        return new AxisStatus
        {
            IsEnabled = feedback.IsEnabled,
            IsUpperLimit = false,
            IsHome = feedback.IsHomeCompleted,
            IsLowerLimit = false,
            IsHoming = feedback.IsHoming,
            IsStandstill = isStandstill,
            IsMoving = feedback.IsMoving,
            IsErrorstop = feedback.IsFault,
            InPosition = feedback.InPosition,
            ActualPosition = feedback.ActualPosition,
            ActualVelocity = feedback.ActualVelocity,
            ActualTorque = feedback.ActualTorque,
            TargetPosition = targetPosition,
            IsFault = feedback.IsFault,
            IsWarning = feedback.HasAlarm,
            FaultCode = feedback.FaultCode,
            UniaxialState = DeriveUniaxialState(feedback, isStandstill)
        };
    }

    private static UniaxialState DeriveUniaxialState(AxisFeedback feedback, bool isStandstill)
    {
        if (feedback.IsFault)
        {
            return UniaxialState.Errorstop;
        }

        if (!feedback.IsEnabled)
        {
            return UniaxialState.Disabled;
        }

        if (feedback.IsHoming)
        {
            return UniaxialState.Homing;
        }

        if (feedback.IsMoving)
        {
            return feedback.IsPositioning ? UniaxialState.Discrete : UniaxialState.Continuous;
        }

        return isStandstill ? UniaxialState.Standstill : UniaxialState.Unknown;
    }
}
