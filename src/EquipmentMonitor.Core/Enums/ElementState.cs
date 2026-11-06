namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 元件级状态。与 <see cref="MachineState"/> 是两套独立的状态机。
/// </summary>
/// <remarks>
/// <para>
/// 元件的状态只反映物理事实（Ready, Running, Error 等），
/// 不包含人为管理意图（如维护、暂停），因为“暂停一个气缸”或“维护一个传感器”在业务上毫无意义。
/// </para>
/// <para>
/// <b>禁用（Disabled）与错误（Error）的区别：</b>
/// 禁用的元件是配置决定的（计划内的，不参与轮询），而错误是运行时的突发故障（计划外的）。
/// </para>
/// </remarks>
public enum ElementState
{
    /// <summary>
    /// 未知。元件刚创建，尚未执行初始化。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 禁用。配置文件中标记为 <see cref="ElementMetadata.IsEnabled"/> = false。
    /// </summary>
    /// <remarks>
    /// 处于此状态的元件<b>绝对不参与轮询</b>，也<b>不影响机台状态的聚合</b>。
    /// 例如：预留工位、已拆除的传感器。
    /// </remarks>
    Disabled = 1,

    /// <summary>
    /// 就绪/待机。初始化成功，设备已使能，等待动作指令。
    /// </summary>
    /// <remarks>
    /// 对应伺服的“伺服使能（Servo On）”状态，或气缸的“停在原点或目标位”。
    /// </remarks>
    Ready = 2,

    /// <summary>
    /// 运行中。元件正在执行动作。
    /// </summary>
    /// <remarks>
    /// 对应伺服的“走位中”，或气缸的“动作中”。此时通常不接受新的控制指令。
    /// </remarks>
    Running = 3,

    /// <summary>
    /// 警告。非致命异常，设备尚能运行，但需要关注。
    /// </summary>
    /// <remarks>
    /// 例如：伺服跟随误差变大、传感器数值接近量程边缘、气压轻微不足。
    /// </remarks>
    Warning = 4,

    /// <summary>
    /// 错误/故障。致命异常，设备无法继续运行，需人工介入。
    /// </summary>
    /// <remarks>
    /// 例如：伺服过载、断线、严重超程、气缸动作超时。
    /// 必须通过调用 <c>Reset()</c> 方法才能退出此状态。
    /// </remarks>
    Error = 5
}
