namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 实时引擎：按固定周期执行一组 <see cref="IRealtimeTask"/>。
/// </summary>
public interface IRealtimeEngine
{
    /// <summary>引擎名称。</summary>
    string Name { get; }

    /// <summary>执行周期。</summary>
    TimeSpan Interval { get; }

    /// <summary>是否正在运行。</summary>
    bool IsRunning { get; }

    /// <summary>注册任务；运行中不允许动态注册。</summary>
    void RegisterTask(IRealtimeTask task);

    /// <summary>启动。</summary>
    void Start();

    /// <summary>停止并等待循环退出。</summary>
    Task StopAsync();
}
