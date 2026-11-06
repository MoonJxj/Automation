using EquipmentMonitor.Core.Enums;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 一种可选认证方式的描述，供登录界面列出并将来在“本地 / Windows / 远端”之间切换。
/// </summary>
/// <param name="Source">认证方式标识。</param>
/// <param name="DisplayName">界面显示名，例如“本地角色登录”。</param>
/// <param name="IsAvailable">当前环境是否可用；不可用的方式应置灰而不是点了报错。</param>
public sealed record AuthenticationProviderInfo(
    AuthenticationSource Source,
    string DisplayName,
    bool IsAvailable);
