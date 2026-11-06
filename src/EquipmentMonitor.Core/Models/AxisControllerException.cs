namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 控制器/适配器在同步调用边界上抛出的业务异常。
/// </summary>
/// <remarks>
/// <para>
/// 同步 <c>IAxisController</c> 的 <c>bool</c> 返回值无法携带错误码；
/// 适配器（例如 Modbus 异步桥）在无法用 <c>false</c> 表达“离线/超时/从站异常”时抛本异常，
/// 由 <c>AxisElement</c> 统一转换成 <see cref="AxisOperationError"/> / <c>Result.Fail</c>。
/// </para>
/// <para>
/// 这是 Core 契约，不是某个基础设施实现的内部异常；外部控制器适配器可以复用它，
/// 不需要引用 Infrastructure。
/// </para>
/// </remarks>
public sealed class AxisControllerException : Exception
{
    /// <summary>创建异常。</summary>
    /// <param name="message">可读消息。</param>
    /// <param name="errorCode">稳定错误码，例如 <c>Modbus.NotConnected</c>；可为空。</param>
    /// <param name="innerException">原始异常；可为空。</param>
    public AxisControllerException(
        string message,
        string? errorCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>原始错误码。</summary>
    public string? ErrorCode { get; }
}
