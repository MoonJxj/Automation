using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Desktop.Controls;

// ⚠️ 分部类边界：控件字段与 InitializeComponent 只在 CylinderOperationPage.Designer.cs 中定义。

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 气缸操作台弹窗：承载 <see cref="CylinderOperationPanel"/>。布局在面板的 Designer 里。
/// </summary>
public sealed partial class CylinderOperationPage : Form
{
    /// <summary>设计器专用构造。</summary>
    public CylinderOperationPage()
    {
        InitializeComponent();
    }

    /// <summary>构造操作台并注入权限服务。</summary>
    public CylinderOperationPage(IPermissionService permissions)
        : this()
    {
        ArgumentNullException.ThrowIfNull(permissions);
        _panel.AttachPermissions(permissions);
    }

    /// <summary>绑定一个气缸。</summary>
    public void Bind(IElementCylinder cylinder)
    {
        ArgumentNullException.ThrowIfNull(cylinder);

        Text = $"气缸操作台 — {cylinder.ElementMetadata.Name}";
        _panel.Bind(cylinder);
    }

    /// <summary>内部面板（测试用）。</summary>
    internal CylinderOperationPanel Panel => _panel;
}
