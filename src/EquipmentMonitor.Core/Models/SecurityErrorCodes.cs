namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 用户管理与认证的错误码，供界面/测试按码判断，不靠比较中文文案。
/// </summary>
public static class SecurityErrorCodes
{
    /// <summary>账号名不合法。</summary>
    public const string InvalidUserName = "USER_INVALID_NAME";

    /// <summary>显示名不合法。</summary>
    public const string InvalidDisplayName = "USER_INVALID_DISPLAY";

    /// <summary>角色值不在定义内。</summary>
    public const string InvalidRole = "USER_INVALID_ROLE";

    /// <summary>账号名已存在（大小写不敏感）。</summary>
    public const string DuplicateUserName = "USER_DUPLICATE";

    /// <summary>账号不存在。</summary>
    public const string UserNotFound = "USER_NOT_FOUND";

    /// <summary>不能停用 / 删除 / 降级最后一个启用中的管理员。</summary>
    public const string LastAdminProtected = "USER_LAST_ADMIN";

    /// <summary>不能停用或删除当前登录账号。</summary>
    public const string SelfProtected = "USER_SELF_PROTECTED";

    /// <summary>口令不符合规则。</summary>
    public const string InvalidPassword = "USER_INVALID_PASSWORD";

    /// <summary>用户文件读写失败。</summary>
    public const string StoreUnavailable = "USER_STORE_UNAVAILABLE";

    /// <summary>登录：未指定账号。</summary>
    public const string AuthenticationUserRequired = "AUTH_USER_REQUIRED";

    /// <summary>登录：账号不存在。</summary>
    public const string AuthenticationUnknownUser = "AUTH_USER_NOT_FOUND";

    /// <summary>登录：账号已停用。</summary>
    public const string AuthenticationDisabledUser = "AUTH_USER_DISABLED";

    /// <summary>登录：口令不正确。</summary>
    public const string AuthenticationBadCredential = "AUTH_BAD_CREDENTIAL";
}
