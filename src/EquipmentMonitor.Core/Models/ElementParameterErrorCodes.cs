namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 元件参数服务的稳定错误码。UI/日志按这些值判断，不要匹配中文消息。
/// </summary>
public static class ElementParameterErrorCodes
{
    /// <summary>参数校验失败（类型/范围/跨字段）；消息中带问题列表。</summary>
    public const string ValidationFailed = "ELEMENT_PARAM_VALIDATION_FAILED";

    /// <summary>元件不存在或未装配。</summary>
    public const string ElementNotFound = "ELEMENT_PARAM_ELEMENT_NOT_FOUND";

    /// <summary>控制器不支持参数读写，无法执行 Apply 模式。</summary>
    public const string ControllerUnsupported = "ELEMENT_PARAM_CONTROLLER_UNSUPPORTED";

    /// <summary>配置版本冲突；调用方应重新读取后再合并。</summary>
    public const string VersionConflict = "ELEMENT_PARAM_VERSION_CONFLICT";

    /// <summary>已写控制器，但保存配置失败。</summary>
    public const string SaveFailed = "ELEMENT_PARAM_SAVE_FAILED";

    /// <summary>控制器写入失败。</summary>
    public const string ControllerWriteFailed = "ELEMENT_PARAM_CONTROLLER_WRITE_FAILED";
}
