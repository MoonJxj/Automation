using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EquipmentMonitor.Core.Abstractions;
using Serilog; // 1. 引入 Serilog

namespace EquipmentMonitor.Infrastructure
{
    public class RealtimeEngine : IRealtimeEngine, IAsyncDisposable
    {
        // 2. 字段类型改为 Serilog.ILogger
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, IRealtimeTask> _tasks = new();
        private CancellationTokenSource? _cts;
        private Task? _engineTask;

        public string Name { get; }
        public TimeSpan Interval { get; }

        public bool IsRunning { get; private set; }

        // 3. 构造函数注入 Serilog.ILogger
        public RealtimeEngine(string name, TimeSpan interval)
        {
            Name = name;
            Interval = interval;
            // 使用 ForContext 为当前类打上专属标签（相当于原来的 ILogger<T>）
            _logger = Log.ForContext("EngineName", name);
        }

        public void RegisterTask(IRealtimeTask task)
        {
            ArgumentNullException.ThrowIfNull(task);

            if (IsRunning)
                throw new InvalidOperationException($"引擎 {Name} 正在运行，无法动态注册任务：{task.Name}");

            if (_tasks.TryAdd(task.Name, task))
            {
                // 4. 方法名变化：LogInformation -> Information
                _logger.Information("任务 {TaskName} 已注册到实时引擎。", task.Name);
            }
        }

        public void Start()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            IsRunning = true;

            _engineTask = Task.Run(() => RunLoopAsync(_cts.Token));
            _logger.Information("引擎已启动，周期: {Interval}ms", Interval.TotalMilliseconds);
        }

        public async Task StopAsync()
        {
            if (!IsRunning || _cts == null) return;

            _logger.Information("正在停止引擎...");
            _cts.Cancel();

            if (_engineTask != null)
            {
                try { await _engineTask; }
                catch (OperationCanceledException) { /* 正常的取消退出 */ }
            }

            IsRunning = false;
            _cts.Dispose();
            _cts = null;
            _engineTask = null;

            _logger.Information("引擎已停止。");
        }

        private async Task RunLoopAsync(CancellationToken token)
        {
            using var timer = new PeriodicTimer(Interval);

            try
            {
                // PeriodicTimer 保证不会因为单次执行时间过长而发生并发重入
                while (await timer.WaitForNextTickAsync(token))
                {
                    if (token.IsCancellationRequested) break;

                    // 遍历并并发执行所有注册的任务
                    var executeTasks = _tasks.Values.Select(task => ExecuteTaskSafelyAsync(task, token));

                    try
                    {
                        await Task.WhenAll(executeTasks);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "引擎循环发生未捕获异常");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 捕获因 StopAsync 触发的取消异常，这是正常的退出流程
            }
        }

        private async Task ExecuteTaskSafelyAsync(IRealtimeTask task, CancellationToken token)
        {
            try
            {
                // 隔离执行，防止某一个任务崩溃导致整个引擎宕机
                await task.ExecuteAsync(token);
            }
            catch (OperationCanceledException) { /* 忽略取消 */ }
            catch (Exception ex)
            {
                _logger.Error(ex, "实时任务 [{TaskName}] 执行失败。", task.Name);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }
    }
}