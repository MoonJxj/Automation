namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 权限点 —— 系统中最小的授权单位。
/// </summary>
/// <remarks>
/// <para>权限点是<b>固定的枚举</b>，不是可编辑的数据：每个权限点都对应代码中一段真实的判断
/// （<c>RequirePermission(Permission.MachineOperate)</c>）。角色到权限点的映射属于安全模块。</para>
/// <para>枚举数值不表达优先级或包含关系；授权一律查决策，不做数值比较。</para>
/// </remarks>
public enum Permission
{
    /// <summary>查看设备状态、监控画面。只读。</summary>
    MachineView = 0,

    /// <summary>操作设备：启停、手动调试、写入点位。</summary>
    /// <remarks>⚠️ 此权限会直接导致现场设备动作。相关界面必须提供二次确认。</remarks>
    MachineOperate = 1,

    /// <summary>修改设备配置：通道参数、轴参数、点位映射、轮询周期。</summary>
    MachineConfig = 2,

    /// <summary>修改工艺参数与配方：速度/加速度默认值、工艺窗口、配方切换。</summary>
    /// <remarks>与 <see cref="MachineConfig"/> 分开，是为了让「工艺」与「设备」两个岗位分权：
    /// 工艺可以调工艺参数，但不应该改通道地址与轴卡参数。</remarks>
    ProcessConfig = 5,

    /// <summary>用户管理：增删改用户、分配角色、重置密码。</summary>
    UserManage = 3,

    /// <summary>查看系统日志与审计日志。</summary>
    LogView = 4,

    /// <summary>
    /// 确认报警：标记「已知悉」，不改变设备状态。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="MachineOperate"/> 分开：确认报警不产生设备动作，
    /// 不应让「只想确认报警的人」顺带获得启停/点动权限。
    /// </remarks>
    AlarmAcknowledge = 6
}
