namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 实时任务接口：由实时引擎按固定周期调用。
/// </summary>
public interface IRealtimeTask
{
    /// <summary>任务名称（日志与调试用）。</summary>
    string Name { get; }

    /// <summary>每次循环执行的逻辑（支持异步）。</summary>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
