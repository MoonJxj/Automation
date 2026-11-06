using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Forms;
using EquipmentMonitor.Desktop.FunctionSelection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 功能选择页：目录覆盖六个规划功能；无权限只提示不打开；待接入入口不打开空窗口。
/// </summary>
public sealed class FunctionSelectionPageTests
{
    [Fact]
    public void 容器能解析功能选择页并生成卡片()
    {
        using var host = Program.BuildHost([]);

        StaTestHost.RunSta(() =>
        {
            using var page = (FunctionSelectionPage)host.Services.GetRequiredService(
                typeof(FunctionSelectionPage));

            page.CardCount.Should().Be(FunctionCatalog.All.Count);
        });
    }

    [Fact]
    public void 目录覆盖六个规划功能_标识唯一且文案齐全()
    {
        FunctionCatalog.All.Should().HaveCount(6);
        FunctionCatalog.All.Select(entry => entry.Key).Should().OnlyHaveUniqueItems();
        FunctionCatalog.All.Should().OnlyContain(
            entry => !string.IsNullOrWhiteSpace(entry.Title) && !string.IsNullOrWhiteSpace(entry.Summary));

        FunctionCatalog.All.Should().Contain(entry => entry.Key == FunctionCatalog.StationBypassKey);
        FunctionCatalog.All.Should().Contain(entry => entry.Key == FunctionCatalog.HelpKey);
    }

    [Fact]
    public void 页面为每个入口生成一张卡片()
    {
        StaTestHost.RunSta(() =>
        {
            using var page = new FunctionSelectionPage(
                DesktopTestServices.AllowPermissions(),
                new UnusedStationBypassService());

            page.CardCount.Should().Be(FunctionCatalog.All.Count);
            page.StatusText.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void 无权限时_已接入入口只提示缺少权限()
    {
        StaTestHost.RunSta(() =>
        {
            using var page = new FunctionSelectionPage(
                DesktopTestServices.DenyPermissions(),
                new UnusedStationBypassService());

            page.InvokeForTest(FunctionCatalog.StationBypassKey);

            page.StatusText.Should().Contain("缺少").And.Contain("修改设备配置");
        });
    }

    [Fact]
    public void 待接入入口_只提示未接入且说明缺什么()
    {
        StaTestHost.RunSta(() =>
        {
            using var page = new FunctionSelectionPage(
                DesktopTestServices.AllowPermissions(),
                new UnusedStationBypassService());

            page.InvokeForTest(FunctionCatalog.RecipeKey);

            page.StatusText.Should().Contain("尚未接入").And.Contain("换型配方");
        });
    }

    private sealed class UnusedStationBypassService : IStationBypassService
    {
        public IReadOnlyList<StationBypassState> GetStates() => [];

        public Task<Result> SetAsync(
            int stationId,
            bool bypassed,
            string reason,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Ok());
    }
}
