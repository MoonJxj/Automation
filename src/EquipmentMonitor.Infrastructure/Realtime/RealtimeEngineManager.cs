using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EquipmentMonitor.Core.Abstractions;
using Serilog;

namespace EquipmentMonitor.Infrastructure
{
    /// <summary>
    /// 实时引擎管理器实现：按名称维护引擎，统一启停。
    /// </summary>
    public class RealtimeEngineManager : IRealtimeEngineManager
    {
        private readonly ConcurrentDictionary<string, RealtimeEngine> _engines = new();
        private readonly ILogger _logger;

        public RealtimeEngineManager()
        {
            _logger = Log.ForContext<RealtimeEngineManager>();
        }

        public IRealtimeEngine CreateOrGetEngine(string engineName, int intervalMs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(engineName);
            if (intervalMs <= 0) throw new ArgumentOutOfRangeException(nameof(intervalMs), "周期必须大于0");

            return _engines.GetOrAdd(engineName, name =>
            {
                _logger.Information("创建新引擎: {EngineName}，周期: {Interval}ms", name, intervalMs);
                return new RealtimeEngine(name, TimeSpan.FromMilliseconds(intervalMs));
            });
        }

        public void AddTask(string engineName, IRealtimeTask task)
        {
            if (!_engines.TryGetValue(engineName, out var engine))
                throw new KeyNotFoundException($"找不到名为 {engineName} 的引擎，请先调用 CreateOrGetEngine。");

            engine.RegisterTask(task);
        }

        public void StartEngine(string engineName)
        {
            if (_engines.TryGetValue(engineName, out var engine))
            {
                engine.Start();
            }
            else
            {
                _logger.Warning("尝试启动不存在的引擎: {EngineName}", engineName);
            }
        }

        public void StartAll()
        {
            _logger.Information("正在启动所有引擎...");
            foreach (var engine in _engines.Values)
            {
                engine.Start();
            }
        }

        public async Task StopEngineAsync(string engineName)
        {
            if (_engines.TryGetValue(engineName, out var engine))
            {
                await engine.StopAsync();
            }
        }

        public async Task StopAllAsync()
        {
            _logger.Information("正在停止所有引擎...");
            var stopTasks = _engines.Values.Select(e => e.StopAsync());
            await Task.WhenAll(stopTasks);
            _logger.Information("所有引擎已停止。");
        }

        public IReadOnlyDictionary<string, bool> GetEngineStatus()
        {
            return _engines.ToDictionary(kv => kv.Key, kv => kv.Value.IsRunning);
        }

        public async ValueTask DisposeAsync()
        {
            await StopAllAsync();
            foreach (var engine in _engines.Values)
            {
                await engine.DisposeAsync();
            }
        }
    }
}
