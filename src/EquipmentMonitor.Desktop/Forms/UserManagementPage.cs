using System.ComponentModel;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

// ⚠️ 分部类边界：控件字段与 InitializeComponent 只在 UserManagementPage.Designer.cs 中定义。
// 本文件不要再声明控件字段，也不要再写 InitializeComponent。

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 用户管理页：账号列表 + 新建/编辑/启停/口令/删除。布局在 <c>UserManagementPage.Designer.cs</c>。
/// </summary>
/// <remarks>
/// <para><b>三层防护</b>：导航项需要 <see cref="Permission.UserManage"/>（第 1 层）；
/// 本页按钮按权限与选中行启用（第 2 层，可绕过）；真正的校验在 <see cref="IUserDirectory"/>
/// 的写方法里（第 3 层，<c>RequirePermission</c>）。</para>
/// <para><b>不能把自己锁在门外</b>：当前登录账号不能停用/删除；最后一个启用中的管理员
/// 不能被停用/删除/降级 —— 这两条在用户目录里强制，界面只是提前禁用按钮。</para>
/// <para><b>变更在下一次登录生效</b>：改角色不会踢掉已登录的会话；这是有意的，
/// 避免管理员改权限时把现场正在操作的人即时降权。</para>
/// </remarks>
public sealed partial class UserManagementPage : Form
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<UserManagementPage>();

    private readonly List<UserRow> _rows = [];

    private IUserDirectory? _directory;
    private IPermissionService? _permissions;
    private ISessionProvider? _session;
    private UserRow? _selected;
    private bool _busy;

    /// <summary>设计器专用构造：只初始化界面与列，不连接任何服务。</summary>
    public UserManagementPage()
    {
        InitializeComponent();
        AddColumns();
    }

    /// <summary>构造用户管理页。</summary>
    /// <param name="directory">用户目录。</param>
    /// <param name="permissions">权限服务（只用于按钮显隐）。</param>
    /// <param name="session">当前会话（保护“不能停用/删除自己”）。</param>
    public UserManagementPage(
        IUserDirectory directory,
        IPermissionService permissions,
        ISessionProvider session)
        : this()
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(session);

        _directory = directory;
        _permissions = permissions;
        _session = session;

        _table.CellClick += (_, e) =>
        {
            if (e.Record is UserRow row)
            {
                SelectRow(row);
            }
        };

        _table.CellDoubleClick += (_, e) =>
        {
            if (e.Record is UserRow row)
            {
                SelectRow(row);
                _ = RunAsync(EditAsync);
            }
        };

        _createButton.Click += (_, _) => _ = RunAsync(CreateAsync);
        _editButton.Click += (_, _) => _ = RunAsync(EditAsync);
        _toggleButton.Click += (_, _) => _ = RunAsync(ToggleEnabledAsync);
        _passwordButton.Click += (_, _) => _ = RunAsync(ChangePasswordAsync);
        _deleteButton.Click += (_, _) => _ = RunAsync(DeleteAsync);
        _refreshButton.Click += (_, _) => _ = RunAsync(RefreshAsync);

        // 首次可见之前就按权限把按钮置灰：不能等 RefreshAsync 才收口。
        UpdateToolbar();

        VisibleChanged += (_, _) =>
        {
            if (Visible)
            {
                _ = RunAsync(RefreshAsync);
            }
        };
    }

    /// <summary>当前角色是否具备用户管理权限（测试用）。</summary>
    internal bool ActionsAllowed => _permissions?.HasPermission(Permission.UserManage) ?? false;

    /// <summary>“新建用户”按钮是否可用（测试用）。</summary>
    internal bool CreateButtonEnabled => _createButton.Enabled;

    /// <summary>“启用/停用”按钮是否可用（测试用）。</summary>
    internal bool CanToggleSelected => _toggleButton.Enabled;

    /// <summary>“删除”按钮是否可用（测试用）。</summary>
    internal bool CanDeleteSelected => _deleteButton.Enabled;

    /// <summary>表格行数（测试用）。</summary>
    internal int RowCount => _rows.Count;

    /// <summary>渲染的行（测试用）：账号、角色、状态。</summary>
    internal IReadOnlyList<(string User, string Role, string Status)> RenderedRows
        => _rows.Select(row => (row.user, row.role, row.status)).ToArray();

    /// <summary>提示文本（测试用）。</summary>
    internal string HintText => _hint.Text ?? string.Empty;

    /// <summary>当前选中行（测试用）。</summary>
    internal UserRow? SelectedRow() => _selected;

    /// <summary>选中指定账号（测试用）。</summary>
    internal void SelectUserForTest(string userName) => SelectUser(userName);

    private void SelectUser(string userName)
    {
        var row = _rows.FirstOrDefault(item =>
            string.Equals(item.Account.UserName, userName, StringComparison.OrdinalIgnoreCase));

        if (row is not null)
        {
            SelectRow(row);
        }
    }

    /// <summary>刷新列表（测试用；生产路径由可见性变化触发）。</summary>
    internal async Task RefreshForTestAsync() => await RefreshAsync().ConfigureAwait(true);

    /// <summary>
    /// 统一的异步入口：吞掉界面不能处理的异常并给出提示，避免 async void 把异常抛进消息循环。
    /// </summary>
    private async Task RunAsync(Func<Task> action)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        UpdateToolbar();

        try
        {
            await action().ConfigureAwait(true);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.Warning(ex, "用户管理操作被权限拒绝。");
            ShowHint($"权限不足：{ex.Message}", isError: true);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "用户管理操作失败。");
            ShowHint("操作失败，请查看日志。", isError: true);
        }
        finally
        {
            _busy = false;
            UpdateToolbar();
        }
    }

    private async Task RefreshAsync()
    {
        var directory = _directory;

        if (directory is null)
        {
            return;
        }

        var result = await directory.ListAsync().ConfigureAwait(true);

        if (result.IsFailure)
        {
            ShowHint(result.Error, isError: true);
            return;
        }

        ApplyRows(result.Value.Select(ToRow).ToList());
    }

    private void ApplyRows(List<UserRow> rows)
    {
        var selectedUserName = _selected?.Account.UserName;

        _rows.Clear();
        _rows.AddRange(rows);

        _table.DataSource = new BindingList<UserRow>(rows.ToList());

        _selected = null;

        if (selectedUserName is not null)
        {
            var previous = _rows.FirstOrDefault(row =>
                string.Equals(row.Account.UserName, selectedUserName, StringComparison.OrdinalIgnoreCase));

            if (previous is not null)
            {
                SelectRow(previous);
            }
        }

        if (_selected is null && _rows.Count > 0)
        {
            SelectRow(_rows[0]);
        }

        if (_selected is null)
        {
            _table.SelectedIndex = 0;
        }

        var currentUserName = _session?.Current?.UserName;
        var selfNote = currentUserName is null
            ? string.Empty
            : $"；当前登录：{currentUserName}（不能停用/删除自己）";

        ShowHint($"共 {rows.Count} 个账号{selfNote}。");
        UpdateToolbar();
    }

    private void SelectRow(UserRow row)
    {
        _selected = row;

        var index = _rows.FindIndex(item => ReferenceEquals(item, row));

        if (index >= 0 && _table.SelectedIndex != index + 1)
        {
            _table.SelectedIndex = index + 1;
        }

        UpdateToolbar();
    }

    private void UpdateToolbar()
    {
        var allowed = ActionsAllowed && !_busy;
        var selected = _selected;
        var currentUserName = _session?.Current?.UserName;

        var isSelf = selected is not null &&
                     currentUserName is not null &&
                     string.Equals(selected.Account.UserName, currentUserName, StringComparison.OrdinalIgnoreCase);

        _createButton.Enabled = allowed;
        _refreshButton.Enabled = allowed;
        _editButton.Enabled = allowed && selected is not null;
        _passwordButton.Enabled = allowed && selected is not null;
        _toggleButton.Enabled = allowed && selected is not null && !isSelf;
        _deleteButton.Enabled = allowed && selected is not null && !isSelf;

        _toggleButton.Text = selected is { Account.IsEnabled: true } ? "停用账号" : "启用账号";
        _passwordButton.Text = selected is { Account.HasPassword: true } ? "重置口令" : "设置口令";
    }

    private async Task CreateAsync()
    {
        var directory = _directory;

        if (directory is null)
        {
            return;
        }

        using var dialog = new UserEditDialog(null);

        if (dialog.ShowDialog(FindTopLevelOwner()) != DialogResult.OK || dialog.CreateRequest is not { } request)
        {
            return;
        }

        var result = await directory.CreateAsync(request).ConfigureAwait(true);

        if (result.IsFailure)
        {
            ShowHint(result.Error, isError: true);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        SelectUser(result.Value.UserName);
        ShowHint($"已新建账号「{result.Value.UserName}」（{UserRoleCatalog.DisplayName(result.Value.Role)}）。");
    }

    private async Task EditAsync()
    {
        var selected = _selected;
        var directory = _directory;

        if (selected is null || directory is null)
        {
            return;
        }

        using var dialog = new UserEditDialog(selected.Account);

        if (dialog.ShowDialog(FindTopLevelOwner()) != DialogResult.OK || dialog.UpdateRequest is not { } request)
        {
            return;
        }

        var result = await directory.UpdateAsync(request).ConfigureAwait(true);

        if (result.IsFailure)
        {
            ShowHint(result.Error, isError: true);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        SelectUser(request.UserName);
        ShowHint($"已保存「{request.UserName}」（{UserRoleCatalog.DisplayName(request.Role)}）。下次登录生效。");
    }

    private async Task ToggleEnabledAsync()
    {
        var selected = _selected;
        var directory = _directory;

        if (selected is null || directory is null)
        {
            return;
        }

        var enable = !selected.Account.IsEnabled;
        var action = enable ? "启用" : "停用";
        var message = enable
            ? $"启用账号「{selected.Account.UserName}」？启用后该账号可以登录。"
            : $"停用账号「{selected.Account.UserName}」？停用后该账号不能登录。";

        if (MessageBox.Show(
                FindTopLevelOwner(),
                message,
                $"确认{action}",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        var result = await directory
            .SetEnabledAsync(selected.Account.UserName, enable)
            .ConfigureAwait(true);

        if (result.IsFailure)
        {
            ShowHint(result.Error, isError: true);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        SelectUser(selected.Account.UserName);
        ShowHint($"已{action}账号「{selected.Account.UserName}」。");
    }

    private async Task ChangePasswordAsync()
    {
        var selected = _selected;
        var directory = _directory;

        if (selected is null || directory is null)
        {
            return;
        }

        using var dialog = new PasswordDialog(selected.Account);

        if (dialog.ShowDialog(FindTopLevelOwner()) != DialogResult.OK)
        {
            return;
        }

        var result = dialog.ClearRequested
            ? await directory.ClearPasswordAsync(selected.Account.UserName).ConfigureAwait(true)
            : await directory
                .SetPasswordAsync(selected.Account.UserName, dialog.NewPassword!)
                .ConfigureAwait(true);

        if (result.IsFailure)
        {
            ShowHint(result.Error, isError: true);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        SelectUser(selected.Account.UserName);
        ShowHint(dialog.ClearRequested
            ? $"已清除「{selected.Account.UserName}」的口令，该账号回到工位信任模式。"
            : $"已设置「{selected.Account.UserName}」的口令。");
    }

    private async Task DeleteAsync()
    {
        var selected = _selected;
        var directory = _directory;

        if (selected is null || directory is null)
        {
            return;
        }

        if (MessageBox.Show(
                FindTopLevelOwner(),
                $"删除账号「{selected.Account.UserName}」？该操作不可撤销，审计记录会保留。",
                "确认删除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        var result = await directory.DeleteAsync(selected.Account.UserName).ConfigureAwait(true);

        if (result.IsFailure)
        {
            ShowHint(result.Error, isError: true);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        ShowHint($"已删除账号「{selected.Account.UserName}」。");
    }

    private void AddColumns()
    {
        _table.AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;

        _table.Columns.Add(new AntdUI.Column("user", "账号") { Width = "110" });
        _table.Columns.Add(new AntdUI.Column("display", "显示名") { Width = "110" });
        _table.Columns.Add(new AntdUI.Column("role", "角色") { Width = "100" });
        _table.Columns.Add(new AntdUI.Column("password", "口令") { Width = "70" });
        _table.Columns.Add(new AntdUI.Column("status", "状态") { Width = "60" });
        _table.Columns.Add(new AntdUI.Column("created", "创建时间") { Width = "140" });
    }

    private void ShowHint(string text, bool isError = false)
    {
        _hint.Text = text;
        _hint.ForeColor = isError ? Color.Firebrick : SystemColors.GrayText;
    }

    /// <summary>
    /// 找到真正的顶层窗体：本页会被嵌进主壳，<c>FindForm()</c> 返回的是自己，
    /// 用它当 <c>ShowDialog</c> 的 owner 会导致弹窗不出现。
    /// </summary>
    private Form? FindTopLevelOwner()
    {
        for (var current = (Control)this; current is not null; current = current.Parent)
        {
            if (current is Form { TopLevel: true } form)
            {
                return form;
            }
        }

        return null;
    }

    private static UserRow ToRow(UserAccount account) => new(
        account.UserName,
        account.DisplayName,
        UserRoleCatalog.DisplayName(account.Role),
        account.HasPassword ? "已设置" : "未设置",
        account.IsEnabled ? "启用" : "停用",
        account.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
        account);

    /// <summary>表格行：列名与 AntdUI 绑定键一致（小写）。</summary>
    internal sealed record UserRow(
        string user,
        string display,
        string role,
        string password,
        string status,
        string created,
        UserAccount Account);
}
