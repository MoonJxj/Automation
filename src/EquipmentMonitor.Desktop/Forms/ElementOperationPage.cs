using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Desktop.Controls;
using EquipmentMonitor.Infrastructure.Axis;

// ⚠️ 分部类边界：控件字段与 InitializeComponent 只在 ElementOperationPage.Designer.cs 中定义。
// 本文件不要再声明控件字段，也不要再写 InitializeComponent。

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 操作台弹窗：承载 <see cref="AxisOperationPanel"/>。布局在 AxisOperationPanel.Designer.cs。
/// </summary>
/// <remarks>
/// 面板用 UserControl 是为了让 VS 设计器能直接编辑它；本窗体只负责“弹窗 + 注入权限”。
/// </remarks>
public sealed partial class ElementOperationPage : Form
{
    /// <summary>设计器专用构造。</summary>
    public ElementOperationPage()
    {
        InitializeComponent();
    }

    /// <summary>构造操作台弹窗。</summary>
    public ElementOperationPage(IPermissionService permissions)
        : this()
    {
        ArgumentNullException.ThrowIfNull(permissions);

        _panel.AttachPermissions(permissions);
    }

    /// <summary>绑定一根轴。</summary>
    public void Bind(AxisElement axis)
    {
        ArgumentNullException.ThrowIfNull(axis);

        Text = $"操作台 — {axis.ElementMetadata.Name}";
        _panel.Bind(axis);
    }

    /// <summary>内部面板（测试用）。</summary>
    internal AxisOperationPanel Panel => _panel;
}
