namespace EquipmentMonitor.Desktop.Controls;

/// <summary>
/// 回原点模式面板：只负责按钮与说明，实际命令由 <see cref="AxisOperationPanel"/> 执行。
/// </summary>
public sealed partial class HomeModePanel : UserControl
{
    /// <summary>请求执行回原点。</summary>
    public event Action? HomeRequested;

    /// <summary>设计器专用构造。</summary>
    public HomeModePanel()
    {
        InitializeComponent();

        _homeButton.Click += (_, _) => HomeRequested?.Invoke();
    }

    /// <summary>启用/禁用回原点按钮。</summary>
    public void SetEnabled(bool enabled) => _group.Enabled = enabled;
}
