using EquipmentMonitor.Common;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 用户账号的输入校验规则。界面与用户目录共用同一份，避免“界面允许、服务拒绝”。
/// </summary>
public static class UserRules
{
    /// <summary>账号名最小长度。</summary>
    public const int MinUserNameLength = 2;

    /// <summary>账号名最大长度。</summary>
    public const int MaxUserNameLength = 32;

    /// <summary>显示名最大长度。</summary>
    public const int MaxDisplayNameLength = 40;

    /// <summary>校验账号名：字母/数字开头，允许 <c>. _ -</c>，长度 2–32。</summary>
    public static Result ValidateUserName(string? userName)
    {
        var value = userName?.Trim() ?? string.Empty;

        if (value.Length < MinUserNameLength)
        {
            return Result.Fail(
                $"账号名至少 {MinUserNameLength} 个字符。",
                SecurityErrorCodes.InvalidUserName);
        }

        if (value.Length > MaxUserNameLength)
        {
            return Result.Fail(
                $"账号名最多 {MaxUserNameLength} 个字符。",
                SecurityErrorCodes.InvalidUserName);
        }

        if (!char.IsAsciiLetterOrDigit(value[0]))
        {
            return Result.Fail(
                "账号名必须以字母或数字开头（可含 . _ -）。",
                SecurityErrorCodes.InvalidUserName);
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-'))
            {
                return Result.Fail(
                    "账号名只能包含字母、数字与 . _ -。",
                    SecurityErrorCodes.InvalidUserName);
            }
        }

        return Result.Ok();
    }

    /// <summary>校验显示名：非空，最多 40 个字符。</summary>
    public static Result ValidateDisplayName(string? displayName)
    {
        var value = displayName?.Trim() ?? string.Empty;

        if (value.Length == 0)
        {
            return Result.Fail("显示名不能为空。", SecurityErrorCodes.InvalidDisplayName);
        }

        if (value.Length > MaxDisplayNameLength)
        {
            return Result.Fail(
                $"显示名最多 {MaxDisplayNameLength} 个字符。",
                SecurityErrorCodes.InvalidDisplayName);
        }

        return Result.Ok();
    }

    /// <summary>去除首尾空白后的账号名（比较与存储都用这个形式）。</summary>
    public static string NormalizeUserName(string? userName) => userName?.Trim() ?? string.Empty;
}
