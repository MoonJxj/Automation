using System.Runtime.CompilerServices;
using System.Text.Json;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Desktop.Controls;
using EquipmentMonitor.Desktop.Forms;
using EquipmentMonitor.Desktop.Monitoring;
using EquipmentMonitor.Desktop.Operation;
using EquipmentMonitor.Infrastructure.Alarms.Sources;
using EquipmentMonitor.Infrastructure.Axis;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 宿主装配与启动链路的测试：走真实的 <see cref="Program.BuildHost"/>。
/// </summary>
public sealed class DesktopHostTests
{
    private const string SimConfig = """
        {
          "controllers": [
            {
              "id": 1,
              "name": "模拟调试卡",
              "brand": "Sim",
              "controllerNo": 0,
              "isEnabled": true
            }
          ],
          "axes": [
            {
              "id": 10,
              "controllerConfigId": 1,
              "axisNo": 0,
              "name": "X 轴（模拟）",
              "defaultVelocity": 10.0,
              "isEnabled": true
            }
          ]
        }
        """;

    [Fact]
    public void BuildHost_ResolvesFrameworkAndDesktopServices()
    {
        using var host = Program.BuildHost([]);

        host.Services.GetRequiredService<AxisSnapshotStore>().Should().NotBeNull();
        host.Services.GetRequiredService<AxisMonitorService>().Should().NotBeNull();
        host.Services.GetRequiredService<AxisFrameworkBootstrapper>().Should().NotBeNull();
        host.Services.GetRequiredService<IAlarmService>().Should().NotBeNull();
        host.Services.GetRequiredService<AlarmMonitorService>().Should().NotBeNull();
        host.Services.GetRequiredService<MachineRunService>().Should().NotBeNull();
    }

    [Fact]
    public void BuildHost_WithDemoAlarms_RegistersDemoSource()
    {
        using var host = Program.BuildHost(["--demo-alarms"]);

        host.Services.GetServices<IAlarmSource>()
            .Should().ContainSingle(source => source is DemoAlarmSource);
    }

    [Fact]
    public void BuildHost_WithoutDemoAlarms_DoesNotRegisterDemoSource()
    {
        using var host = Program.BuildHost([]);

        host.Services.GetServices<IAlarmSource>()
            .Should().NotContain(source => source is DemoAlarmSource);
    }
    [Fact]
    public void LaunchSettings_DemoAlarmsProfile_PassesDemoFlag()
    {
        var path = Path.Combine(
            RepoRoot(),
            "src",
            "EquipmentMonitor.Desktop",
            "Properties",
            "launchSettings.json");

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        document.RootElement
            .GetProperty("profiles")
            .GetProperty("DemoAlarms")
            .GetProperty("commandLineArgs")
            .GetString()
            .Should()
            .Contain("--demo-alarms");
    }

    [Fact]
    public async Task Bootstrapper_LoadsSimulatedAxis_AndMonitorPublishesSnapshot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"axis-framework-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, SimConfig);

        using var host = Program.BuildHost([]);
        var initialized = await host.Services
            .GetRequiredService<AxisFrameworkBootstrapper>()
            .InitializeAsync(path);

        initialized.IsSuccess.Should().BeTrue(initialized.Error);
        initialized.Value.Elements.Should().HaveCount(1);

        try
        {
            var store = host.Services.GetRequiredService<AxisSnapshotStore>();
            store.Attach(initialized.Value.Elements);

            var snapshot = await host.Services
                .GetRequiredService<AxisMonitorService>()
                .RefreshOnceAsync(CancellationToken.None);

            snapshot.Axes.Should().HaveCount(1);
            snapshot.Axes[0].ElementId.Should().NotBeNullOrWhiteSpace();
            snapshot.LastError.Should().BeNull();
            store.Current.Axes.Should().HaveCount(1);
        }
        finally
        {
            foreach (var element in initialized.Value.Elements)
            {
                element.Dispose();
            }

            File.Delete(path);
        }
    }

    [Fact]
    public void MainForm_AndOverviewPage_ResolveFromContainer()
    {
        using var host = Program.BuildHost([]);

        StaTestHost.RunSta(() =>
        {
            using var form = (Form)host.Services.GetRequiredService(typeof(MainForm));
            form.Text.Should().NotBeNullOrWhiteSpace();

            using var page = (Form)host.Services.GetRequiredService(typeof(ElementOverviewPage));
            page.Should().BeOfType<ElementOverviewPage>();

            using var operation = (Control)host.Services.GetRequiredService(typeof(AxisOperationPanel));
            operation.Should().BeOfType<AxisOperationPanel>();

            using var dialog = (Form)host.Services.GetRequiredService(typeof(ElementOperationPage));
            dialog.Should().BeOfType<ElementOperationPage>();

            using var cylinderPage = (Form)host.Services.GetRequiredService(typeof(CylinderOverviewPage));
            cylinderPage.Should().BeOfType<CylinderOverviewPage>();

            using var cylinderDialog = (Form)host.Services.GetRequiredService(typeof(CylinderOperationPage));
            cylinderDialog.Should().BeOfType<CylinderOperationPage>();

            using var cylinderPanel = (Control)host.Services.GetRequiredService(typeof(CylinderOperationPanel));
            cylinderPanel.Should().BeOfType<CylinderOperationPanel>();

            using var autoMonitor = (Form)host.Services.GetRequiredService(typeof(AutoMonitorPage));
            autoMonitor.Should().BeOfType<AutoMonitorPage>();

            using var manual = (Form)host.Services.GetRequiredService(typeof(ManualOperationPage));
            manual.Should().BeOfType<ManualOperationPage>();

            using var function = (Form)host.Services.GetRequiredService(typeof(FunctionSelectionPage));
            function.Should().BeOfType<FunctionSelectionPage>();

            using var parameters = (Form)host.Services.GetRequiredService(typeof(ParameterSettingsPage));
            parameters.Should().BeOfType<ParameterSettingsPage>();

            using var alarms = (Form)host.Services.GetRequiredService(typeof(AlarmInfoPage));
            alarms.Should().BeOfType<AlarmInfoPage>();

            using var users = (Form)host.Services.GetRequiredService(typeof(UserManagementPage));
            users.Should().BeOfType<UserManagementPage>();

            using var signIn = (Form)host.Services.GetRequiredService(typeof(SignInForm));
            signIn.Should().BeOfType<SignInForm>();
        });
    }

    private static string RepoRoot([CallerFilePath] string path = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));
}
