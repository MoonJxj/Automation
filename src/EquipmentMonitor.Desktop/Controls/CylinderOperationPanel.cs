using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Core.Cylinder;

// ⚠️ 分部类边界：控件字段与 InitializeComponent 只在 CylinderOperationPanel.Designer.cs 中定义。
// 本文件不要再声明控件字段，也不要再写 InitializeComponent。

namespace EquipmentMonitor.Desktop.Controls;

/// <summary>
/// 气缸操作面板：伸出/缩回/断阀/复位、限位与故障指示。
/// 布局在 <c>CylinderOperationPanel.Designer.cs</c>，可在 VS 设计器中编辑。
/// </summary>
/// <remarks>
/// <para>命令语义与轴一致：按钮成功只表示<b>已下发</b>，到位由限位反馈观察；
/// 超时后置故障，必须点“复位”。</para>
/// <para>“断阀”对单控气缸意味着弹簧复位（会缩回），对双控气缸意味着停在原地；
/// 界面提示必须如实说明，不能承诺“停在中间”。</para>
/// </remarks>
public sealed partial class CylinderOperationPanel : UserControl
{
    private static readonly TimeSpan ResultHoldTime = TimeSpan.FromSeconds(3);

    private IPermissionService? _permissions;
    private IElementCylinder? _cylinder;
    private System.Windows.Forms.Timer? _refreshTimer;
    private ToolTip? _toolTip;
    private bool _allowed;
    private bool _busy;
    private DateTimeOffset _resultAt = DateTimeOffset.MinValue;
    private string? _resultText;

    /// <summary>设计器专用构造。</summary>
    public CylinderOperationPanel()
    {
        InitializeComponent();
        _stripePanel.Paint += (_, e) => PaintWarningStripe(e.Graphics, _stripePanel.ClientSize);
    }

    /// <summary>构造面板并注入权限服务。</summary>
    public CylinderOperationPanel(IPermissionService permissions)
        : this()
    {
        AttachPermissions(permissions);
    }

