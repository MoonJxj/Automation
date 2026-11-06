using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Security;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>
/// 五角色权限表：单调性、穷尽性与分权边界。
/// </summary>
/// <remarks>
/// 这张表就是本阶段的安全模型。测试直接断言“谁能做什么”，
/// 修改角色或权限点时这些用例会失败 —— 那正是提醒把改动同步到文档与界面。
/// </remarks>
public sealed class RolePermissionPolicyTests
{
    private readonly RolePermissionPolicy _policy = new();

    [Fact]
    public void 五个角色_权限随角色单调递增()
    {
        var roles = UserRoleCatalog.All;

        roles.Should().HaveCount(5);

        for (var index = 1; index < roles.Count; index++)
        {
            var lower = _policy.GetGrantedPermissions(roles[index - 1]);
            var higher = _policy.GetGrantedPermissions(roles[index]);

            higher.Should().Contain(
                lower,
                $"角色 {roles[index]} 应包含 {roles[index - 1]} 的全部权限");
            higher.Should().HaveCountGreaterThan(lower.Count, $"角色 {roles[index]} 必须比上一级多出至少一项权限");
        }
    }

    [Fact]
    public void 管理员_拥有全部权限点()
    {
        var all = Enum.GetValues<Permission>();

        _policy.GetGrantedPermissions(UserRole.Admin).Should().BeEquivalentTo(all);
    }

    [Fact]
    public void 观察员_只能查看()
    {
        _policy.GetGrantedPermissions(UserRole.Viewer)
            .Should().BeEquivalentTo([Permission.MachineView, Permission.LogView]);
    }

    [Fact]
    public void 工艺工程师_可改工艺_不可改设备配置()
    {
        _policy.IsGranted(UserRole.ProcessEngineer, Permission.ProcessConfig).Should().BeTrue();
        _policy.IsGranted(UserRole.ProcessEngineer, Permission.MachineOperate).Should().BeTrue();
        _policy.IsGranted(UserRole.ProcessEngineer, Permission.MachineConfig).Should().BeFalse();
    }

    [Fact]
    public void 设备工程师_可改设备配置_不可管理用户()
    {
        _policy.IsGranted(UserRole.Engineer, Permission.MachineConfig).Should().BeTrue();
        _policy.IsGranted(UserRole.Engineer, Permission.ProcessConfig).Should().BeTrue();
        _policy.IsGranted(UserRole.Engineer, Permission.UserManage).Should().BeFalse();
    }

    [Fact]
    public void 未知角色_全部拒绝()
    {
        const UserRole undefined = (UserRole)99;

        _policy.GetGrantedPermissions(undefined).Should().BeEmpty();
        _policy.IsGranted(undefined, Permission.MachineView).Should().BeFalse();
    }
}
