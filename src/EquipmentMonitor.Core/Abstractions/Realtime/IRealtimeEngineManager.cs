namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 实时引擎管理器：按名称创建/获取引擎并统一启停。
/// </summary>
public interface IRealtimeEngineManager : IAsyncDisposable
{
    /// <summary>创建或获取指定名称的引擎。</summary>
    IRealtimeEngine CreateOrGetEngine(string engineName, int intervalMs);

    /// <summary>向指定引擎注册任务。</summary>
    void AddTask(string engineName, IRealtimeTask task);

    /// <summary>启动指定引擎。</summary>
    void StartEngine(string engineName);

    /// <summary>启动全部引擎。</summary>
    void StartAll();

    /// <summary>停止指定引擎。</summary>
    Task StopEngineAsync(string engineName);

    /// <summary>停止全部引擎。</summary>
    Task StopAllAsync();

    /// <summary>获取各引擎运行状态。</summary>
    IReadOnlyDictionary<string, bool> GetEngineStatus();
}
