using System.ComponentModel;
using EquipmentMonitor.Core.Axis;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Desktop.Monitoring;
using EquipmentMonitor.Infrastructure.Axis;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 元件总览页：全部轴的实时状态一览；选中行后打开操作台弹窗（或双击行）。
/// 界面布局在 <c>ElementOverviewPage.Designer.cs</c>。
/// </summary>
/// <remarks>
/// <para><b>排序</b>：故障 / 警告 / 未知 置顶，操作工先看到有问题的轴。</para>
/// <para><b>刷新</b>：500ms 读快照；不订阅元件事件。</para>
/// </remarks>
public sealed partial class ElementOverviewPage : Form
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<ElementOverviewPage>();

    private readonly List<Row> _rows = [];

    private AxisSnapshotStore? _store;
    private IServiceProvider? _services;
    private System.Windows.Forms.Timer? _timer;

    /// <summary>上一帧的行内容签名，用于避免无变化时重绑表格（会丢滚动位置）。</summary>
    private string _signature = string.Empty;

    /// <summary>最近一次点击的行（0 基）；SelectedIndex 为 1 基，两者需要区分。</summary>
    private int _clickedIndex = -1;

    /// <summary>当前选中的元件（跨表格重绑保持）。</summary>
    private AxisElement? _selectedElement;

    /// <summary>工位过滤；<c>null</c> 表示显示全部（自动监控），手动操作页设置。</summary>
    internal int? StationFilter { get; set; }

    /// <summary>设计器专用构造：只初始化界面与列，不连接任何服务。</summary>
    public ElementOverviewPage()
    {
        InitializeComponent();
        AddColumns();
    }

    /// <summary>构造总览页。</summary>
    /// <param name="store">采集快照仓。</param>
    /// <param name="services">容器；用于解析操作台弹窗。</param>
    public ElementOverviewPage(AxisSnapshotStore store, IServiceProvider services)
        : this()
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(services);

        _store = store;
        _services = services;

        // AntdUI 的 CellClick 对表头也会触发（RowType=None，RowIndex 给 1），
        // 只处理数据单元格，否则会出现“点表头选中了第二行/什么都选不中”。
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

    /// <summary>当前渲染的行（测试用）：按界面顺序给出轴名与状态文本。</summary>
    internal IReadOnlyList<(string Axis, string State)> RenderedRows
        => _table.DataSource is BindingList<Row> rows
            ? rows.Select(row => (row.axis, row.state)).ToArray()
            : [];

    /// <summary>底部提示文本（测试用）。</summary>
    internal string HintText => _hint.Text ?? string.Empty;

    /// <summary>当前选中的元件（测试用）。</summary>
    internal AxisElement? SelectedElement()
    {
        if (_selectedElement is not null)
        {
            return _selectedElement;
        }

        var selected = _table.SelectedIndex;
        var index = selected > 0 ? selected - 1 : _clickedIndex;

        return index >= 0 && index < _rows.Count ? _rows[index].Element : null;
    }

    /// <summary>选中一行（生产路径由 <c>CellClick</c> 触发，测试直接调用）。</summary>
    internal void SelectRow(int index)
    {
        _clickedIndex = index;

        Logger.Debug("SelectRow index={Index} rows={Rows}", index, _rows.Count);

        if (index < 0 || index >= _rows.Count)
        {
            return;
        }

        _selectedElement = _rows[index].Element;

        // 显式同步表格选中态（1 基）：不能依赖“点一下 AntdUI 一定自动选中”。
        if (_table.SelectedIndex != index + 1)
        {
            _table.SelectedIndex = index + 1;
        }
    }

    /// <summary>
    /// 用行对象定位，而不是用 <c>RowIndex</c>。
    /// </summary>
    /// <remarks>
    /// AntdUI 的 <c>SelectedIndex</c> 是 1 基，<c>CellClick.RowIndex</c> 也带表头偏移；
    /// 直接把它当 0 基会“点第二行选不中/点第一行选第二行”。<c>Record</c> 是数据对象本身，最可靠。
    /// </remarks>
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

    /// <summary>用最新快照重绑表格。无变化时跳过，避免每 500ms 重置滚动位置。</summary>
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

        var rows = snapshot.Axes
            .Where(axis => StationFilter is null || axis.StationId == StationFilter)
            .OrderBy(axis => Severity(axis.State))
            .ThenBy(axis => axis.Name, StringComparer.Ordinal)
            .Select(axis => ToRow(axis, elements.GetValueOrDefault(axis.ElementId)))
            .ToList();

        ApplyRows(rows, snapshot);
    }

    /// <summary>打开选中轴的操作台（按钮/双击/测试共用）。</summary>
    internal void OpenSelected()
    {
        var element = SelectedElement();

        if (element is null)
        {
            _hint.Text = "先选中一行，再打开操作台。";
            return;
        }

        Open(element);
    }

    private void AddColumns()
    {
        _table.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;

        _table.Columns.Add(new AntdUI.Column("axis", "轴") { Width = "160" });
        _table.Columns.Add(new AntdUI.Column("state", "状态") { Width = "60" });
        _table.Columns.Add(new AntdUI.Column("position", "位置 (mm)") { Width = "90" });
        _table.Columns.Add(new AntdUI.Column("speed", "速度 (mm/s)") { Width = "90" });
        _table.Columns.Add(new AntdUI.Column("enabled", "使能") { Width = "50" });
        _table.Columns.Add(new AntdUI.Column("fault", "报警码") { Width = "60" });
    }

    private void ApplyRows(List<Row> rows, AxisMonitorSnapshot snapshot)
    {
        var signature = string.Join(
            '|',
            rows.Select(row => $"{row.axis}/{row.state}/{row.position}/{row.enabled}/{row.fault}/{row.updated}"));

        _hint.Text = snapshot.LastError is null
            ? $"最近刷新：{FormatTime(snapshot.RefreshedAt)}（{rows.Count} 根轴）"
            : $"采集异常：{snapshot.LastError}";
        _hint.ForeColor = snapshot.LastError is null ? SystemColors.GrayText : Color.Firebrick;

        _rows.Clear();
        _rows.AddRange(rows);

        if (signature != _signature)
        {
            _signature = signature;
            _table.DataSource = new BindingList<Row>(rows.ToList());

            // 重绑后恢复选中行：否则每次刷新都会把用户的选择清掉。
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

    private void Open(AxisElement? element)
    {
        if (element is null)
        {
            _hint.Text = "该行没有可操作的元件实例。";
            return;
        }

        var services = _services;

        if (services is null)
        {
            return;
        }

        using var page = services.GetRequiredService<ElementOperationPage>();
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

    /// <summary>
    /// 找到真正的顶层窗体。页面会被嵌到 <see cref="AutoMonitorPage"/> / <see cref="ManualOperationPage"/> 里，
    /// 这些页面本身也是 <c>TopLevel = false</c> 的 Form，<c>FindForm()</c> 会返回自己，
    /// 用它当 <c>ShowDialog</c> 的 owner 会导致弹窗不出现。
    /// </summary>
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

    private static Row ToRow(AxisSnapshot axis, AxisElement? element) => new(
        axis.Name,
        StateText(axis.State),
        axis.Status.ActualPosition.ToString("F3"),
        axis.Status.ActualVelocity.ToString("F3"),
        axis.Status.IsEnabled ? "是" : "否",
        axis.Status.IsFault ? axis.Status.FaultCode.ToString() : "-",
        axis.CapturedAt.ToLocalTime().ToString("HH:mm:ss.fff"),
        element);

    private static string StateText(ElementState state) => state switch
    {
        ElementState.Unknown => "未知",
        ElementState.Disabled => "已禁用",
        ElementState.Ready => "就绪",
        ElementState.Running => "运行中",
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
        string axis,
        string state,
        string position,
        string speed,
        string enabled,
        string fault,
        string updated,
        AxisElement? Element);
}
