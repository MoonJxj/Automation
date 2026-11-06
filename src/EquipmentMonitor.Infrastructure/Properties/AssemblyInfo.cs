using System.Runtime.CompilerServices;

// Modbus 异步端口与同步桥是 Infrastructure 内部实现；测试需要直接验证它们，
// 因此只对测试程序集开放，不进入公共 API。
[assembly: InternalsVisibleTo("EquipmentMonitor.AxisFramework.Tests")]
