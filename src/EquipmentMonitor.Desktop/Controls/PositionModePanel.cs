using System.Globalization;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Desktop.Controls;

/// <summary>
/// 定位模式面板：绝对/相对选择、目标输入、定位按钮。
/// </summary>
/// <remarks>
/// <para>本控件只负责收集参数并抛出 <see cref="MoveRequested"/>；权限、二次确认、审计、
/// 结果展示由 <see cref="AxisOperationPanel"/> 统一处理。</para>
/// <para>绝对/相对切换只改标签与请求键，不改变布局。</para>
/// </remarks>
public sealed partial class PositionModePanel : UserControl
{
    /// <summary>请求执行定位；参数是已构造好的命令请求。</summary>
    public event Action<ElementCommandRequest>? MoveRequested;

    /// <summary>输入非法时通知宿主显示原因。</summary>
    public event Action<string>? ValidationFailed;

    /// <summary>设计器专用构造。</summary>
    public PositionModePanel()
    {
        InitializeComponent();

        _absoluteRadio.CheckedChanged += (_, _) => UpdateModeLabels();
        _relativeRadio.CheckedChanged += (_, _) => UpdateModeLabels();
        _moveButton.Click += (_, _) => RaiseMoveRequest();

        var toolTip = new ToolTip();
        toolTip.SetToolTip(_relativeRadio, "MC_MoveRelative：从当前位置移动指定距离。");
    }

    /// <summary>是否为相对定位。</summary>
    public bool IsRelative => _relativeRadio.Checked;

    /// <summary>绑定当前轴位置与单位。</summary>
    public void Bind(double currentPosition, string unit)
    {
        _targetUnitLabel.Text = unit;
        _absoluteRadio.Checked = true;
        _targetPositionBox.Text = currentPosition.ToString("F3", CultureInfo.CurrentCulture);
        UpdateModeLabels();
    }

    /// <summary>启用/禁用整个定位组。</summary>
    public void SetEnabled(bool enabled) => _group.Enabled = enabled;

    /// <summary>按当前输入构造命令请求；数值非法时返回 <c>null</c>。</summary>
    public ElementCommandRequest? BuildRequest()
    {
        if (!TryReadTarget(out var value))
        {
            return null;
        }

        return _relativeRadio.Checked
            ? new ElementCommandRequest
            {
                CommandKey = AxisElement.CommandMoveRelative,
                Arguments = new Dictionary<string, CommandValue>
                {
                    [CommandParameterKeys.Distance] = CommandValue.FromNumber(value)
                }
            }
            : new ElementCommandRequest
            {
                CommandKey = AxisElement.CommandMoveAbsolute,
                Arguments = new Dictionary<string, CommandValue>
                {
                    [CommandParameterKeys.Position] = CommandValue.FromNumber(value)
                }
            };
    }

    /// <summary>切换绝对/相对（测试用）。</summary>
    internal void SelectPositionModeForTest(bool relative)
    {
        _relativeRadio.Checked = relative;
        _absoluteRadio.Checked = !relative;
        UpdateModeLabels();
    }

    /// <summary>设置输入值（测试用）。</summary>
    internal void SetTargetForTest(string text) => _targetPositionBox.Text = text;

    private void RaiseMoveRequest()
    {
        var request = BuildRequest();

        if (request is null)
        {
            ValidationFailed?.Invoke("目标位置不是有效数值。");
            return;
        }

        MoveRequested?.Invoke(request);
    }

    private bool TryReadTarget(out double value)
        => double.TryParse(_targetPositionBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
           || double.TryParse(_targetPositionBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private void UpdateModeLabels()
    {
        var relative = _relativeRadio.Checked;

        _targetLabel.Text = relative ? "相对位移：" : "目标位置：";
        _moveButton.Text = relative ? "相对" : "定位";
    }
}
