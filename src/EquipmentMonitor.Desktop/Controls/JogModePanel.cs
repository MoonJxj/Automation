using System.Globalization;

namespace EquipmentMonitor.Desktop.Controls;

/// <summary>
/// 点动模式参数面板：只读显示速度/加减速度/加加速度。
/// </summary>
/// <remarks>只负责展示与启用状态；点动按钮和命令仍由 <see cref="AxisOperationPanel"/> 统一处理。</remarks>
public sealed partial class JogModePanel : UserControl
{
    /// <summary>设计器专用构造。</summary>
    public JogModePanel()
    {
        InitializeComponent();

        var toolTip = new ToolTip();
        toolTip.SetToolTip(_velocityBox, "来自轴配置；单次命令覆盖暂不支持。");
        toolTip.SetToolTip(_accelBox, "来自轴配置；单次命令覆盖暂不支持。");
        toolTip.SetToolTip(_jerkBox, "来自轴配置；单次命令覆盖暂不支持。");
    }

    /// <summary>绑定轴参数。</summary>
    public void Bind(string unit, double defaultVelocity, double acceleration, double jerk)
    {
        _velocityUnitLabel.Text = $"{unit}/s";
        _accelUnitLabel.Text = $"{unit}/s²";
        _jerkUnitLabel.Text = $"{unit}/s³";

        _velocityBox.Text = defaultVelocity.ToString("F1", CultureInfo.CurrentCulture);
        _accelBox.Text = acceleration.ToString("F1", CultureInfo.CurrentCulture);
        _jerkBox.Text = jerk.ToString("F1", CultureInfo.CurrentCulture);
        _jerkLimitCheck.Checked = jerk > 0;
        _jerkBox.Enabled = jerk > 0;
    }

    /// <summary>启用/禁用参数区。</summary>
    public void SetEnabled(bool enabled)
    {
        _group.Enabled = enabled;
        _jerkLimitCheck.Enabled = enabled && _jerkBox.Enabled;
    }
}
