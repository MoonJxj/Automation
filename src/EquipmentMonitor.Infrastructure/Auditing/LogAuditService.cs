using EquipmentMonitor.Core.Abstractions;
using Serilog;

namespace EquipmentMonitor.Infrastructure.Auditing;

/// <summary>
/// 审计的日志实现：把审计写进结构化日志（带 <c>SourceContext</c>），暂不落库。
/// </summary>
/// <remarks>
/// <para>新框架尚未接数据库，先保证“每条命令都有记录”这一行为存在。
/// 现场版本必须替换为落库实现（永久保留、可按操作者/时间查询）。</para>
/// <para><b>操作者由本类补齐</b>：从当前会话取，调用方只描述事件本身，
/// 这样不会出现“某个调用点忘了带操作者”的记录。</para>
/// <para>写日志失败不影响业务：<see cref="RecordAsync"/> 内部吞掉异常并尽量记一条 Error。</para>
/// </remarks>
public sealed class LogAuditService : IAuditService
{
    private static readonly ILogger Logger = Log.ForContext<LogAuditService>();

    private readonly ISessionProvider? _session;

    /// <summary>创建日志版审计。</summary>
    /// <param name="session">
    /// 会话持有者；为空时审计仍可用，只是操作者记为“未登录”（测试与引导期使用）。
    /// </param>
    public LogAuditService(ISessionProvider? session = null)
    {
        _session = session;
    }

    /// <inheritdoc />
    public Task RecordAsync(string eventType, string detail, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(detail);

        var session = _session?.Current;

        try
        {
            Logger.Information(
                "审计事件 {AuditEventType}：{AuditDetail}（操作者 {OperatorName}，角色 {OperatorRole}）",
                eventType,
                detail,
                session?.UserName ?? "(未登录)",
                session?.Role.ToString() ?? "-");
        }
        catch (Exception ex)
        {
            // 审计失败不得影响业务结果。
            try
            {
                Logger.Error(ex, "写审计日志失败：{AuditEventType}", eventType);
            }
            catch
            {
                // 连日志都写不出去时没有别的降级手段。
            }
        }

        return Task.CompletedTask;
    }
}
