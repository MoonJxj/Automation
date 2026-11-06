using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 工位屏蔽对话框：逐个工位勾选屏蔽、填写原因，保存时二次确认并写审计。
/// </summary>
/// <remarks>
/// <para><b>这是“功能选择”页的代码案例</b>：展示一个功能入口从卡片到弹窗、权限、确认、
/// 服务调用与状态回读的完整链路。</para>
/// <para><b>权限分层</b>：无 <see cref="Permission.MachineConfig"/> 时界面只读（保存禁用）；
/// 即使绕过界面，<see cref="IStationBypassService.SetAsync"/> 仍会
/// <c>RequirePermission</c> 抛异常。</para>
/// <para><b>当前为示例数据</b>：服务是内存实现，重启后恢复；接设备后替换服务即可，本窗体不改。</para>
/// </remarks>
public sealed partial class StationBypassDialog : Form
{
    private const int MaxReasonLength = 200;

    private readonly List<StationRow> _rows = [];

    private readonly IStationBypassService? _stationBypass;
    private bool _canEdit;
    private bool _busy;

    /// <summary>设计器专用构造：只初始化界面。</summary>
    public StationBypassDialog()
    {
        InitializeComponent();

        _saveButton.Enabled = false;
    }

    /// <summary>构造工位屏蔽对话框。</summary>
    /// <param name="stationBypass">工位屏蔽服务。</param>
    /// <param name="permissions">权限服务，决定只读 / 可编辑。</param>
    public StationBypassDialog(IStationBypassService stationBypass, IPermissionService permissions)
        : this()
    {
        ArgumentNullException.ThrowIfNull(stationBypass);
        ArgumentNullException.ThrowIfNull(permissions);

        _stationBypass = stationBypass;
        _canEdit = permissions.HasPermission(Permission.MachineConfig);

        BuildStationRows();
        ReloadStates();

        _saveButton.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);

        if (!_canEdit)
        {
            _bannerLabel.Text = "当前账号没有「修改设备配置」权限：只能查看，不能保存。";
            _bannerLabel.BackColor = Color.FromArgb(255, 235, 235);
            _bannerLabel.ForeColor = Color.Firebrick;
        }

        UpdateButtons();
    }

    /// <summary>最近一次保存的摘要（页面据此显示状态）；未保存为 <c>null</c>。</summary>
    internal string? SaveSummary { get; private set; }

    private void BuildStationRows()
    {
        _rowsPanel.SuspendLayout();
        _rowsPanel.Controls.Clear();
        _rows.Clear();

        foreach (var station in StationCatalog.All)
        {
            var panel = new Panel
            {
                Size = new Size(552, 104),
                Margin = new Padding(0, 0, 0, 6),
                BackColor = Color.White
            };

            var name = new Label
            {
                Text = station.Name,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                Bounds = new Rectangle(8, 8, 90, 24),
                BackColor = Color.Transparent
            };

            var toggle = new CheckBox
            {
                Text = "屏蔽此工位",
                Bounds = new Rectangle(100, 6, 150, 26),
                BackColor = Color.Transparent,
                Enabled = _canEdit
            };

            var state = new Label
            {
                Bounds = new Rectangle(258, 8, 282, 24),
                ForeColor = Color.DimGray,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            var reasonLabel = new Label
            {
                Text = "屏蔽原因",
                Bounds = new Rectangle(8, 46, 84, 24),
                ForeColor = Color.DimGray,
                BackColor = Color.Transparent
            };

            var reason = new TextBox
            {
                Bounds = new Rectangle(100, 44, 440, 28),
                PlaceholderText = "启用屏蔽时必填（最多 200 字）",
                MaxLength = MaxReasonLength,
                Enabled = _canEdit
            };

            panel.Controls.Add(name);
            panel.Controls.Add(toggle);
            panel.Controls.Add(state);
            panel.Controls.Add(reasonLabel);
            panel.Controls.Add(reason);

            _rowsPanel.Controls.Add(panel);
            _rows.Add(new StationRow(station.Id, toggle, reason, state));
        }

        _rowsPanel.ResumeLayout();
    }

    private void ReloadStates()
    {
        var stationBypass = _stationBypass;

        if (stationBypass is null)
        {
            return;
        }

        var states = stationBypass.GetStates().ToDictionary(state => state.StationId);

        foreach (var row in _rows)
        {
            if (!states.TryGetValue(row.StationId, out var state))
            {
                continue;
            }

            row.Toggle.Checked = state.IsBypassed;
            row.Reason.Text = state.Reason;
            row.State.Text = Describe(state);
        }
    }

    private async Task SaveAsync()
    {
        var stationBypass = _stationBypass;

        if (stationBypass is null || !_canEdit || _busy)
        {
            return;
        }

        var current = stationBypass.GetStates().ToDictionary(state => state.StationId);
        var changes = new List<(int StationId, bool Bypassed, string Reason)>();

        foreach (var row in _rows)
        {
            if (!current.TryGetValue(row.StationId, out var state))
            {
                continue;
            }

            var target = row.Toggle.Checked;

            if (target == state.IsBypassed)
            {
                continue;
            }

            var reason = row.Reason.Text.Trim();

            if (target && reason.Length == 0)
            {
                SetStatus($"请填写{StationCatalog.Name(row.StationId)}的屏蔽原因。", isError: true);
                row.Reason.Focus();
                return;
            }

            changes.Add((row.StationId, target, reason));
        }

        if (changes.Count == 0)
        {
            SetStatus("没有需要保存的修改。");
            return;
        }

        var summary = string.Join(
            Environment.NewLine,
            changes.Select(change =>
                $"{(change.Bypassed ? "屏蔽" : "恢复")}{StationCatalog.Name(change.StationId)}"));

        var confirmed = MessageBox.Show(
            this,
            $"确认以下变更？{Environment.NewLine}{Environment.NewLine}{summary}",
            "确认工位屏蔽",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirmed != DialogResult.OK)
        {
            return;
        }

        _busy = true;
        UpdateButtons();

        try
        {
            foreach (var change in changes)
            {
                var result = await stationBypass
                    .SetAsync(change.StationId, change.Bypassed, change.Reason)
                    .ConfigureAwait(true);

                if (result.IsFailure)
                {
                    // 可能前几项已生效：回读一次，界面不能停留在“都没改”的旧状态。
                    ReloadStates();
                    SetStatus($"保存失败：{result.Error}", isError: true);
                    return;
                }
            }

            ReloadStates();
            SaveSummary = $"工位屏蔽已更新（{changes.Count} 项）。";
            SetStatus(SaveSummary);
        }
        catch (UnauthorizedAccessException ex)
        {
            ReloadStates();
            SetStatus($"保存失败：{ex.Message}", isError: true);
        }
        catch (Exception ex)
        {
            ReloadStates();
            SetStatus($"保存异常：{ex.Message}", isError: true);
        }
        finally
        {
            _busy = false;
            UpdateButtons();
        }
    }

    private void UpdateButtons()
        => _saveButton.Enabled = _canEdit && !_busy;

    private void SetStatus(string text, bool isError = false)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = isError ? Color.Firebrick : SystemColors.ControlText;
    }

    private static string Describe(StationBypassState state)
    {
        if (!state.IsBypassed)
        {
            return "状态：正常（参与自动循环）";
        }

        var changedAt = state.ChangedAt == DateTimeOffset.MinValue
            ? string.Empty
            : $"（{state.OperatorName} {state.ChangedAt:MM-dd HH:mm}）";

        return $"状态：已屏蔽{changedAt}";
    }

    private sealed record StationRow(int StationId, CheckBox Toggle, TextBox Reason, Label State);
}