    /// <summary>注入权限服务（设计器先无参构造，运行期由宿主注入）。</summary>
    public void AttachPermissions(IPermissionService permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        _permissions = permissions;

        _toolTip = new ToolTip();
        _toolTip.SetToolTip(_extendButton, "置位伸出电磁阀；到位由限位反馈观察。");
        _toolTip.SetToolTip(_retractButton, "置位缩回电磁阀；单控气缸断阀后弹簧复位。");
        _toolTip.SetToolTip(_haltButton, "断开两个电磁阀：单控会缩回，双控停在原地。");
        _toolTip.SetToolTip(_resetButton, "清除超时/限位矛盾故障。");

        _extendButton.Click += (_, _) => _ = ExecuteAsync(CylinderElement.CommandExtend);
        _retractButton.Click += (_, _) => _ = ExecuteAsync(CylinderElement.CommandRetract);
        _haltButton.Click += (_, _) => _ = ExecuteAsync(CylinderElement.CommandHalt);
        _resetButton.Click += (_, _) => _ = ExecuteAsync(CylinderElement.CommandReset);

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 150 };
        _refreshTimer.Tick += (_, _) => RefreshView();
        _refreshTimer.Start();
    }

    /// <summary>绑定一个气缸。</summary>
    public void Bind(IElementCylinder cylinder)
    {
        ArgumentNullException.ThrowIfNull(cylinder);

        _cylinder = cylinder;

        _titleLabel.Text = cylinder.ElementMetadata.Name;
        _subtitleLabel.Text =
            $"{(cylinder.IsDoubleActing ? "双控" : "单控（弹簧复位）")} · 动作超时 {cylinder.ActionTimeoutSeconds:F1}s";

        _allowed = _permissions?.HasPermission(Permission.MachineOperate) ?? false;

        if (!_allowed)
        {
            SetButtonsEnabled(false);
            _hintLabel.Text = "当前账号无「设备操作」权限 —— 控制区已禁用。";
        }

        RefreshView();
    }

    /// <summary>当前绑定的气缸（测试用）。</summary>
    internal IElementCylinder? CurrentCylinder => _cylinder;

    /// <summary>当前是否允许操作（测试用）。</summary>
    internal bool HasOperationControls => _cylinder is not null && _allowed;

    /// <summary>提示文本（测试用）。</summary>
    internal string AvailabilityText => _hintLabel.Text ?? string.Empty;

    /// <summary>最近命令结果（测试用）。</summary>
    internal string ResultText => _resultText ?? _infoBox.Text ?? string.Empty;

    /// <summary>位置文本（测试用）。</summary>
    internal string PositionText => _cylinder?.Status.PositionText ?? "未知";

    /// <summary>伸出按钮是否可用（测试用）。</summary>
    internal bool IsExtendEnabled => _extendButton.Enabled;

    /// <summary>缩回按钮是否可用（测试用）。</summary>
    internal bool IsRetractEnabled => _retractButton.Enabled;

    /// <summary>刷新指示器、限位与按钮状态。</summary>
    internal void RefreshView()
    {
        var cylinder = _cylinder;

        if (cylinder is null)
        {
            return;
        }

        var status = cylinder.Status;

        SetIndicator(_extendedIndicator, status.IsExtended ? Color.LimeGreen : Color.DimGray);
        SetIndicator(_retractedIndicator, status.IsRetracted ? Color.LimeGreen : Color.DimGray);
        SetIndicator(_movingIndicator, status.IsMoving ? Color.Orange : Color.DimGray);
        SetIndicator(_faultIndicator, status.IsFault ? Color.Red : Color.DimGray);

        _extendedSensorLabel.Text = $"伸出限位：{(status.ExtendedSensor ? "ON" : "OFF")}";
        _retractedSensorLabel.Text = $"缩回限位：{(status.RetractedSensor ? "ON" : "OFF")}";

        if (_resultText is null || DateTimeOffset.Now - _resultAt > ResultHoldTime)
        {
            _infoBox.Text = Describe(cylinder);
        }
        else
        {
            _infoBox.Text = _resultText;
        }

        if (string.IsNullOrWhiteSpace(status.LastError))
        {
            _errorBox.Text = "正常";
            _errorBox.ForeColor = SystemColors.WindowText;
        }
        else
        {
            _errorBox.Text = status.LastError;
            _errorBox.ForeColor = Color.Firebrick;
        }

        if (_allowed)
        {
            UpdateButtons(cylinder);
            UpdateHint(cylinder, status);
        }
    }

    private void UpdateButtons(IElementCylinder cylinder)
    {
        _extendButton.Enabled = !_busy && cylinder.EvaluateCommand(CylinderElement.CommandExtend).IsAvailable;
        _retractButton.Enabled = !_busy && cylinder.EvaluateCommand(CylinderElement.CommandRetract).IsAvailable;
        _haltButton.Enabled = !_busy && cylinder.EvaluateCommand(CylinderElement.CommandHalt).IsAvailable;
        _resetButton.Enabled = !_busy && cylinder.EvaluateCommand(CylinderElement.CommandReset).IsAvailable;
    }

    private void UpdateHint(IElementCylinder cylinder, CylinderStatus status)
    {
        if (status.IsFault)
        {
            _hintLabel.Text = "提示：气缸故障 —— 先点「复位」，再检查限位/机械。";
            return;
        }

        if (status.IsMoving)
        {
            _hintLabel.Text = "提示：动作中，等待限位到位；超时会自动置故障。";
            return;
        }

        var availability = cylinder.EvaluateCommand(CylinderElement.CommandExtend);

        _hintLabel.Text = availability.IsAvailable
            ? string.Empty
            : $"提示：{availability.Reason}";
    }

    private async Task ExecuteAsync(string commandKey)
    {
        var cylinder = _cylinder;

        if (cylinder is null || _busy)
        {
            return;
        }

        var command = cylinder.Commands.FirstOrDefault(
            item => string.Equals(item.Key, commandKey, StringComparison.Ordinal));

        if (command is null)
        {
            ShowResult($"{commandKey}：该气缸不支持此命令。");
            return;
        }

        if (command.Risk == ElementCommandRisk.Confirm && !Confirm(command))
        {
            ShowResult($"{command.DisplayName}：已取消。");
            return;
        }

        await ExecuteAsync(command, new ElementCommandRequest { CommandKey = commandKey }).ConfigureAwait(true);
    }

    /// <summary>执行一条已构造的命令（测试直接调用，绕过确认框）。</summary>
    internal async Task ExecuteAsync(ElementCommandDescriptor command, ElementCommandRequest request)
    {
        var cylinder = _cylinder;

        if (cylinder is null || _busy)
        {
            return;
        }

        _busy = true;

        try
        {
            var result = await cylinder.ExecuteCommandAsync(request).ConfigureAwait(true);

            if (result.IsFailure)
            {
                ShowResult($"{command.DisplayName}：未执行（{result.ErrorCode}）—— {result.Error}");
            }
            else
            {
                var outcome = result.Value.Outcome switch
                {
                    CommandOutcome.Accepted => "已下发",
                    CommandOutcome.Done => "已完成",
                    CommandOutcome.CommandAborted => "已中止",
                    CommandOutcome.Timeout => "超时",
                    CommandOutcome.Error => "失败",
                    _ => "未知"
                };

                var message = string.IsNullOrWhiteSpace(result.Value.Message)
                    ? outcome
                    : result.Value.Outcome == CommandOutcome.Accepted
                        ? result.Value.Message
                        : $"{outcome} —— {result.Value.Message}";

                ShowResult($"{command.DisplayName}：{message}");
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowResult($"{command.DisplayName}：权限不足 —— {ex.Message}");
        }
        finally
        {
            _busy = false;
            RefreshView();
        }
    }

    private bool Confirm(ElementCommandDescriptor command)
        => MessageBox.Show(
            FindForm(),
            $"元件：{_cylinder?.ElementMetadata.ElementId}{Environment.NewLine}" +
            $"命令：{command.DisplayName}（{command.Key}）{Environment.NewLine}{Environment.NewLine}" +
            (_cylinder?.IsDoubleActing == true
                ? "双控气缸：另一个电磁阀会先断开，避免同时得电。"
                : "单控气缸：断电后弹簧会复位到缩回位。") + Environment.NewLine +
            "确认要执行这条命令吗？",
            "二次确认",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.OK;

    private void ShowResult(string text)
    {
        _resultText = text;
        _resultAt = DateTimeOffset.Now;
        _infoBox.Text = text;
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _extendButton.Enabled = enabled;
        _retractButton.Enabled = enabled;
        _haltButton.Enabled = enabled;
        _resetButton.Enabled = enabled;
    }

    private static void SetIndicator(Control indicator, Color color) => indicator.BackColor = color;

    private static string Describe(IElementCylinder cylinder)
    {
        var status = cylinder.Status;

        if (!status.IsEnabled)
        {
            return "气缸已在配置中禁用。";
        }

        if (status.IsFault)
        {
            return status.LastError ?? "气缸故障。";
        }

        return status.Position switch
        {
            CylinderPosition.Extended => "伸出到位。",
            CylinderPosition.Retracted => "缩回到位。",
            CylinderPosition.Moving => "动作中，等待限位到位。",
            CylinderPosition.Intermediate => "停在中间或限位矛盾，请检查传感器。",
            _ => "位置未知，等待采集。"
        };
    }

    private static void PaintWarningStripe(Graphics graphics, Size size)
    {
        using var pen = new Pen(Color.White, 5);

        for (var x = -size.Height; x < size.Width; x += 18)
        {
            graphics.DrawLine(pen, x, size.Height, x + size.Height, 0);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer?.Dispose();
            _toolTip?.Dispose();
        }

        base.Dispose(disposing);
    }
}
