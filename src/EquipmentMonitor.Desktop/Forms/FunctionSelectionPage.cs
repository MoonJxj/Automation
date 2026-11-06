using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Events;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.FunctionSelection;

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 功能选择页：把现场任务（工位屏蔽、换型、维护等）做成卡片入口。
/// </summary>
/// <remarks>
/// <para><b>数据驱动</b>：卡片来自 <see cref="FunctionCatalog"/>，新增入口只改目录；
/// 页面本身不认识具体功能，只在 <see cref="Invoke"/> 里按 <see cref="FunctionEntry.Key"/> 分发。</para>
/// <para><b>权限只决定外观</b>：无权限的卡片置灰并给出提示，真正的拦截在功能服务入口
/// （<c>RequirePermission</c>）。UI 隐藏不等于权限控制。</para>
/// <para><b>未接入不装作可用</b>：目录里标 <see cref="FunctionAvailability.Planned"/> 的入口
/// 只显示说明，不打开空窗口。</para>
/// <para><b>会话变化</b>：切换用户后权限可能变化，页面重建卡片；事件可能来自后台线程，
/// 有句柄时切回 UI 线程。</para>
/// </remarks>
public sealed partial class FunctionSelectionPage : Form
{
    private const int CardWidth = 300;
    private const int CardHeight = 150;

    private static readonly Font CardTitleFont = new("Microsoft YaHei UI", 12F, FontStyle.Bold);
    private static readonly Font CardCaptionFont = new("Microsoft YaHei UI", 8.5F);

    private readonly IPermissionService? _permissions;
    private readonly IStationBypassService? _stationBypass;
    private readonly ISessionProvider? _session;

    /// <summary>设计器专用构造：只初始化界面。</summary>
    public FunctionSelectionPage()
    {
        InitializeComponent();
    }

    /// <summary>构造功能选择页。</summary>
    /// <param name="permissions">权限服务，决定卡片可用性。</param>
    /// <param name="stationBypass">工位屏蔽服务（示例功能）。</param>
    /// <param name="session">当前会话；变化时重建卡片，测试可不传。</param>
    public FunctionSelectionPage(
        IPermissionService permissions,
        IStationBypassService stationBypass,
        ISessionProvider? session = null)
        : this()
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(stationBypass);

        _permissions = permissions;
        _stationBypass = stationBypass;
        _session = session;

        if (session is not null)
        {
            session.Changed += OnSessionChanged;
        }

