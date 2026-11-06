using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Desktop.FunctionSelection;

/// <summary>功能入口的接入状态。</summary>
public enum FunctionAvailability
{
    /// <summary>已接入：点击后打开对应页面 / 对话框。</summary>
    Ready,

    /// <summary>尚未接入：点击只给出说明，不打开空窗口。</summary>
    Planned
}

/// <summary>
/// 功能选择页的一行数据：入口标识、文案、所需权限与接入状态。
/// </summary>
/// <param name="Key">稳定标识，用于分发处理程序；不随标题变化。</param>
/// <param name="Title">卡片标题。</param>
/// <param name="Summary">一句话说明（未来接入什么、依赖什么）。</param>
/// <param name="Required">打开 / 执行该功能所需的权限。</param>
/// <param name="Availability">是否已接入。</param>
public sealed record FunctionEntry(
    string Key,
    string Title,
    string Summary,
    Permission Required,
    FunctionAvailability Availability);
