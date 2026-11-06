using EquipmentMonitor.Common;

namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 口令强度规则。只约束长度，不搞“必须含大小写+符号”的组合要求 ——
/// 工业现场更可能因此把口令写在显示器上。
/// </summary>
public static class PasswordRules
{
    /// <summary>口令最小长度。</summary>
    public const int MinLength = 8;

    /// <summary>口令最大长度（防止把超长串塞进哈希）。</summary>
    public const int MaxLength = 128;

    /// <summary>校验口令；失败时返回可直接展示给用户的原因。</summary>
    public static Result Validate(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return Result.Fail("口令不能为空。", SecurityErrorCodes.InvalidPassword);
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return Result.Fail("口令不能全部是空白字符。", SecurityErrorCodes.InvalidPassword);
        }

        if (password.Length < MinLength)
        {
            return Result.Fail($"口令至少 {MinLength} 个字符。", SecurityErrorCodes.InvalidPassword);
        }

        if (password.Length > MaxLength)
        {
            return Result.Fail($"口令最多 {MaxLength} 个字符。", SecurityErrorCodes.InvalidPassword);
        }

        return Result.Ok();
    }
}
