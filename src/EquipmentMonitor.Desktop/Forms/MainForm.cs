using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Events;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Monitoring;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 主窗体：导航容器 + 常驻状态栏。界面布局在 <c>MainForm.Designer.cs</c>（VS 设计器可解析）。
/// </summary>
/// <remarks>
/// <para><b>分部类约定</b>：布局在 <c>*.Designer.cs</c>，行为在本文件；
/// 无参构造只做 <see cref="InitializeComponent"/>，供 VS 设计器实例化。</para>
/// <para><b>导航按钮是运行期生成的</b>：导航项来自 <see cref="Navigation"/> 表（类型 + 标题 + 所需权限），
/// 新增页面只需在表里加一行；设计器里看到的空导航面板是预期的。</para>
/// <para><b>权限只决定显隐</b>：这里 <c>HasPermission</c> 只控制按钮可见性，
/// 真正的校验在各业务入口的 <c>RequirePermission</c>（UI 隐藏不等于权限控制）。</para>
/// <para><b>会话变化</b>：登录 / 切换用户 / 登出都会刷新状态栏与导航；
/// 事件可能来自后台线程，统一切回 UI 线程再改控件。</para>
/// </remarks>
public sealed partial class MainForm : Form
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<MainForm>();

    /// <summary>导航项：标题 + 页面类型 + 打开它所需的权限。新增页面在此追加。</summary>
    private static readonly (string Title, Type PageType, Permission Required)[] Navigation =
    [
        ("自动监控", typeof(AutoMonitorPage), Permission.MachineView),
        ("手动操作", typeof(ManualOperationPage), Permission.MachineView),
        ("功能选择", typeof(FunctionSelectionPage), Permission.MachineView),
        ("参数设置", typeof(ParameterSettingsPage), Permission.MachineConfig),
        ("报警信息", typeof(AlarmInfoPage), Permission.MachineView),
        ("用户管理", typeof(UserManagementPage), Permission.UserManage)
    ];

    private readonly Dictionary<Type, Form> _pages = [];
    private readonly Dictionary<Type, Button> _navButtons = [];
    private readonly System.Windows.Forms.Timer _statusTimer;

    private IServiceProvider? _services;
    private AxisSnapshotStore? _store;
    private CylinderSnapshotStore? _cylinderStore;
    private ISessionProvider? _session;
    private IAuthenticationService? _authentication;
    private IPermissionService? _permissions;
    private IAlarmService? _alarms;
    private Type? _currentPageType;

    /// <summary>设计器专用构造：只初始化界面，不连接任何服务。</summary>
    public MainForm()
    {
        InitializeComponent();

        _statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _statusTimer.Tick += (_, _) => RefreshStatus();

        Load += (_, _) =>
        {
            if (_services is null || _permissions is null)
            {
                return;
            }

            RefreshSessionView();
            ShowFirstAllowedPage();
            RefreshStatus();
            _statusTimer.Start();
        };

        FormClosed += (_, _) =>
        {
            _statusTimer.Dispose();

            if (_session is not null)
            {
                _session.Changed -= OnSessionChanged;
            }
        };
    }

    /// <summary>构造主窗体。</summary>
    /// <param name="services">容器，用于按导航项与“切换用户”解析窗体。</param>
    /// <param name="store">采集快照仓，状态栏从这里取数。</param>
    /// <param name="session">当前会话（状态栏显示用户与角色）。</param>
    /// <param name="authentication">登录门面（切换用户时先登出再登录）。</param>
    /// <param name="permissions">权限服务（只用于导航显隐）。</param>
    /// <param name="alarms">报警服务（状态栏显示报警计数）；测试可不传。</param>
    public MainForm(
        IServiceProvider services,
        AxisSnapshotStore store,
        ISessionProvider session,
        IAuthenticationService authentication,
        IPermissionService permissions,
        IAlarmService? alarms = null)
        : this()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(permissions);

        _services = services;
        _store = store;
        _cylinderStore = services.GetService<CylinderSnapshotStore>();
        _session = session;
        _authentication = authentication;
        _permissions = permissions;
        _alarms = alarms;

        _session.Changed += OnSessionChanged;
        _switchUserLabel.Click += async (_, _) => await SwitchUserAsync().ConfigureAwait(true);
        _alarmLabel.Click += (_, _) => ShowPage(typeof(AlarmInfoPage));

        BuildNavigationButtons();
        RefreshSessionView();
    }

    /// <summary>状态栏用户文本（测试用）。</summary>
    internal string UserText => _userLabel.Text ?? string.Empty;

    /// <summary>状态栏报警文本（测试用）。</summary>
    internal string AlarmText => _alarmLabel.Text ?? string.Empty;

    /// <summary>当前有权限显示的导航项数量（测试用）。</summary>
    /// <remarks>
    /// 不用 <c>Button.Visible</c> 统计：控件未挂到已显示的窗体上时，WinForms 的
    /// <c>Visible</c> 恒为 <c>false</c>（它表示“有效可见性”），会让测试误判。
    /// </remarks>
    internal int VisibleNavigationCount => Navigation.Count(item => IsPageAllowed(item.PageType));

    /// <summary>当前显示的页面类型（测试用）。</summary>
    internal Type? CurrentPageType => _currentPageType;

    /// <summary>刷新会话视图（测试用；生产路径由会话变更事件触发）。</summary>
    internal void RefreshSessionViewForTest() => RefreshSessionView();

    /// <summary>刷新状态栏（测试用；生产路径由定时器触发）。</summary>
    internal void RefreshStatusForTest() => RefreshStatus();

    private void BuildNavigationButtons()
    {
        foreach (var (title, pageType, _) in Navigation)
        {
            var button = new Button
            {
                Text = title,
                Width = 160,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Tag = pageType
            };

            button.FlatAppearance.BorderSize = 0;
            button.Click += (_, _) => ShowPage(pageType);

            _navPanel.Controls.Add(button);
            _navButtons[pageType] = button;
        }

        UpdateNavigationAvailability();
    }

    private void UpdateNavigationAvailability()
    {
        var permissions = _permissions;

        if (permissions is null)
        {
            return;
        }

        foreach (var (_, pageType, required) in Navigation)
        {
            if (_navButtons.TryGetValue(pageType, out var button))
            {
                button.Visible = permissions.HasPermission(required);
            }
        }

        _switchUserLabel.Visible = _session?.Current is not null;
    }

    private bool IsPageAllowed(Type pageType)
    {
        var permissions = _permissions;

        if (permissions is null)
        {
            return false;
        }

        foreach (var (_, type, required) in Navigation)
        {
            if (type == pageType)
            {
                return permissions.HasPermission(required);
            }
        }

        return false;
    }

    private void ShowFirstAllowedPage()
    {
        foreach (var (_, pageType, _) in Navigation)
        {
            if (IsPageAllowed(pageType))
            {
                ShowPage(pageType);
                return;
            }
        }

        // 一个页面都没权限时不假装还停在旧页面上。
        foreach (var page in _pages.Values)
        {
            page.Visible = false;
        }

        _currentPageType = null;
    }

    private void ShowPage(Type pageType)
    {
        var provider = _services;

        if (provider is null || !IsPageAllowed(pageType))
        {
            return;
        }

        if (!_pages.TryGetValue(pageType, out var page))
        {
            page = (Form)provider.GetRequiredService(pageType);
            page.TopLevel = false;
            page.FormBorderStyle = FormBorderStyle.None;
            page.Dock = DockStyle.Fill;

            _pages.Add(pageType, page);
            _contentPanel.Controls.Add(page);
        }

        foreach (var (type, form) in _pages)
        {
            form.Visible = type == pageType;
        }

        page.Visible = true;
        page.BringToFront();

        _currentPageType = pageType;

        foreach (var (type, button) in _navButtons)
        {
            button.BackColor = type == pageType ? Color.FromArgb(230, 235, 245) : Color.Transparent;
        }
    }

    private void RefreshSessionView()
    {
        var session = _session?.Current;

        _userLabel.Text = session is null
            ? "用户：未登录"
            : $"用户：{session.DisplayName}（{UserRoleCatalog.DisplayName(session.Role)}）";

        UpdateNavigationAvailability();

        if (_currentPageType is { } current && !IsPageAllowed(current))
        {
            ShowFirstAllowedPage();
        }
    }

    private void OnSessionChanged(object? sender, SessionChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        // 事件可能来自后台线程（将来的令牌刷新）；有窗口句柄时一律切回 UI 线程。
        if (InvokeRequired && IsHandleCreated)
        {
            BeginInvoke((Action)RefreshSessionView);
            return;
        }

        RefreshSessionView();
    }

    private async Task SwitchUserAsync()
    {
        var services = _services;
        var authentication = _authentication;

        if (services is null || authentication is null)
        {
            return;
        }

        try
        {
            await authentication.SignOutAsync("切换用户").ConfigureAwait(true);

            using var form = (SignInForm)services.GetRequiredService(typeof(SignInForm));

            if (form.ShowDialog(this) != DialogResult.OK)
            {
                Logger.Information("用户取消登录，程序退出。");
                Close();
                return;
            }

            RefreshSessionView();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "切换用户失败。");

            MessageBox.Show(
                this,
                $"切换用户失败：{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RefreshStatus()
    {
        var store = _store;

        if (store is null)
        {
            return;
        }

        var snapshot = store.Current;

        _axesLabel.Text = $"轴：{snapshot.Axes.Count}";
        _cylindersLabel.Text = $"气缸：{_cylinderStore?.Current.Cylinders.Count ?? 0}";
        _refreshLabel.Text = snapshot.RefreshedAt is { } at
            ? $"刷新：{at.ToLocalTime():HH:mm:ss}"
            : "刷新：—";

        if (snapshot.LastError is null)
        {
            _errorLabel.Text = "采集：正常";
            _errorLabel.ForeColor = SystemColors.ControlText;
        }
        else
        {
            _errorLabel.Text = $"采集：{snapshot.LastError}";
            _errorLabel.ForeColor = Color.Firebrick;
        }

        RefreshAlarmStatus();
    }

    private void RefreshAlarmStatus()
    {
        var alarms = _alarms;

        if (alarms is null)
        {
            _alarmLabel.Text = "报警：—";
            _alarmLabel.ForeColor = SystemColors.ControlText;
            return;
        }

        var snapshot = alarms.Snapshot;

        if (snapshot.Count == 0)
        {
            _alarmLabel.Text = "报警：无";
            _alarmLabel.ForeColor = SystemColors.ControlText;
            return;
        }

        _alarmLabel.Text = $"报警：{snapshot.Count}（未确认 {snapshot.UnacknowledgedCount}）";
        _alarmLabel.ForeColor = snapshot.HighestLevel switch
        {
            AlarmLevel.Critical => Color.DarkRed,
            AlarmLevel.Error => Color.Firebrick,
            AlarmLevel.Warning => Color.DarkOrange,
            _ => SystemColors.ControlText
        };
    }
}
