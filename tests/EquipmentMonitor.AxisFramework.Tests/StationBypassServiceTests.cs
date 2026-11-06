using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Security;
using EquipmentMonitor.Infrastructure.StationBypass;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>
/// 工位屏蔽服务：权限、原因校验、状态快照与审计。
/// </summary>
public sealed class StationBypassServiceTests
{
    [Fact]
    public async Task 无MachineConfig权限_抛越权异常且不写审计()
    {
        var audit = TestServices.RecordingAuditService();
        var service = new StationBypassService(TestServices.DenyPermissions(), audit);

        var act = () => service.SetAsync(StationCatalog.Station1, true, "维修");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        service.GetStates().Should().OnlyContain(state => !state.IsBypassed);
        audit.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task 屏蔽未填原因_返回失败且状态不变()
    {
        var service = CreateService(TestServices.RecordingAuditService());

        var result = await service.SetAsync(StationCatalog.Station1, true, "   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("原因");
        service.GetStates()
            .Should().ContainSingle(state => state.StationId == StationCatalog.Station1)
            .Which.IsBypassed.Should().BeFalse();
    }

    [Fact]
    public async Task 屏蔽成功_状态记录原因与操作者并写审计()
    {
        var audit = TestServices.RecordingAuditService();
        var service = CreateService(audit, NewSession("设备工程师"));

        var result = await service.SetAsync(StationCatalog.Station1, true, " 缺料 ");

        result.IsSuccess.Should().BeTrue();

        var state = service.GetStates().Single(state => state.StationId == StationCatalog.Station1);
        state.IsBypassed.Should().BeTrue();
        state.Reason.Should().Be("缺料");
        state.OperatorName.Should().Be("设备工程师");
        state.StationName.Should().Be("工位1");

        audit.Records
            .Should().ContainSingle(record => record.EventType == AuditEventTypes.StationBypassChanged)
            .Which.Detail.Should().Contain("工位1").And.Contain("缺料");
    }

    [Fact]
    public async Task 恢复工位_清除屏蔽并再写一条审计()
    {
        var audit = TestServices.RecordingAuditService();
        var service = CreateService(audit);

        await service.SetAsync(StationCatalog.Station2, true, "单工位调试");
        var result = await service.SetAsync(StationCatalog.Station2, false, string.Empty);

        result.IsSuccess.Should().BeTrue();
        service.GetStates()
            .Should().ContainSingle(state => state.StationId == StationCatalog.Station2)
            .Which.IsBypassed.Should().BeFalse();
        audit.Records.Should().HaveCount(2);
    }

    [Fact]
    public async Task 重复设置相同状态_幂等且不重复写审计()
    {
        var audit = TestServices.RecordingAuditService();
        var service = CreateService(audit);

        await service.SetAsync(StationCatalog.Station1, true, "维修");
        var second = await service.SetAsync(StationCatalog.Station1, true, "维修");

        second.IsSuccess.Should().BeTrue();
        audit.Records.Should().ContainSingle();
    }

    [Fact]
    public async Task 未知工位_返回失败()
    {
        var service = CreateService(TestServices.RecordingAuditService());

        var result = await service.SetAsync(99, true, "维修");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("未知工位");
    }

    [Fact]
    public async Task 审计写入失败_不影响屏蔽结果()
    {
        var service = new StationBypassService(
            TestServices.AllowPermissions(),
            new ThrowingAudit());

        var result = await service.SetAsync(StationCatalog.Station1, true, "维修");

        result.IsSuccess.Should().BeTrue();
        service.GetStates()
            .Should().ContainSingle(state => state.StationId == StationCatalog.Station1)
            .Which.IsBypassed.Should().BeTrue();
    }

    private static StationBypassService CreateService(RecordingAudit audit, UserSession? session = null)
    {
        var sessions = new SessionProvider();

        if (session is not null)
        {
            sessions.Set(session);
        }

        return new StationBypassService(TestServices.AllowPermissions(), audit, sessions);
    }

    private static UserSession NewSession(string displayName) => new()
    {
        UserName = displayName,
        DisplayName = displayName,
        Role = UserRole.Engineer,
        Source = AuthenticationSource.LocalAccount
    };

    private sealed class ThrowingAudit : IAuditService
    {
        public Task RecordAsync(string eventType, string detail, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("审计不可用。");
    }
}
