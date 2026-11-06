using EquipmentMonitor.Infrastructure;
using EquipmentMonitor.Infrastructure.Axis;
using EquipmentMonitor.Infrastructure.Hardware.Simulation;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

public class AxisFrameworkLoaderTests
{
    [Fact]
    public async Task BuildAsync_WhenConfigValid_CreatesInitializedAxes()
    {
        var loader = new AxisFrameworkLoader(new FakeControllerFactory(), TestServices.AllowPermissions(), TestServices.RecordingAuditService());

        var result = await loader.BuildAsync(
            [
                new ControllerConfig
                {
                    Id = 1,
                    Name = "模拟卡",
                    Brand = "Sim",
                    ControllerNo = 0,
                    IsEnabled = true
                }
            ],
            [
                new AxisConfig
                {
                    Id = 10,
                    ControllerConfigId = 1,
                    AxisNo = 0,
                    Name = "X 轴",
                    DefaultVelocity = 10.0
                },
                new AxisConfig
                {
                    Id = 11,
                    ControllerConfigId = 1,
                    AxisNo = 1,
                    Name = "Y 轴",
                    DefaultVelocity = 20.0
                }
            ]);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Controllers.Should().ContainKey(1);
        result.Value.Elements.Should().HaveCount(2);
        result.Value.Elements.Should().OnlyContain(e => e.IsInitialized);
        result.Value.Elements[0].ElementMetadata.ElementId.Should().Be("Axis-0-0");
        result.Value.Elements[1].ElementMetadata.ElementId.Should().Be("Axis-0-1");

        foreach (var element in result.Value.Elements)
        {
            element.Dispose();
        }
    }

    [Fact]
    public async Task BuildAsync_WhenAxisPointsToUnknownController_Fails()
    {
        var loader = new AxisFrameworkLoader(new FakeControllerFactory(), TestServices.AllowPermissions(), TestServices.RecordingAuditService());

        var result = await loader.BuildAsync(
            [],
            [
                new AxisConfig
                {
                    Id = 10,
                    ControllerConfigId = 99,
                    AxisNo = 0,
                    Name = "孤立轴"
                }
            ]);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("AxisFramework.BuildFailed");
        result.Error.Should().Contain("不存在或已禁用");
    }

    [Fact]
    public async Task BuildAsync_WhenControllerDisabled_DoesNotCreateItsAxes()
    {
        var loader = new AxisFrameworkLoader(new FakeControllerFactory(), TestServices.AllowPermissions(), TestServices.RecordingAuditService());

        var result = await loader.BuildAsync(
            [
                new ControllerConfig
                {
                    Id = 1,
                    Name = "禁用卡",
                    Brand = "Sim",
                    ControllerNo = 0,
                    IsEnabled = false
                }
            ],
            [
                new AxisConfig
                {
                    Id = 10,
                    ControllerConfigId = 1,
                    AxisNo = 0,
                    Name = "X 轴"
                }
            ]);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("不存在或已禁用");
    }

    [Fact]
    public async Task BuildAsync_WhenAxisConfigInvalid_ReportsError()
    {
        var loader = new AxisFrameworkLoader(new FakeControllerFactory(), TestServices.AllowPermissions(), TestServices.RecordingAuditService());

        var result = await loader.BuildAsync(
            [
                new ControllerConfig
                {
                    Id = 1,
                    Name = "模拟卡",
                    Brand = "Sim",
                    ControllerNo = 0,
                    IsEnabled = true
                }
            ],
            [
                new AxisConfig
                {
                    Id = 10,
                    ControllerConfigId = 1,
                    AxisNo = 0,
                    Name = "X 轴",
                    DefaultVelocity = 0.0
                }
            ]);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("非法");
    }

    private sealed class FakeControllerFactory : IControllerFactory
    {
        public T Create<T>(string brand) where T : class, IControllerBase
        {
            if (typeof(T) == typeof(IAxisController))
            {
                return (T)(object)new SimulatedAxisController(axisCount: 2);
            }

            throw new NotSupportedException($"测试工厂不支持类型 {typeof(T).Name}。");
        }
    }
}
