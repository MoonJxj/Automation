using EquipmentMonitor.Infrastructure;
using EquipmentMonitor.Infrastructure.Axis;
using EquipmentMonitor.Infrastructure.Hardware.Simulation;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

public class AxisFrameworkOptionsTests
{
    [Fact]
    public void Load_WhenJsonValid_ReadsControllersAndAxes()
    {
        var path = WriteTempJson(
            """
            {
              "controllers": [
                {
                  "id": 1,
                  "name": "模拟卡",
                  "brand": "Sim",
                  "controllerNo": 0,
                  "connectionType": "PciCard",
                  "isEnabled": true
                }
              ],
              "axes": [
                {
                  "id": 10,
                  "controllerConfigId": 1,
                  "axisNo": 0,
                  "name": "X 轴",
                  "stateStartAddress": 100,
                  "commandStartAddress": 200,
                  "executeCoilAddress": 10,
                  "positionScale": 0.001,
                  "defaultVelocity": 10.0,
                  "isEnabled": true
                }
              ]
            }
            """);

        try
        {
            var result = EquipmentMonitor.Infrastructure.Configuration.AxisFrameworkOptionsLoader.Load(path);

            result.IsSuccess.Should().BeTrue();
            result.Value.Controllers.Should().HaveCount(1);
            result.Value.Controllers[0].Brand.Should().Be("Sim");
            result.Value.Controllers[0].ConnectionType.Should().Be(ControllerConnectionType.PciCard);
            result.Value.Axes.Should().HaveCount(1);
            result.Value.Axes[0].StateStartAddress.Should().Be(100);
            result.Value.Axes[0].CommandStartAddress.Should().Be(200);
            result.Value.Axes[0].ExecuteCoilAddress.Should().Be(10);
            result.Value.Axes[0].PositionScale.Should().Be(0.001);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsFailure()
    {
        var result = EquipmentMonitor.Infrastructure.Configuration.AxisFrameworkOptionsLoader.Load(
            Path.Combine(Path.GetTempPath(), $"axis-missing-{Guid.NewGuid():N}.json"));

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("AxisFramework.ConfigFileNotFound");
    }

    [Fact]
    public async Task Bootstrapper_WithSimConfig_BuildsInitializedAxis()
    {
        var path = WriteTempJson(
            """
            {
              "controllers": [
                { "id": 1, "name": "模拟卡", "brand": "Sim", "controllerNo": 0, "connectionType": "PciCard" }
              ],
              "axes": [
                { "id": 10, "controllerConfigId": 1, "axisNo": 0, "name": "X 轴" }
              ]
            }
            """);

        try
        {
            var loader = new AxisFrameworkLoader(new FakeControllerFactory(), TestServices.AllowPermissions(), TestServices.RecordingAuditService());
            var bootstrapper = new AxisFrameworkBootstrapper(loader);

            var result = await bootstrapper.InitializeAsync(path);

            result.IsSuccess.Should().BeTrue(result.Error);
            result.Value.Elements.Should().HaveCount(1);
            result.Value.Elements[0].IsInitialized.Should().BeTrue();
            result.Value.Elements[0].Dispose();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_WithRelativePath_ResolvesAgainstBaseDirectory()
    {
        var fileName = $"axis-relative-{Guid.NewGuid():N}.json";
        var fullPath = Path.Combine(AppContext.BaseDirectory, fileName);
        File.WriteAllText(fullPath, """{ "controllers": [], "axes": [] }""");

        try
        {
            var result = EquipmentMonitor.Infrastructure.Configuration.AxisFrameworkOptionsLoader.Load(fileName);

            result.IsSuccess.Should().BeTrue(result.Error);
            result.Value.Controllers.Should().BeEmpty();
        }
        finally
        {
            File.Delete(fullPath);
        }
    }

    [Fact]
    public async Task Bootstrapper_WithDefaultConfigFile_BuildsSimAxis()
    {
        var loader = new AxisFrameworkLoader(new FakeControllerFactory(), TestServices.AllowPermissions(), TestServices.RecordingAuditService());
        var bootstrapper = new AxisFrameworkBootstrapper(loader);

        var result = await bootstrapper.InitializeAsync();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Elements.Should().HaveCount(2, "骨架配置包含两个工位各一根模拟轴");
        result.Value.Cylinders.Should().HaveCount(2, "骨架配置包含两个工位各一个模拟气缸");
        result.Value.Elements.Should().OnlyContain(element => element.IsInitialized);

        foreach (var element in result.Value.Elements)
        {
            element.Dispose();
        }

        foreach (var cylinder in result.Value.Cylinders)
        {
            cylinder.Dispose();
        }
    }

    private static string WriteTempJson(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"axis-options-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    private sealed class FakeControllerFactory : IControllerFactory
    {
        public T Create<T>(string brand) where T : class, IControllerBase
        {
            if (typeof(T) == typeof(IAxisController))
            {
                return (T)(object)new SimulatedAxisController(axisCount: 8);
            }

            if (typeof(T) == typeof(IIoController))
            {
                return (T)(object)new SimulatedIoController();
            }

            throw new NotSupportedException($"测试工厂不支持类型 {typeof(T).Name}。");
        }
    }
}
