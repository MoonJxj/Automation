namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 轴操作失败的详细信息。
/// </summary>
/// <remarks>
/// 厂商 DLL 通常只返回 <c>bool</c>，无法区分“离线/超时/参数非法/驱动器拒绝”。
/// 控制器实现应在失败时记录本对象，由 <c>GetLastError</c> 交给上层，
/// 避免把原因压成一句“命令被拒绝”。
/// </remarks>
/// <param name="Code">稳定错误码，例如 <c>Axis.NotEnabled</c>。</param>
/// <param name="Message">可读错误信息。</param>
/// <param name="VendorCode">厂商错误码（可选）。</param>
/// <param name="Exception">内部异常（可选，仅用于日志）。</param>
public sealed record AxisOperationError(
    string Code,
    string Message,
    int? VendorCode = null,
    Exception? Exception = null);
