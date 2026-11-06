using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Events;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Security;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

/// <summary>
/// 会话持有者：登录/登出事件、旧值传递、未登录时的空操作。
/// </summary>
public sealed class SessionProviderTests
{
    [Fact]
    public void Set_触发事件_携带新旧会话()
    {
        var provider = new SessionProvider();
        var first = SessionFor(UserRole.Operator, "张三");
        var second = SessionFor(UserRole.Admin, "李四");
        var events = new List<SessionChangedEventArgs>();

        provider.Changed += (_, e) => events.Add(e);

        provider.Set(first);
        provider.Set(second);

        events.Should().HaveCount(2);
        events[0].IsSignIn.Should().BeTrue();
        events[0].Current.Should().BeSameAs(first);
        events[0].Previous.Should().BeNull();
        events[1].Previous.Should().BeSameAs(first);
        events[1].Current.Should().BeSameAs(second);
        provider.Current.Should().BeSameAs(second);
    }

    [Fact]
    public void Clear_触发事件_带原因且清空当前会话()
    {
        var provider = new SessionProvider();
        var session = SessionFor(UserRole.Viewer, "观察席");
        SessionChangedEventArgs? captured = null;

        provider.Set(session);
        provider.Changed += (_, e) => captured = e;

        provider.Clear("会话超时");

        captured.Should().NotBeNull();
        captured!.IsSignOut.Should().BeTrue();
        captured.Reason.Should().Be("会话超时");
        captured.Previous.Should().BeSameAs(session);
        provider.Current.Should().BeNull();
    }

    [Fact]
    public void Clear_未登录_不触发事件()
    {
        var provider = new SessionProvider();
        var triggered = false;

        provider.Changed += (_, _) => triggered = true;

        provider.Clear("多余调用");

        triggered.Should().BeFalse();
    }

    private static UserSession SessionFor(UserRole role, string name) => new()
    {
        UserName = name,
        DisplayName = name,
        Role = role,
        Source = AuthenticationSource.LocalAccount
    };
}
