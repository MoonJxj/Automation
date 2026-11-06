namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 审计写入：谁、何时、做了什么、结果如何。
/// </summary>
/// <remarks>
/// <para><b>审计失败不得影响业务结果</b>：实现方内部吞掉异常并记日志，
/// 调用方不需要为“审计写不进去”回滚一条已经发出的设备命令 —— 那会让动作与记录更不一致。</para>
/// <para><b>当前实现状态</b>：新框架尚未接数据库，桌面宿主注册的是
/// <c>LogAuditService</c>（写结构化日志）。现场版本须落库并永久保留。</para>
/// <para><b>操作者</b>：由实现方从当前会话补齐，调用方只描述事件本身；
/// 这样不会出现“某个调用点忘了带操作者”的记录。</para>
/// </remarks>
public interface IAuditService
{
    /// <summary>
    /// 记录一条审计。
    /// </summary>
    /// <param name="eventType">事件类型，取 <see cref="AuditEventTypes"/> 的常量。</param>
    /// <param name="detail">可读详情（命令、参数、结果；操作者由实现补充）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RecordAsync(string eventType, string detail, CancellationToken cancellationToken = default);
}

/// <summary>审计事件类型常量。</summary>
public static class AuditEventTypes
{
    /// <summary>命令被执行且到达成功终态。</summary>
    public const string CommandExecuted = "COMMAND_EXECUTED";

    /// <summary>命令被执行但终态失败（Error / Timeout / Aborted）。</summary>
    public const string CommandFailed = "COMMAND_FAILED";

    /// <summary>命令被接受层拒绝（未发出报文）：未知命令 / 参数非法 / 互锁 / 未就绪。</summary>
    public const string CommandRejected = "COMMAND_REJECTED";

    /// <summary>登录成功。</summary>
    public const string SignInSucceeded = "SIGN_IN_SUCCEEDED";

    /// <summary>登录失败（<b>不记录凭据</b>）。</summary>
    public const string SignInFailed = "SIGN_IN_FAILED";

    /// <summary>登出（含切换用户、退出程序）。</summary>
    public const string SignOut = "SIGN_OUT";

    /// <summary>权限校验未通过（越权尝试）。</summary>
    /// <remarks>当前由业务入口的异常兜底与日志记录；待审计落库后补齐自动写入。</remarks>
    public const string PermissionDenied = "PERMISSION_DENIED";

    /// <summary>新建用户账号。</summary>
    public const string UserCreated = "USER_CREATED";

    /// <summary>修改用户资料或角色。</summary>
    public const string UserUpdated = "USER_UPDATED";

    /// <summary>启用 / 停用账号。</summary>
    public const string UserEnabledChanged = "USER_ENABLED_CHANGED";

    /// <summary>设置 / 清除口令（<b>不记录口令本身</b>）。</summary>
    public const string UserPasswordChanged = "USER_PASSWORD_CHANGED";

    /// <summary>删除用户账号。</summary>
    public const string UserDeleted = "USER_DELETED";

    /// <summary>确认一条报警。</summary>
    public const string AlarmAcknowledged = "ALARM_ACKNOWLEDGED";

    /// <summary>批量确认报警。</summary>
    public const string AlarmAcknowledgedAll = "ALARM_ACKNOWLEDGED_ALL";

    /// <summary>工位屏蔽状态变更（屏蔽 / 恢复）。</summary>
    public const string StationBypassChanged = "STATION_BYPASS_CHANGED";

    /// <summary>设备级启动序列成功完成。</summary>
    public const string MachineRunStarted = "MACHINE_RUN_STARTED";

    /// <summary>设备级停止序列成功完成。</summary>
    public const string MachineRunStopped = "MACHINE_RUN_STOPPED";

    /// <summary>设备级启动/停止序列失败或未完整完成。</summary>
    public const string MachineRunFailed = "MACHINE_RUN_FAILED";

    /// <summary>设备级复位轴故障。</summary>
    public const string MachineAxisReset = "MACHINE_AXIS_RESET";
}
