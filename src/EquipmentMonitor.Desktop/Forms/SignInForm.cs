using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Models;

// ⚠️ 分部类边界：控件字段与 InitializeComponent 只在 SignInForm.Designer.cs 中定义。
// 本文件不要再声明控件字段，也不要再写 InitializeComponent。

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 登录窗体：从本地用户目录选择账号登录（账号可要求口令）。布局在 <c>SignInForm.Designer.cs</c>。
/// </summary>
/// <remarks>
/// <para><b>角色来自用户目录</b>：这里不再让使用者自选角色 —— 角色由管理员在“用户管理”里分配，
/// 登录窗体只展示所选账号的角色与职责说明。</para>
/// <para><b>口令可选</b>：未设置口令的账号直接登录（工位信任）；设置后必须输入。
/// 校验发生在认证层（用户目录内部），本窗体只收集输入。</para>
/// <para><b>窗体不写业务</b>：收集输入 → 调登录门面 → 显示失败原因；会话由门面写入。</para>
/// </remarks>
public sealed partial class SignInForm : Form
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<SignInForm>();

    private IAuthenticationService? _authentication;
    private IUserDirectory? _directory;
    private bool _busy;

    /// <summary>设计器专用构造：只初始化界面，不连接任何服务。</summary>
    public SignInForm()
    {
        InitializeComponent();

        _signInButton.Click += async (_, _) => await SignInAsync().ConfigureAwait(true);
        _accountCombo.SelectedIndexChanged += (_, _) => UpdateAccountDetails();
    }

    /// <summary>构造登录窗体。</summary>
    /// <param name="authentication">登录门面。</param>
    /// <param name="directory">用户目录：列出可登录账号。</param>
    public SignInForm(IAuthenticationService authentication, IUserDirectory directory)
        : this()
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(directory);

        _authentication = authentication;
        _directory = directory;

        Load += async (_, _) => await LoadAccountsAsync().ConfigureAwait(true);
    }

    /// <summary>可登录账号数量（测试用）。</summary>
    internal int AccountCount => _accountCombo.Items.Count;

    /// <summary>角色说明文本（测试用）。</summary>
    internal string RoleText => $"{_roleLabel.Text} {_roleDescriptionLabel.Text}";

    /// <summary>口令输入是否可用（测试用）。</summary>
    internal bool IsPasswordRequired => _passwordBox.Enabled;

    /// <summary>错误提示（测试用）。</summary>
    internal string ErrorText => _errorLabel.Text ?? string.Empty;

    /// <summary>选择账号（测试用）。</summary>
    internal void SelectAccountForTest(string userName)
    {
        for (var index = 0; index < _accountCombo.Items.Count; index++)
        {
            if (_accountCombo.Items[index] is AccountItem item &&
                string.Equals(item.Account.UserName, userName, StringComparison.OrdinalIgnoreCase))
            {
                _accountCombo.SelectedIndex = index;
                return;
            }
        }
    }

    /// <summary>加载账号（测试用；生产路径由 <c>Load</c> 事件触发）。</summary>
    internal async Task LoadAccountsForTestAsync()
    {
        await LoadAccountsAsync().ConfigureAwait(true);
    }

    private async Task LoadAccountsAsync()
    {
        var directory = _directory;

        if (directory is null)
        {
            return;
        }

        try
        {
            var result = await directory.ListAsync().ConfigureAwait(true);

            if (result.IsFailure)
            {
                ShowError($"读取账号失败：{result.Error}");
                return;
            }

            var accounts = result.Value.Where(account => account.IsEnabled).ToArray();

            _accountCombo.Items.Clear();

            foreach (var account in accounts)
            {
                _accountCombo.Items.Add(new AccountItem(account));
            }

            if (accounts.Length == 0)
            {
                UpdateAccountDetails();
                ShowError("没有启用中的账号，请联系管理员。");
                return;
            }

            _accountCombo.SelectedIndex = 0;
            UpdateAccountDetails();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "加载登录账号失败。");
            ShowError("读取账号失败，请查看日志。");
        }
    }

    private void UpdateAccountDetails()
    {
        if (_accountCombo.SelectedItem is not AccountItem item)
        {
            _roleLabel.Text = "角色：—";
            _roleDescriptionLabel.Text = string.Empty;
            _passwordBox.Enabled = false;
            _passwordBox.Clear();
            _hintLabel.Text = "没有可登录的账号。";
            _signInButton.Enabled = false;
            return;
        }

        var account = item.Account;

        _roleLabel.Text = $"角色：{UserRoleCatalog.DisplayName(account.Role)}";
        _roleDescriptionLabel.Text = UserRoleCatalog.Describe(account.Role);
        _passwordBox.Enabled = account.HasPassword;
        _passwordBox.Clear();
        _signInButton.Enabled = true;

        _hintLabel.Text = account.HasPassword
            ? "该账号已设置口令，请输入口令后登录。"
            : "该账号未设置口令（工位信任），直接登录。";
    }

    private async Task SignInAsync()
    {
        var authentication = _authentication;

        if (authentication is null || _busy)
        {
            return;
        }

        if (_accountCombo.SelectedItem is not AccountItem item)
        {
            ShowError("请选择登录账号。");
            return;
        }

        _busy = true;
        _errorLabel.Text = string.Empty;
        _signInButton.Enabled = false;
        _accountCombo.Enabled = false;

        try
        {
            var request = new SignInRequest
            {
                UserName = item.Account.UserName,
                Credential = item.Account.HasPassword ? _passwordBox.Text : null
            };

            var result = await authentication.SignInAsync(request).ConfigureAwait(true);

            if (result.IsFailure)
            {
                ShowError(result.Error);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            // 登录异常不得把未处理异常抛进消息循环：界面停在登录页，日志留完整堆栈。
            Logger.Error(ex, "登录过程发生异常。");
            ShowError("登录过程发生异常，请查看日志。");
        }
        finally
        {
            _busy = false;

            if (!IsDisposed)
            {
                _signInButton.Enabled = true;
                _accountCombo.Enabled = true;
            }
        }
    }

    private void ShowError(string text)
    {
        _errorLabel.Text = text;
        _signInButton.Enabled = true;
    }

    /// <summary>下拉项：显示“显示名（账号）”，取值保留账号快照。</summary>
    private sealed record AccountItem(UserAccount Account)
    {
        public override string ToString() => $"{Account.DisplayName}（{Account.UserName}）";
    }
}
