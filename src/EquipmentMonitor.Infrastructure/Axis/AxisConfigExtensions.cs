using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Infrastructure.Axis;

/// <summary>
/// <see cref="AxisConfig"/>（数据库实体）→ <see cref="AxisElementConfig"/>（运行期快照）的映射。
/// </summary>
/// <remarks>
/// <para>
/// 映射放在 Infrastructure，而不是让 Core 依赖数据库实体：轴元件只认不可变配置，
/// ORM 列、SqlSugar 特性、表名都不应该进入 Core。
/// </para>
/// <para>
/// <b>ElementId</b>：<see cref="AxisConfig"/> 目前没有独立的逻辑编码列，这里用
/// <c>Axis-{controllerNo}-{axisNo}</c> 生成稳定标识。若现场需要“X 轴”这类可读编码，
/// 应在 <see cref="AxisConfig"/> 增加 <c>ElementId</c> 列并迁移，而不是拿 <c>Name</c>
/// 当唯一键（显示名可改，改完历史数据就对不上了）。
/// </para>
/// </remarks>
public static class AxisConfigExtensions
{
    /// <summary>
    /// 把数据库实体转换为轴元件配置。
    /// </summary>
    /// <param name="axis">轴实体。</param>
    /// <param name="controllerNo">控制器编号（来自 <see cref="ControllerConfig.ControllerNo"/>，不是数据库 Id）。</param>
    /// <param name="elementId">可选的逻辑编码；不传时使用 <c>Axis-{controllerNo}-{axisNo}</c>。</param>
    public static AxisElementConfig ToElementConfig(
        this AxisConfig axis,
        int controllerNo,
        string? elementId = null)
    {
        ArgumentNullException.ThrowIfNull(axis);

        var config = new AxisElementConfig
        {
            ElementId = string.IsNullOrWhiteSpace(elementId)
                ? $"Axis-{controllerNo}-{axis.AxisNo}"
                : elementId,
            Name = string.IsNullOrWhiteSpace(axis.Name) ? $"Axis-{axis.AxisNo}" : axis.Name,
            ControllerNo = controllerNo,
            AxisNo = axis.AxisNo,
            StationId = axis.StationId,
            Unit = string.IsNullOrWhiteSpace(axis.Unit) ? "mm" : axis.Unit,
            MaxVelocity = axis.MaxVelocity,
            SoftLimitMin = axis.SoftLimitMin,
            SoftLimitMax = axis.SoftLimitMax,
            DefaultVelocity = axis.DefaultVelocity ?? 10.0,
            Acceleration = axis.Acceleration ?? 100.0,
            Deceleration = axis.Deceleration ?? 100.0,
            Jerk = axis.Jerk,
            HomeMethod = axis.HomeMethod,
            IsEnabled = axis.IsEnabled
        };

        // 配置错误在装配期炸掉，而不是等操作工按下“定位”才发现 SoftLimitMin > Max。
        config.Validate();
        return config;
    }
}
