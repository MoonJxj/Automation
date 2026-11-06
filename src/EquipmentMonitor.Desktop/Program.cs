using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Desktop.Controls;
using EquipmentMonitor.Desktop.Forms;
using EquipmentMonitor.Desktop.Monitoring;
using EquipmentMonitor.Desktop.Operation;
using EquipmentMonitor.Desktop.Parameter;
using EquipmentMonitor.Infrastructure;
using EquipmentMonitor.Infrastructure.Alarms.Sources;
using EquipmentMonitor.Infrastructure.Axis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace EquipmentMonitor.Desktop;

/// <summary>
/// 桌面宿主入口：装配容器 → 登录（建立会话）→ 初始化轴控框架 → 启动后台刷新 → 打开主窗体。
/// </summary>
/// <remarks>
/// <para><b>为什么装配路径是 <c>public</c></b>：集成测试要跑真实路径
/// （<see cref="BuildHost"/>），而不是把注册代码抄一份到测试里 ——
/// 抄的那份永远是对的，于是既发现不了漏注册，也发现不了启动顺序问题。</para>
/// <para><b>同步阻塞只在引导期</b>：<c>InitializeAsync(...).GetAwaiter().GetResult()</c>
/// 只出现在本文件，且发生在 <see cref="Application.Run(Form)"/> 之前；
/// 界面期一律异步（见 <see cref="AxisMonitorService"/>）。</para>
/// <para><b>返回码</b>：0 正常退出；1 启动异常；2 轴控装配失败；3 用户取消登录。</para>
/// </remarks>
public static class Program
{
    /// <summary>按真实启动路径装配容器。</summary>
    /// <param name="args">命令行参数。</param>
    /// <param name="configure">装配前的调整钩子，仅测试使用。</param>
    public static IHost BuildHost(
        string[] args,
        Action<HostApplicationBuilder>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            // 必须显式指定：默认是当前工作目录，从快捷方式启动时可能是任意位置。
            ContentRootPath = AppContext.BaseDirectory,
            Args = args
        });

        configure?.Invoke(builder);

        ConfigureLogging(builder);
        ConfigureServices(
            builder.Services,
            demoAlarms: args.Contains("--demo-alarms", StringComparer.OrdinalIgnoreCase));

        return builder.Build();
    }

    /// <summary>注册宿主侧服务（监控、窗体）与轴控框架。</summary>
    /// <param name="services">服务集合。</param>
    /// <param name="demoAlarms">
    /// 是否注册演示报警源。只应在界面验收/演示时通过 <c>--demo-alarms</c> 开启；
    /// 默认关闭，避免现场启动时凭空出现报警。
    /// </param>
    public static void ConfigureServices(IServiceCollection services, bool demoAlarms = false)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 快照仓与采集服务：UI 只读快照，后台线程独占元件。
        services.AddSingleton<AxisSnapshotStore>();
        services.AddSingleton<AxisMonitorService>();
        services.AddHostedService(sp => sp.GetRequiredService<AxisMonitorService>());

        // 设备级启停：把“启动/停止/复位”翻译成元件命令；页面只通过它下发。
        services.AddSingleton<MachineRunService>();

        services.AddSingleton<CylinderSnapshotStore>();
        services.AddSingleton<CylinderMonitorService>();
        services.AddHostedService(sp => sp.GetRequiredService<CylinderMonitorService>());

        // 报警采集：唯一入口 IAlarmService 的后台轮询；与轴采集互不影响。
        services.AddSingleton<AlarmMonitorService>();
        services.AddHostedService(sp => sp.GetRequiredService<AlarmMonitorService>());

        if (demoAlarms)
        {
            // 仅演示/验收：固定三条报警，让页面在没有真实故障时可展示完整链路。
            services.AddSingleton<IAlarmSource, DemoAlarmSource>();
        }

        // 窗体/控件：主壳单例（导航状态要保留），页面与操作面板按需创建。
        services.AddSingleton<MainForm>();
        services.AddTransient<SignInForm>();
        services.AddTransient<ElementOverviewPage>();
        services.AddTransient<AxisOperationPanel>();
        services.AddTransient<ElementOperationPage>();
        services.AddTransient<CylinderOverviewPage>();
        services.AddTransient<CylinderOperationPanel>();
        services.AddTransient<CylinderOperationPage>();
        services.AddTransient<AutoMonitorPage>();
        services.AddTransient<ManualOperationPage>();
        services.AddTransient<FunctionSelectionPage>();
        services.AddTransient<ParameterSettingsPage>();

        // 参数页模板：当前用内存示例源；接入真实参数服务后替换这一行。
        services.AddSingleton<IParameterPageSource, TemplateParameterPageSource>();
        services.AddTransient<AlarmInfoPage>();
        services.AddTransient<UserManagementPage>();

        // 轴控框架：控制器工厂、Modbus 能力、装配器、启动器。
        services.RegisterInfrastructure();
    }

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        RegisterGlobalExceptionHandlers();

        IHost? host = null;
        AxisFrameworkBuildResult? buildResult = null;

        try
        {
            host = BuildHost(args);

            // 先登录再碰硬件：取消登录不应因为初始化了轴卡而留下副作用。
            using (var signIn = host.Services.GetRequiredService<SignInForm>())
            {
                if (signIn.ShowDialog() != DialogResult.OK)
                {
                    Log.Information("用户在登录界面取消，程序退出。");
                    return 3;
                }
            }

            var bootstrapper = host.Services.GetRequiredService<AxisFrameworkBootstrapper>();
            var initialized = bootstrapper.InitializeAsync().GetAwaiter().GetResult();

            if (initialized.IsFailure)
            {
                Log.Fatal("轴控装配失败：{Error}", initialized.Error);

                MessageBox.Show(
                    $"轴控框架装配失败，程序无法启动。{Environment.NewLine}{Environment.NewLine}{initialized.Error}",
                    "启动失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return 2;
            }

            buildResult = initialized.Value;

            // 先挂载元件，再启动宿主：后台采集的第一次循环就能看到轴与气缸。
            host.Services.GetRequiredService<AxisSnapshotStore>().Attach(buildResult.Elements);
            host.Services.GetRequiredService<CylinderSnapshotStore>().Attach(buildResult.Cylinders);
            host.Start();

            Application.Run(host.Services.GetRequiredService<MainForm>());
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "桌面宿主启动失败。");
            MessageBox.Show(
                $"程序启动失败：{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "启动失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
        finally
        {
            Shutdown(host, buildResult);
        }
    }

    private static void ConfigureLogging(HostApplicationBuilder builder)
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDirectory, "desktop-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        builder.Services.AddSerilog(Log.Logger, dispose: false);
    }

    /// <summary>优雅退出：先停后台采集，再登出（写审计），释放轴元件，最后冲刷日志。</summary>
    private static void Shutdown(IHost? host, AxisFrameworkBuildResult? buildResult)
    {
        try
        {
            host?.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "停止宿主时发生异常。");
        }

        try
        {
            // 退出也留一条登出记录：审计里“谁最后在线”不依赖进程退出时间。
            host?.Services.GetRequiredService<IAuthenticationService>()
                .SignOutAsync("退出程序")
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "退出时写登出审计失败。");
        }

        if (buildResult is not null)
        {
            foreach (var element in buildResult.Elements)
            {
                element.Dispose();
            }

            foreach (var cylinder in buildResult.Cylinders)
            {
                cylinder.Dispose();
            }
        }

        host?.Dispose();

        Log.Information("桌面宿主已退出。");
        Log.CloseAndFlush();
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Application.ThreadException += (_, e) =>
        {
            Log.Fatal(e.Exception, "UI 线程发生未处理异常。");
            MessageBox.Show(
                $"界面发生错误：{Environment.NewLine}{Environment.NewLine}{e.Exception.Message}",
                "错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "后台线程发生未处理异常。IsTerminating={IsTerminating}", e.IsTerminating);
            }

            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "未观察到的 Task 异常。");
            e.SetObserved();
        };
    }
}
