using System.ComponentModel;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 报警信息页：当前报警、历史报警、确认操作。界面布局在 <c>AlarmInfoPage.Designer.cs</c>。
/// </summary>
/// <remarks>
/// <para><b>单一入口</b>：页面只注入 <see cref="IAlarmService"/>，不认任何设备类型；
/// PLC/板卡/气缸的差异在报警源里消化。</para>
/// <para><b>刷新</b>：500ms 定时拉 <see cref="IAlarmService.Snapshot"/>，不订阅 <c>Changed</c>
/// （与现有总览页一致，避免页面反复开关时事件泄漏）。</para>
/// <para><b>权限</b>：确认按钮按 <see cref="Permission.AlarmAcknowledge"/> 显隐；
/// 真正的校验在 <see cref="IAlarmService.Acknowledge"/> 内部，UI 隐藏不算权限控制。</para>
/// </remarks>
public sealed partial class AlarmInfoPage : Form
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<AlarmInfoPage>();

    private readonly List<Row> _rows = [];

    private IAlarmService? _alarms;
    private IPermissionService? _permissions;
    private System.Windows.Forms.Timer? _timer;
    private IReadOnlyList<AlarmRecord> _historyRecords = [];
    private string _signature = string.Empty;
    private string? _selectedId;
    private bool _showHistory;
    private bool _canAcknowledge;

    /// <summary>设计器专用构造：只初始化界面与列，不连接任何服务。</summary>
    public AlarmInfoPage()
    {
        InitializeComponent();
        AddColumns();

        _levelFilter.Items.AddRange(new object[] { "全部等级", "警告及以上", "错误及以上", "严重" });
        _levelFilter.SelectedIndex = 0;
        _stateFilter.Items.AddRange(new object[] { "全部状态", "未确认", "活动", "已恢复未确认" });
        _stateFilter.SelectedIndex = 0;
    }

    /// <summary>构造报警页。</summary>
    /// <param name="alarms">报警服务（唯一入口）。</param>
    /// <param name="permissions">权限服务，仅用于按钮显隐。</param>
    public AlarmInfoPage(IAlarmService alarms, IPermissionService permissions)
        : this()
    {
        ArgumentNullException.ThrowIfNull(alarms);
        ArgumentNullException.ThrowIfNull(permissions);

        _alarms = alarms;
        _permissions = permissions;

        var canAcknowledge = permissions.HasPermission(Permission.AlarmAcknowledge);
        _canAcknowledge = canAcknowledge;
        _ackButton.Enabled = canAcknowledge;
        _ackAllButton.Enabled = canAcknowledge;

        _table.CellClick += (_, e) =>
        {
            if (e.Record is Row row)
            {
                var index = FindRowIndex(row);

                if (index >= 0)
                {
                    SelectRow(index);
                }
            }
        };

        _refreshButton.Click += (_, _) =>
        {
            _signature = string.Empty;
            RefreshView();
        };

        _historyButton.Click += async (_, _) => await ToggleHistoryAsync().ConfigureAwait(true);
        _ackButton.Click += (_, _) => AcknowledgeSelected();
        _ackAllButton.Click += (_, _) => AcknowledgeAll();

        _levelFilter.SelectedIndexChanged += (_, _) => OnFilterChanged();
        _stateFilter.SelectedIndexChanged += (_, _) => OnFilterChanged();

        _timer = new System.Windows.Forms.Timer { Interval = 500 };
        _timer.Tick += (_, _) => RefreshView();

        VisibleChanged += (_, _) =>
        {
            if (Visible)
            {
                // 切换用户后权限可能变化：页面是缓存的，按钮状态不能停在旧会话。
                _canAcknowledge = _permissions?.HasPermission(Permission.AlarmAcknowledge) ?? false;
                _signature = string.Empty;
                RefreshView();
                _timer?.Start();
            }
            else
            {
                _timer?.Stop();
            }
        };

        FormClosed += (_, _) =>
        {
            _timer?.Dispose();
            _timer = null;
        };
    }

    /// <summary>当前表格行数（测试用）。</summary>
    internal int RowCount => _rows.Count;

    /// <summary>当前渲染的行（测试用）。</summary>
    internal IReadOnlyList<(string Level, string State, string Message)> RenderedRows
        => _table.DataSource is BindingList<Row> rows
            ? rows.Select(row => (row.level, row.state, row.message)).ToArray()
            : [];

    /// <summary>当前选中报警 Id（测试用）。</summary>
    internal string? SelectedId => _selectedId;

    /// <summary>确认按钮是否可用（测试用）。</summary>
    internal bool CanAcknowledge => _ackButton.Enabled;

    /// <summary>底部提示文本（测试用）。</summary>
    internal string HintText => _hint.Text ?? string.Empty;

    /// <summary>确认选中行（生产路径由按钮触发，测试直接调用）。</summary>
    internal void AcknowledgeSelectedForTest() => AcknowledgeSelected();

    /// <summary>确认全部（生产路径由按钮触发，测试直接调用）。</summary>
    internal void AcknowledgeAllForTest() => AcknowledgeAll();

    /// <summary>切换当前/历史（生产路径由按钮触发，测试直接调用）。</summary>
    internal Task ToggleHistoryForTestAsync() => ToggleHistoryAsync();

    /// <summary>刷新视图（生产路径由定时器触发，测试直接调用）。</summary>
    internal void RefreshView()
    {
        var alarms = _alarms;

        if (alarms is null)
        {
            return;
        }

        if (_showHistory)
        {
            ApplyHistoryRows();
            return;
        }

        var snapshot = alarms.Snapshot;
        var rows = snapshot.Active
            .Where(Matches)
            .Select(ToRow)
            .ToList();

        _hint.Text = snapshot.Count == 0
            ? "当前无报警。"
            : $"活动报警 {snapshot.Count} 条（未确认 {snapshot.UnacknowledgedCount}）。";
        _hint.ForeColor = snapshot.UnacknowledgedCount > 0 ? Color.Firebrick : SystemColors.GrayText;

        ApplyRows(rows);
    }

    /// <summary>选中一行（生产路径由 CellClick 触发，测试直接调用）。</summary>
    internal void SelectRow(int index)
    {
        if (index < 0 || index >= _rows.Count)
        {
            return;
        }

        _selectedId = _rows[index].Id;
        _table.SelectedIndex = index + 1;
        _detailLabel.Text = _rows[index].detail;
    }

    private void AddColumns()
    {
        _table.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;

        _table.Columns.Add(new AntdUI.Column("level", "等级") { Width = "70" });
        _table.Columns.Add(new AntdUI.Column("state", "状态") { Width = "90" });
        _table.Columns.Add(new AntdUI.Column("raised", "首次发生") { Width = "120" });
        _table.Columns.Add(new AntdUI.Column("lastSeen", "最近发生") { Width = "80" });
        _table.Columns.Add(new AntdUI.Column("source", "来源") { Width = "110" });
        _table.Columns.Add(new AntdUI.Column("code", "代码") { Width = "140" });
        _table.Columns.Add(new AntdUI.Column("message", "描述") { Width = "340" });
        _table.Columns.Add(new AntdUI.Column("ackBy", "确认人") { Width = "90" });
    }

    private void OnFilterChanged()
    {
        _signature = string.Empty;
        RefreshView();
    }

    private void ApplyHistoryRows()
    {
        var rows = _historyRecords
            .Where(Matches)
            .Select(ToRow)
            .ToList();

        _hint.Text = $"历史报警 {_historyRecords.Count} 条（近 7 天）。";
        _hint.ForeColor = SystemColors.GrayText;

        ApplyRows(rows);
    }

    private void ApplyRows(List<Row> rows)
    {
        var signature = string.Join(
            '|',
            rows.Select(row =>
                $"{row.Id}/{row.level}/{row.state}/{row.raised}/{row.lastSeen}/{row.message}/{row.ackBy}"));

        _rows.Clear();
        _rows.AddRange(rows);

        // 没有可确认的记录时按钮置灰，避免“点了没反应”。
        _ackButton.Enabled = _canAcknowledge && _rows.Count > 0;
        _ackAllButton.Enabled = _canAcknowledge && _rows.Count > 0;

        if (signature == _signature)
        {
            return;
        }

        _signature = signature;
        _table.DataSource = new BindingList<Row>(rows.ToList());

        if (_selectedId is not null)
        {
            var index = rows.FindIndex(row => string.Equals(row.Id, _selectedId, StringComparison.Ordinal));

            if (index >= 0)
            {
                _table.SelectedIndex = index + 1;
            }
            else
            {
                _selectedId = null;
                _table.SelectedIndex = 0;
            }
        }

        if (_selectedId is null && rows.Count > 0)
        {
            SelectRow(0);
        }
    }

    private int FindRowIndex(Row row)
    {
        for (var index = 0; index < _rows.Count; index++)
        {
            if (ReferenceEquals(_rows[index], row))
            {
                return index;
            }
        }

        return -1;
    }

    private void AcknowledgeSelected()
    {
        var alarms = _alarms;

        if (alarms is null)
        {
            return;
        }

        if (_selectedId is null)
        {
            _hint.Text = "先选中一条报警。";
            _hint.ForeColor = Color.Firebrick;
            return;
        }

        var result = alarms.Acknowledge(_selectedId);

        if (result.IsFailure)
        {
            _hint.Text = $"确认失败：{result.Error}";
            _hint.ForeColor = Color.Firebrick;
            return;
        }

        _signature = string.Empty;
        RefreshView();
    }

    private void AcknowledgeAll()
    {
        var alarms = _alarms;

        if (alarms is null)
        {
            return;
        }

        var result = alarms.AcknowledgeAll();

        if (result.IsFailure)
        {
            _hint.Text = $"确认失败：{result.Error}";
            _hint.ForeColor = Color.Firebrick;
            return;
        }

        _signature = string.Empty;
        RefreshView();
    }

    private async Task ToggleHistoryAsync()
    {
        var alarms = _alarms;

        if (alarms is null)
        {
            return;
        }

        _showHistory = !_showHistory;
        _historyButton.Text = _showHistory ? "当前报警" : "历史报警";
        _selectedId = null;
        _signature = string.Empty;

        if (!_showHistory)
        {
            _historyRecords = [];
            RefreshView();
            return;
        }

        try
        {
            _historyRecords = await alarms.QueryHistoryAsync(
                new AlarmHistoryQuery
                {
                    From = DateTimeOffset.Now.AddDays(-7),
                    Limit = 1000
                }).ConfigureAwait(true);

            ApplyHistoryRows();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "读取报警历史失败。");
            _hint.Text = $"读取历史失败：{ex.Message}";
            _hint.ForeColor = Color.Firebrick;
        }
    }

    private bool Matches(AlarmRecord record)
    {
        var minimum = _levelFilter.SelectedIndex switch
        {
            1 => AlarmLevel.Warning,
            2 => AlarmLevel.Error,
            3 => AlarmLevel.Critical,
            _ => (AlarmLevel?)null
        };

        if (minimum is { } level && record.Level < level)
        {
            return false;
        }

        return _stateFilter.SelectedIndex switch
        {
            1 => record.AcknowledgedAt is null,
            2 => record.IsActive,
            3 => record.State == AlarmState.RecoveredUnacknowledged,
            _ => true
        };
    }

    private static Row ToRow(AlarmRecord record) => new(
        record.Id,
        LevelText(record.Level),
        StateText(record.State),
        record.RaisedAt.ToLocalTime().ToString("MM-dd HH:mm:ss"),
        record.LastSeenAt.ToLocalTime().ToString("HH:mm:ss"),
        record.ElementName ?? record.SourceId,
        record.Code,
        record.Message,
        record.AcknowledgedBy ?? "—",
        record.Remedy is null ? record.Message : $"{record.Message}  建议：{record.Remedy}");

    private static string LevelText(AlarmLevel level) => level switch
    {
        AlarmLevel.Info => "提示",
        AlarmLevel.Warning => "警告",
        AlarmLevel.Error => "错误",
        AlarmLevel.Critical => "严重",
        _ => level.ToString()
    };

    private static string StateText(AlarmState state) => state switch
    {
        AlarmState.Active => "未确认",
        AlarmState.Acknowledged => "已确认",
        AlarmState.RecoveredUnacknowledged => "已恢复未确认",
        AlarmState.Cleared => "已恢复",
        _ => state.ToString()
    };

    private sealed record Row(
        string Id,
        string level,
        string state,
        string raised,
        string lastSeen,
        string source,
        string code,
        string message,
        string ackBy,
        string detail);
}
