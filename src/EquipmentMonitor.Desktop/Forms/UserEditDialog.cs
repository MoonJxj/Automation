using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

// ⚠️ 分部类边界：控件字段与 InitializeComponent 只在 UserEditDialog.Designer.cs 中定义。
// 本文件不要再声明控件字段，也不要再写 InitializeComponent。

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 新建 / 编辑用户对话框：账号名、显示名、角色。口令与启停是独立入口。
/// </summary>
/// <remarks>
/// <para><b>账号名创建后不可改</b>：审计与历史都以账号名为键；改名等于换了一个人。</para>
/// <para><b>本对话框不碰服务</b>：只负责收集与校验，保存由调用方（用户管理页）执行。</para>
/// <para>校验失败只更新错误标签，不抛异常、不关闭对话框 —— 用户可以就地修正。</para>
/// </remarks>
public sealed partial class UserEditDialog : Form
{
    private readonly UserAccount? _existing;

    /// <summary>设计器专用构造。</summary>
    public UserEditDialog()
    {
        InitializeComponent();

        LoadRoles();

        _roleCombo.SelectedIndexChanged += (_, _) => UpdateRoleDescription();
        _okButton.Click += (_, _) => Accept();
    }

    /// <summary>构造对话框。</summary>
    /// <param name="existing">要编辑的账号；<c>null</c> 表示新建。</param>
    public UserEditDialog(UserAccount? existing)
        : this()
    {
        _existing = existing;

        if (existing is null)
        {
            Text = "新建用户";
            _titleLabel.Text = "新建用户";
            SelectRoleForTest(UserRole.Viewer);
            return;
        }

        Text = "编辑用户";
        _titleLabel.Text = $"编辑用户 — {existing.UserName}";
        _userNameBox.Text = existing.UserName;
        _userNameBox.ReadOnly = true;
        _displayBox.Text = existing.DisplayName;
        SelectRoleForTest(existing.Role);

        _hintLabel.Text = "账号名不可改；角色下次登录生效。";
    }

    /// <summary>新建请求；编辑模式下为 <c>null</c>。</summary>
    internal CreateUserRequest? CreateRequest => _existing is null
        ? new CreateUserRequest
        {
            UserName = _userNameBox.Text,
            DisplayName = _displayBox.Text,
            Role = SelectedRole()
        }
        : null;

    /// <summary>编辑请求；新建模式下为 <c>null</c>。</summary>
    internal UpdateUserRequest? UpdateRequest => _existing is null
        ? null
        : new UpdateUserRequest
        {
            UserName = _existing.UserName,
            DisplayName = _displayBox.Text,
            Role = SelectedRole()
        };

    /// <summary>错误提示（测试用）。</summary>
    internal string ErrorText => _errorLabel.Text ?? string.Empty;

    /// <summary>填入账号名（测试用）。</summary>
    internal void SetUserNameForTest(string userName) => _userNameBox.Text = userName;

    /// <summary>填入显示名（测试用）。</summary>
    internal void SetDisplayNameForTest(string displayName) => _displayBox.Text = displayName;

    /// <summary>选择角色（测试用）。</summary>
    internal void SelectRoleForTest(UserRole role)
    {
        for (var index = 0; index < _roleCombo.Items.Count; index++)
        {
            if (_roleCombo.Items[index] is RoleItem item && item.Role == role)
            {
                _roleCombo.SelectedIndex = index;
                return;
            }
        }
    }

    /// <summary>执行校验但不关闭对话框（测试用）。</summary>
    internal bool ValidateForTest() => ValidateInput();

    private void LoadRoles()
    {
        _roleCombo.Items.Clear();

        foreach (var role in UserRoleCatalog.All)
        {
            _roleCombo.Items.Add(new RoleItem(role));
        }

        if (_roleCombo.Items.Count > 0)
        {
            _roleCombo.SelectedIndex = 0;
        }

        UpdateRoleDescription();
    }

    private void UpdateRoleDescription()
    {
        _descriptionLabel.Text = _roleCombo.SelectedItem is RoleItem item
            ? UserRoleCatalog.Describe(item.Role)
            : string.Empty;
    }

    private UserRole SelectedRole()
        => _roleCombo.SelectedItem is RoleItem item ? item.Role : UserRole.Viewer;

    private void Accept()
    {
        if (!ValidateInput())
        {
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private bool ValidateInput()
    {
        if (_existing is null)
        {
            var nameCheck = UserRules.ValidateUserName(_userNameBox.Text);

            if (nameCheck.IsFailure)
            {
                ShowError(nameCheck.Error);
                return false;
            }
        }

        var displayCheck = UserRules.ValidateDisplayName(_displayBox.Text);

        if (displayCheck.IsFailure)
        {
            ShowError(displayCheck.Error);
            return false;
        }

        _errorLabel.Text = string.Empty;
        return true;
    }

    private void ShowError(string text) => _errorLabel.Text = text;

    private sealed record RoleItem(UserRole Role)
    {
        public override string ToString() => UserRoleCatalog.DisplayName(Role);
    }
}