        BuildFunctionCards();
        SetStatus("请选择功能；标为“待接入”的入口尚未连接设备。");
    }

    /// <summary>功能目录（测试用）。</summary>
    internal IReadOnlyList<FunctionEntry> Entries => FunctionCatalog.All;

    /// <summary>已生成的卡片数量（测试用）。</summary>
    internal int CardCount => _functionGrid.Controls.Count;

    /// <summary>底部状态文本（测试用）。</summary>
    internal string StatusText => _statusLabel.Text ?? string.Empty;

    /// <summary>按标识触发一次入口（测试用；生产路径为点击卡片）。</summary>
    internal void InvokeForTest(string key)
    {
        var entry = FunctionCatalog.Find(key);

        if (entry is not null)
        {
            Invoke(entry);
        }
    }

    /// <inheritdoc />
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_session is not null)
        {
            _session.Changed -= OnSessionChanged;
        }

        base.OnFormClosed(e);
    }

    private void OnSessionChanged(object? sender, SessionChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired && IsHandleCreated)
        {
            BeginInvoke((Action)BuildFunctionCards);
            return;
        }

        BuildFunctionCards();
    }

    private void BuildFunctionCards()
    {
        _functionGrid.SuspendLayout();
        _functionGrid.Controls.Clear();

        foreach (var entry in FunctionCatalog.All)
        {
            _functionGrid.Controls.Add(CreateCard(entry));
        }

        _functionGrid.ResumeLayout();
    }

    private Control CreateCard(FunctionEntry entry)
    {
        var permissions = _permissions;
        var allowed = permissions is not null && permissions.HasPermission(entry.Required);
        var ready = entry.Availability == FunctionAvailability.Ready;
        var baseColor = allowed ? Color.White : Color.FromArgb(243, 244, 246);

        var card = new Panel
        {
            Size = new Size(CardWidth, CardHeight),
            Margin = new Padding(8),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = baseColor,
            Cursor = Cursors.Hand,
            Tag = entry
        };

        var title = new Label
        {
            Text = entry.Title,
            Font = CardTitleFont,
            ForeColor = allowed ? Color.FromArgb(32, 32, 32) : Color.Gray,
            BackColor = Color.Transparent,
            Bounds = new Rectangle(14, 12, 190, 30)
        };

        var status = new Label
        {
            Text = !ready ? "待接入" : allowed ? "可用" : "无权限",
            Font = CardCaptionFont,
            ForeColor = !ready ? Color.Gray : allowed ? Color.SeaGreen : Color.DarkOrange,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleRight,
            Bounds = new Rectangle(204, 14, 80, 22)
        };

        var summary = new Label
        {
            Text = entry.Summary,
            ForeColor = Color.DimGray,
            BackColor = Color.Transparent,
            AutoEllipsis = true,
            Bounds = new Rectangle(14, 50, 272, 58)
        };

        var permission = new Label
        {
            Text = $"需要：{PermissionCatalog.DisplayName(entry.Required)}",
            Font = CardCaptionFont,
            ForeColor = allowed ? Color.Silver : Color.DarkOrange,
            BackColor = Color.Transparent,
            Bounds = new Rectangle(14, 116, 272, 20)
        };

        card.Controls.Add(title);
        card.Controls.Add(status);
        card.Controls.Add(summary);
        card.Controls.Add(permission);

        void Open(object? sender, EventArgs e) => Invoke(entry);

        card.Click += Open;

        foreach (Control child in card.Controls)
        {
            child.Click += Open;
        }

        void Enter(object? sender, EventArgs e)
            => card.BackColor = allowed ? Color.FromArgb(235, 243, 255) : Color.FromArgb(232, 233, 236);

        void Leave(object? sender, EventArgs e) => card.BackColor = baseColor;

        card.MouseEnter += Enter;
        card.MouseLeave += Leave;

        foreach (Control child in card.Controls)
        {
            child.MouseEnter += Enter;
            child.MouseLeave += Leave;
        }

        return card;
    }

    private void Invoke(FunctionEntry entry)
    {
        if (entry.Availability != FunctionAvailability.Ready)
        {
            SetStatus($"「{entry.Title}」尚未接入：{entry.Summary}");
            return;
        }

        var permissions = _permissions;

        if (permissions is null || !permissions.HasPermission(entry.Required))
        {
            SetStatus(
                $"当前账号缺少「{PermissionCatalog.DisplayName(entry.Required)}」权限，无法打开「{entry.Title}」。");
            return;
        }

        switch (entry.Key)
        {
            case FunctionCatalog.StationBypassKey:
                OpenStationBypass();
                break;

            case FunctionCatalog.HelpKey:
                using (var dialog = new OperationHelpDialog())
                {
                    dialog.ShowDialog(this);
                }

                SetStatus("已关闭操作帮助。");
                break;

            default:
                SetStatus($"「{entry.Title}」入口已就绪，但处理程序尚未注册。");
                break;
        }
    }

    private void OpenStationBypass()
    {
        var stationBypass = _stationBypass;
        var permissions = _permissions;

        if (stationBypass is null || permissions is null)
        {
            SetStatus("工位屏蔽服务未注册。", isError: true);
            return;
        }

        using var dialog = new StationBypassDialog(stationBypass, permissions);
        dialog.ShowDialog(this);

        SetStatus(dialog.SaveSummary ?? "工位屏蔽未修改。");
    }

    private void SetStatus(string text, bool isError = false)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = isError ? Color.Firebrick : SystemColors.ControlText;
    }
}
