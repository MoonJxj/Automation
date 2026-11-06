using System.Collections.Concurrent;
using System.IO.Ports;
using Common;
using EquipmentMonitor.Infrastructure;

namespace EquipmentMonitor.Infrastructure.Hardware.PLC.Modbus;

/// <summary>
/// Modbus 协议实例池：同一个 PLC（同一连接端点 + 从站号）只创建一个 <see cref="ModbusProtocol"/>。
/// </summary>
/// <remarks>
/// <para>
/// 连接是 PLC 级资源，不应该每个轴/每个控制器配置各建一条连接。池按连接参数做键，
/// 多个 <see cref="ModbusRegisterController"/> 共享同一个协议实例。
/// </para>
/// <para>
/// <b>生命周期</b>：池实现 <see cref="IDisposable"/>，由宿主（DI 容器）负责释放；
/// 释放时关闭所有协议连接。
/// </para>
/// <para>
/// <b>一个 PLC 一个从站号</b>：当前约定下从站号进入池键；如果以后同一 PLC 多从站，
/// 需要把从站号从 <see cref="ModbusProtocol"/> 实例属性改为每次读写参数。
/// </para>
/// </remarks>
public sealed class ModbusProtocolPool : IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<ModbusProtocol>> _protocols = new();
    private readonly ILoggerFactory _loggerFactory;
    private bool _disposed;

    /// <summary>创建协议池。</summary>
    /// <param name="loggerFactory">协议日志工厂；为 <c>null</c> 时使用最小 Serilog 工厂。</param>
    public ModbusProtocolPool(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory ?? new LoggerFactory(name => new SerilogManager(name));
    }

    /// <summary>
    /// 获取或创建该控制器配置对应的协议实例。
    /// </summary>
    public ModbusProtocol GetOrCreate(ControllerConfig controllerConfig)
    {
        ArgumentNullException.ThrowIfNull(controllerConfig);

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ModbusProtocolPool));
        }

        var key = BuildKey(controllerConfig);
        var lazy = _protocols.GetOrAdd(
            key,
            _ => new Lazy<ModbusProtocol>(
                () => CreateProtocol(controllerConfig),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value;
    }

    /// <summary>关闭并清空所有协议实例。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var lazy in _protocols.Values)
        {
            if (lazy.IsValueCreated)
            {
                lazy.Value.Dispose();
            }
        }

        _protocols.Clear();
    }

    private ModbusProtocol CreateProtocol(ControllerConfig controllerConfig)
    {
        var isTcp = controllerConfig.ConnectionType == ControllerConnectionType.ModbusTcp;

        if (isTcp && string.IsNullOrWhiteSpace(controllerConfig.IpAddress))
        {
            throw new InvalidOperationException(
                $"Modbus TCP 控制器 {controllerConfig.Name ?? controllerConfig.Id.ToString()} 缺少 IpAddress。");
        }

        if (controllerConfig.SlaveAddress is < 0 or > 255)
        {
            throw new InvalidOperationException(
                $"控制器 {controllerConfig.Name ?? controllerConfig.Id.ToString()} 的 SlaveAddress 必须在 0–255。");
        }

        if (controllerConfig.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                $"控制器 {controllerConfig.Name ?? controllerConfig.Id.ToString()} 的 Port 必须在 1–65535。");
        }

        return new ModbusProtocol(_loggerFactory)
        {
            IsModbusTCP = isTcp,
            IP = controllerConfig.IpAddress ?? "127.0.0.1",
            Port = controllerConfig.Port,
            PortName = controllerConfig.PortName ?? "COM1",
            BaudRate = controllerConfig.BaudRate,
            Parity = (Parity)controllerConfig.Parity,
            DataBits = controllerConfig.DataBits,
            StopBits = (StopBits)controllerConfig.StopBits,
            SlaveAddress = (byte)controllerConfig.SlaveAddress
        };
    }

    private static string BuildKey(ControllerConfig controllerConfig)
    {
        if (controllerConfig.ConnectionType == ControllerConnectionType.ModbusTcp)
        {
            return $"tcp://{controllerConfig.IpAddress}:{controllerConfig.Port}#{controllerConfig.SlaveAddress}";
        }

        return $"rtu://{controllerConfig.PortName}:{controllerConfig.BaudRate}:" +
               $"{(int)controllerConfig.Parity}:{controllerConfig.DataBits}:" +
               $"{(int)controllerConfig.StopBits}#{controllerConfig.SlaveAddress}";
    }
}
