using Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Infrastructure.Alarms;
using EquipmentMonitor.Infrastructure.Alarms.Definitions;
using EquipmentMonitor.Infrastructure.Alarms.History;
using EquipmentMonitor.Infrastructure.Alarms.Sources;
using EquipmentMonitor.Infrastructure.Auditing;
using EquipmentMonitor.Infrastructure.Axis;
using EquipmentMonitor.Infrastructure.Configuration;
using EquipmentMonitor.Infrastructure.Hardware;
using EquipmentMonitor.Infrastructure.Hardware.PLC.Modbus;
using EquipmentMonitor.Infrastructure.Hardware.Simulation;
using EquipmentMonitor.Infrastructure.Security;
using EquipmentMonitor.Infrastructure.StationBypass;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Configuration;
using System.Linq;
using System.Reflection;

namespace EquipmentMonitor.Infrastructure
{
    public static class InfrastructureModuleRegister
    {
        public static IServiceCollection RegisterInfrastructure(this IServiceCollection services)
        {
            // 安全与审计：用户目录 → 会话 → 权限表 → 权限服务 / 登录门面。
            // 新增 Windows / 远端认证时，只需再注册一个 IAuthenticationProvider；
            // 用户目录（含口令哈希）只在本模块内可见，界面通过 IUserDirectory 使用。
            services.AddSingleton(new UserDirectoryOptions());
            services.AddSingleton<LocalUserDirectory>();
            services.AddSingleton<IUserDirectory>(sp => sp.GetRequiredService<LocalUserDirectory>());
            services.AddSingleton<ISessionProvider, SessionProvider>();
            services.AddSingleton<IPermissionPolicy, RolePermissionPolicy>();
            services.AddSingleton<IPermissionService, PermissionService>();
            services.AddSingleton<IAuthenticationProvider, LocalAccountProvider>();
            services.AddSingleton<IAuthenticationService, AuthenticationService>();
            services.AddSingleton<IAuditService, LogAuditService>();

            // 工位屏蔽：当前为内存实现（重启恢复），接设备后替换为写 PLC 并回读。
            services.AddSingleton<IStationBypassService, StationBypassService>();

            // 报警：唯一入口 + 设备无关的报警源。
            // 元件注册表由 AxisFrameworkBootstrapper 在装配成功后发布；报警源只读它。
            services.AddOptions<AlarmOptions>();
            services.AddSingleton<TimeProvider>(TimeProvider.System);
            services.AddSingleton<ElementRegistry>();
            services.AddSingleton<IElementRegistry>(sp => sp.GetRequiredService<ElementRegistry>());
            services.AddSingleton<AlarmCatalog>();
            services.AddSingleton<IAlarmCatalog>(sp => sp.GetRequiredService<AlarmCatalog>());
            services.AddSingleton<IAlarmDefinitionProvider, SystemAlarmDefinitions>();
            services.AddSingleton<IAlarmDefinitionProvider, AxisAlarmDefinitions>();
            services.AddSingleton<IAlarmDefinitionProvider, CylinderAlarmDefinitions>();
            services.AddSingleton<IAlarmSource, AxisAlarmSource>();
            services.AddSingleton<IAlarmSource, CylinderAlarmSource>();
            services.AddSingleton<IAlarmHistoryStore, JsonlAlarmHistoryStore>();
            services.AddSingleton<IAlarmService, AlarmService>();

            services.AddKeyedSingleton<IAxisController, GoogolTechAxisController>("GoogolTech");
            services.AddKeyedSingleton<IIoController, GoogolTechIoController>("GoogolTech");

            services.AddKeyedSingleton<IAxisController, LeadShineAxisController>("LeadShine");
            // LeadShineIoController 需要同一品牌的轴控制器；keyed 服务不能用未加键的构造注入解析。
            services.AddKeyedSingleton<IIoController>(
                "LeadShine",
                (provider, _) => new LeadShineIoController(
                    provider.GetRequiredKeyedService<IAxisController>("LeadShine")));

            // Modbus 轴不是无参可构造的：需要 IRegisterController + 每轴寄存器映射。
            // 由 AxisFrameworkLoader/后续工厂按 ControllerConfig/AxisConfig 创建，
            // 再经 AsyncAxisControllerAdapter 接入同步 IAxisController 契约。
            services.AddKeyedSingleton<IIoController, ModbusIoController>("Modbus");

            // 开发/演示用模拟轴与模拟 IO：无板卡时把 ControllerConfig.Brand 配成 "Sim" 即可。
            services.AddKeyedSingleton<IAxisController>(
                "Sim",
                (_, _) => new SimulatedAxisController(axisCount: 8));
            services.AddKeyedSingleton<IIoController>(
                "Sim",
                (_, _) => new SimulatedIoController());

            // 按品牌键解析控制器的统一入口（Create<T>(brand) 走 keyed DI）。
            services.AddSingleton<IControllerFactory, DefaultControllerFactory>();
            services.AddSingleton<global::Common.ILoggerManager>(
                _ => new SerilogManager("AxisFramework"));
            services.AddSingleton<AxisFrameworkLoader>();
            services.AddSingleton<AxisFrameworkBootstrapper>();

            // Modbus 是独立功能：连接按 PLC 复用（ModbusProtocolPool），
            // 寄存器层与轴工厂都消费它，不各自创建协议。
            services.AddSingleton<ModbusProtocolPool>(sp =>
                new ModbusProtocolPool(
                    sp.GetService<ILoggerFactory>()
                    ?? new LoggerFactory(name => new SerilogManager(name))));
            services.AddSingleton<ModbusRegisterControllerFactory>();
            services.AddSingleton<IAxisControllerAdapterFactory>(sp =>
                new ModbusAxisControllerFactory(
                    sp.GetRequiredService<ModbusRegisterControllerFactory>()));

            // 实时引擎由管理器按参数创建，不能直接注册 RealtimeEngine（构造函数需要 name/interval）。
            services.AddSingleton<IRealtimeEngineManager, RealtimeEngineManager>();
            return services;
        }
    }
}