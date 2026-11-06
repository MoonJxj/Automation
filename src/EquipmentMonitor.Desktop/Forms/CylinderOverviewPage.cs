using System.ComponentModel;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Desktop.Monitoring;
using EquipmentMonitor.Core.Cylinder;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 气缸控制页：气缸列表 + 打开操作台弹窗（或双击行）。
/// </summary>
public sealed partial class CylinderOverviewPage : Form
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<CylinderOverviewPage>();

    private readonly List<Row> _rows = [];

    private CylinderSnapshotStore? _store;
    private IServiceProvider? _services;
    private System.Windows.Forms.Timer? _timer;
    private string _signature = string.Empty;
    private int _clickedIndex = -1;
    private CylinderElement? _selectedElement;

    /// <summary>工位过滤；<c>null</c> 表示显示全部（自动监控），手动操作页设置。</summary>
    internal int? StationFilter { get; set; }

    /// <summary>设计器专用构造。</summary>
    public CylinderOverviewPage()
    {
        InitializeComponent();
        AddColumns();
    }

    /// <summary>构造气缸页。</summary>
    public CylinderOverviewPage(CylinderSnapshotStore store, IServiceProvider services)
        : this()
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(services);

        _store = store;
        _services = services;

        _table.CellClick += (_, e) =>
        {
            Logger.Debug("CellClick RowIndex={RowIndex} RowType={RowType}", e.RowIndex, e.RowType);

            if (e.Record is Row row)
            {
                SelectRecord(row);
            }
        };

        _table.CellDoubleClick += (_, e) =>
        {
            if (e.Record is Row row)
            {
                var index = FindRowIndex(row);

                if (index >= 0)
                {
                    OpenRow(index);
                }
            }
        };

        _openButton.Click += (_, _) => OpenSelected();

        _timer = new System.Windows.Forms.Timer { Interval = 500 };
        _timer.Tick += (_, _) => RefreshView();

        VisibleChanged += (_, _) =>
        {
            if (Visible)
            {
                RefreshView();
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
        };

        FormClosed += (_, _) => _timer?.Dispose();
    }

    /// <summary>当前表格行数（测试用）。</summary>
    internal int RowCount => RenderedRows.Count;

    /// <summary>当前渲染的行（测试用）。</summary>
    internal IReadOnlyList<(string Name, string State, string Position)> RenderedRows
        => _table.DataSource is BindingList<Row> rows
            ? rows.Select(row => (row.name, row.state, row.position)).ToArray()
            : [];

    /// <summary>底部提示文本（测试用）。</summary>
    internal string HintText => _hint.Text ?? string.Empty;

    /// <summary>当前选中的气缸（测试用）。</summary>
    internal CylinderElement? SelectedCylinder()
    {
        if (_selectedElement is not null)
        {
            return _selectedElement;
        }

        var selected = _table.SelectedIndex;
        var index = selected > 0 ? selected - 1 : _clickedIndex;

        return index >= 0 && index < _rows.Count ? _rows[index].Element : null;
    }

    /// <summary>选中一行（测试用；生产路径由 CellClick 触发）。</summary>
    internal void SelectRow(int index)
    {
        _clickedIndex = index;

        if (index < 0 || index >= _rows.Count)
        {
            return;
        }

        _selectedElement = _rows[index].Element;

        if (_table.SelectedIndex != index + 1)
        {
            _table.SelectedIndex = index + 1;
        }
    }

    /// <summary>用行对象定位，避免 AntdUI 的 1 基 RowIndex 偏移。</summary>
    private void SelectRecord(Row row)
    {
        var index = FindRowIndex(row);

        if (index >= 0)
        {
            SelectRow(index);
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

    /// <summary>重绑表格（测试用；生产路径由 500ms 定时器触发）。</summary>
    internal void RefreshView()
    {
        var store = _store;

        if (store is null)
        {
            return;
        }

        var snapshot = store.Current;
        var elements = store.Elements
            .ToDictionary(element => element.ElementMetadata.ElementId, StringComparer.Ordinal);

        var rows = snapshot.Cylinders
            .Where(cylinder => StationFilter is null || cylinder.StationId == StationFilter)
            .OrderBy(cylinder => Severity(cylinder.State))
            .ThenBy(cylinder => cylinder.Name, StringComparer.Ordinal)
            .Select(cylinder => ToRow(cylinder, elements.GetValueOrDefault(cylinder.ElementId)))
            .ToList();

        ApplyRows(rows, snapshot);
    }

    /// <summary>打开选中气缸（按钮/双击/测试共用）。</summary>
    internal void OpenSelected()    {
        var element = SelectedCylinder();

        if (element is null)
        {
            _hint.Text = "先选中一行，再打开气缸操作台。";
            return;
        }

        Open(element);
    }

    private void AddColumns()
    {
        _table.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;

        _table.Columns.Add(new AntdUI.Column("name", "气缸") { Width = "190" });
        _table.Columns.Add(new AntdUI.Column("state", "状态") { Width = "80" });
        _table.Columns.Add(new AntdUI.Column("position", "位置") { Width = "110" });
        _table.Columns.Add(new AntdUI.Column("extended", "伸出限位") { Width = "85" });
        _table.Columns.Add(new AntdUI.Column("retracted", "缩回限位") { Width = "85" });
        _table.Columns.Add(new AntdUI.Column("fault", "故障") { Width = "80" });
    }

    private void ApplyRows(List<Row> rows, CylinderMonitorSnapshot snapshot)
    {
        var signature = string.Join(
            '|',
            rows.Select(row => $"{row.name}/{row.state}/{row.position}/{row.extended}/{row.retracted}/{row.fault}/{row.updated}"));

        _hint.Text = snapshot.LastError is null
            ? $"最近刷新：{FormatTime(snapshot.RefreshedAt)}（{rows.Count} 个气缸）"
            : $"采集异常：{snapshot.LastError}";
        _hint.ForeColor = snapshot.LastError is null ? SystemColors.GrayText : Color.Firebrick;

        _rows.Clear();
        _rows.AddRange(rows);

        if (signature != _signature)
        {
            _signature = signature;
            _table.DataSource = new BindingList<Row>(rows.ToList());

            if (_selectedElement is { } selected)
            {
                var index = rows.FindIndex(row => ReferenceEquals(row.Element, selected));

                if (index >= 0)
                {
                    _table.SelectedIndex = index + 1;
                }
                else
                {
                    _selectedElement = null;
                    _table.SelectedIndex = 0;
                }
            }

            if (_selectedElement is null && rows.Count > 0)
            {
                SelectRow(0);
            }
        }
    }

    private void OpenRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count)
        {
            return;
        }

        Open(_rows[rowIndex].Element);
    }

    private void Open(CylinderElement? element)
    {
        if (element is null)
        {
            _hint.Text = "该行没有可操作的气缸实例。";
            return;
        }

        var services = _services;

        if (services is null)
        {
            return;
        }

        using var page = services.GetRequiredService<CylinderOperationPage>();
        page.Bind(element);

        var owner = FindTopLevelOwner(this);

        if (owner is null)
        {
            page.ShowDialog();
        }
        else
        {
            page.ShowDialog(owner);
        }
    }

    /// <summary>找到真正的顶层窗体（页面可能嵌在 TopLevel=false 的宿主页里）。</summary>
    private static Form? FindTopLevelOwner(Control control)
    {
        for (var current = control; current is not null; current = current.Parent)
        {
            if (current is Form { TopLevel: true } form)
            {
                return form;
            }
        }

        return null;
    }

    private static Row ToRow(CylinderSnapshot cylinder, CylinderElement? element) => new(
        cylinder.Name,
        StateText(cylinder.State),
        cylinder.Status.PositionText,
        cylinder.Status.ExtendedSensor ? "ON" : "OFF",
        cylinder.Status.RetractedSensor ? "ON" : "OFF",
        cylinder.Status.IsFault ? cylinder.Status.FaultCode.ToString() : "-",
        cylinder.CapturedAt.ToLocalTime().ToString("HH:mm:ss.fff"),
        element);

    private static string StateText(ElementState state) => state switch
    {
        ElementState.Unknown => "未知",
        ElementState.Disabled => "已禁用",
        ElementState.Ready => "就绪",
        ElementState.Running => "动作中",
        ElementState.Warning => "警告",
        ElementState.Error => "故障",
        _ => state.ToString()
    };

    private static int Severity(ElementState state) => state switch
    {
        ElementState.Error => 0,
        ElementState.Warning => 1,
        ElementState.Unknown => 2,
        ElementState.Running => 3,
        ElementState.Ready => 4,
        ElementState.Disabled => 5,
        _ => 9
    };

    private static string FormatTime(DateTimeOffset? timestamp)
        => timestamp is { } value ? value.ToLocalTime().ToString("HH:mm:ss") : "—";

    private sealed record Row(
        string name,
        string state,
        string position,
        string extended,
        string retracted,
        string fault,
        string updated,
        CylinderElement? Element);
}
