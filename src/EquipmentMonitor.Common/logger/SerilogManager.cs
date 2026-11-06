using Serilog;
using Serilog.Core;

namespace Common
{
    public class SerilogManager : ILoggerManager
    {
        private readonly ILogger _logger;

        /// <summary>
        /// 根据传入的名称创建日志。
        /// </summary>
        /// <param name="loggername">日志来源名称，写入 SourceContext 属性。</param>
        public SerilogManager(string loggername)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(loggername);

            // 写入 SourceContext，使 EquipmentMonitor.Logging 的 ModuleEnricher 能
            // 由类名推导模块并分流到各模块日志文件；缺该属性时日志会落进 Other-*.log。
            _logger = Log.ForContext(Constants.SourceContextPropertyName, loggername);
        }

        public void Trace(string msg) => _logger.Verbose(msg);

        public void Debug(string msg) => _logger.Debug(msg);

        public void Info(string msg) => _logger.Information(msg);

        public void Warn(string msg) => _logger.Warning(msg);

        public void Error(string msg) => _logger.Error(msg);

        public void Fatal(string msg) => _logger.Fatal(msg);
    }
}
