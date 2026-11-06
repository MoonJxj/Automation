using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Controls;
using EquipmentMonitor.Desktop.Parameter;
using FluentAssertions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 通用参数编辑器：按描述符生成行、权限、单参数校验、值收集。
/// </summary>
public sealed class ParameterEditorControlTests
{
    [Fact]
    public void Load_按描述符生成行并应用权限()
    {
        StaTestHost.RunSta(() =>
        {
            var snapshot = Snapshot();
            using var editor = new ParameterEditorControl();

            editor.AttachPermissions(DesktopTestServices.AllowPermissions());
            editor.LoadSnapshot(snapshot);

            editor.RowCount.Should().Be(snapshot.Descriptors.Count);
            editor.EditableRowCount.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void 无权限_全部字段只读()
    {
        StaTestHost.RunSta(() =>
        {
            var snapshot = Snapshot();
            using var editor = new ParameterEditorControl();

            editor.AttachPermissions(DesktopTestServices.DenyPermissions());
            editor.LoadSnapshot(snapshot);

            editor.EditableRowCount.Should().Be(0);
        });
    }

    [Fact]
    public void 修改数值_生成新的参数集并标记脏()
    {
        StaTestHost.RunSta(() =>
        {
            var snapshot = Snapshot();
            using var editor = new ParameterEditorControl();
            editor.AttachPermissions(DesktopTestServices.AllowPermissions());
            editor.LoadSnapshot(snapshot);

            editor.SetEditorValueForTest(AxisParameterKeys.DefaultVelocity, "12.5");
            var ok = editor.TryBuildSet(out var values, out var error);

            ok.Should().BeTrue(error);
            values.Should().NotBeNull();
            values!.Values[AxisParameterKeys.DefaultVelocity].TryAsNumber(out var value).Should().BeTrue();
            value.Should().Be(12.5);
            editor.IsDirty.Should().BeTrue();
        });
    }

    [Fact]
    public void 超出范围_返回原因且不生成集合()
    {
        StaTestHost.RunSta(() =>
        {
            var snapshot = Snapshot();
            using var editor = new ParameterEditorControl();
            editor.AttachPermissions(DesktopTestServices.AllowPermissions());
            editor.LoadSnapshot(snapshot);

            editor.SetEditorValueForTest(AxisParameterKeys.StationId, "0");
            var ok = editor.TryBuildSet(out var values, out var error);

            ok.Should().BeFalse();
            values.Should().BeNull();
            error.Should().Contain("超出范围");
        });
    }

    private static ElementParameterSnapshot Snapshot()
    {
        var source = new TemplateParameterPageSource();
        var result = source.GetAsync("Axis-0-0").GetAwaiter().GetResult();

        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value;
    }
}
