using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Infrastructure.Configuration;

/// <summary>
/// 报警服务配置；通过 <c>IOptions&lt;AlarmOptions&gt;</c> 注入，业务代码不直接读 <c>IConfiguration</c>。
/// </summary>
/// <remarks>
/// <para>
/// 配置模型跟随使用它的模块（本模块），宿主负责绑定；文件不存在时使用默认值，
/// 配置错误不能阻止设备启动（报警是辅助功能，不是启动前置条件）。
/// </para>
/// <para>
/// P0 只使用默认值；P1 由 <c>AlarmOptionsLoader</c> 从 <c>alarms.json</c> 读取后绑定。
/// </para>
/// </remarks>
public sealed class AlarmOptions
{
    /// <summary>采集周期（毫秒），默认 500；小于 100 时按 100 处理。</summary>
    public int PollIntervalMs { get; set; } = 500;

    /// <summary>抖动窗口（秒）：恢复后在此窗口内再次发生，复用原记录。</summary>
    public int ChatterWindowSeconds { get; set; } = 5;

    /// <summary>恢复后是否保留「未确认」记录，直到人工确认。</summary>
    public bool RetainUnacknowledgedOnRecovery { get; set; } = true;

    /// <summary>活动报警上限；超出时丢弃新报警并记 Warning，不丢已有报警。</summary>
    public int MaxActiveAlarms { get; set; } = 200;

    /// <summary>JSONL 历史保留天数。</summary>
    public int HistoryRetentionDays { get; set; } = 90;

    /// <summary>历史目录；相对路径按程序运行目录解析。</summary>
    public string HistoryDirectory { get; set; } = "logs/alarms";

    /// <summary>报警码定义（覆盖内置码表）。</summary>
    public List<AlarmDefinition> Definitions { get; set; } = new();

    /// <summary>PLC 报警字映射（P1）。</summary>
    public List<PlcAlarmWordOptions> PlcAlarmWords { get; set; } = new();
}

/// <summary>PLC 报警字配置：一段保持寄存器 + 位映射。</summary>
public sealed class PlcAlarmWordOptions
{
    /// <summary>控制器配置 Id。</summary>
    public int ControllerId { get; set; }

    /// <summary>起始寄存器地址。</summary>
    public ushort Address { get; set; }

    /// <summary>读取的寄存器数量。</summary>
    public ushort Count { get; set; } = 1;

    /// <summary>位映射。</summary>
    public List<PlcAlarmBitOptions> Bits { get; set; } = new();
}

/// <summary>单个 PLC 报警位的映射。</summary>
public sealed class PlcAlarmBitOptions
{
    /// <summary>位号（0 起）；大于 <c>Count * 16 - 1</c> 视为配置错误。</summary>
    public int Bit { get; set; }

    /// <summary>报警码，必须能在码表中找到。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>等级；码表定义优先。</summary>
    public AlarmLevel Level { get; set; } = AlarmLevel.Error;

    /// <summary>可选消息覆盖。</summary>
    public string? Message { get; set; }
}
