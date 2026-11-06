using EquipmentMonitor.Core.Models;

// ⚠️ 分部类边界：控件字段与 InitializeComponent 只在 PasswordDialog.Designer.cs 中定义。
// 本文件不要再声明控件字段，也不要再写 InitializeComponent。

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 设置 / 重置 / 清除账号口令的对话框。
/// </summary>
/// <remarks>
/// <para><b>口令不落界面以外的任何地方</b>：本对话框只把明文交给用户管理页，
/// 由用户目录立即哈希；不写日志、不进审计、不进模型。</para>
/// <para><b>清除口令</b>：只有已设置口令的账号才能选，清除后回到“工位信任”模式（演示/巡检用）。</para>
/// </remarks>
public sealed partial class PasswordDialog : Form
{
    private readonly UserAccount? _account;

    /// <summary>设计器专用构造。</summary>
    public PasswordDialog()
    {
        InitializeComponent();

        _okButton.Click += (_, _) => Accept();
        _clearCheck.CheckedChanged += (_, _) => UpdateInputsEnabled();
    }

    /// <summary>构造对话框。</summary>
    /// <param name="account">要设置口令的账号。</param>
    public PasswordDialog(UserAccount account)
        : this()
    {
        ArgumentNullException.ThrowIfNull(account);

        _account = account;
        _accountLabel.Text = $"账号：{account.DisplayName}（{account.UserName}）";

        if (account.HasPassword)
        {
            Text = "重置口令";
            _titleLabel.Text = "重置口令";
            _clearCheck.Visible = true;
        }
        else
        {
            Text = "设置口令";
            _titleLabel.Text = "设置口令";
            _clearCheck.Visible = false;
            _hintLabel.Text = "该账号未设口令；设置后登录须输入口令。";
        }
    }

    /// <summary>新口令；选择“清除口令”时为 <c>null</c>。</summary>
    internal string? NewPassword { get; private set; }

    /// <summary>是否请求清除口令。</summary>
    internal bool ClearRequested { get; private set; }

    /// <summary>错误提示（测试用）。</summary>
    internal string ErrorText => _errorLabel.Text ?? string.Empty;

    /// <summary>填入新口令与确认口令（测试用）。</summary>
    internal void SetPasswordForTest(string password, string confirm)
    {
        _newPasswordBox.Text = password;
        _confirmPasswordBox.Text = confirm;
    }

    /// <summary>勾选/取消“清除口令”（测试用）。</summary>
    internal void SetClearForTest(bool clear) => _clearCheck.Checked = clear;

    /// <summary>执行校验但不关闭对话框（测试用）。</summary>
    internal bool ValidateForTest() => TryBuildResult();

    private void UpdateInputsEnabled()
    {
        var editing = !_clearCheck.Checked;

        _newPasswordBox.Enabled = editing;
        _confirmPasswordBox.Enabled = editing;

        if (!editing)
        {
            _newPasswordBox.Clear();
            _confirmPasswordBox.Clear();
        }
    }

    private void Accept()
    {
        if (!TryBuildResult())
        {
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TryBuildResult()
    {
        // 不能判 _clearCheck.Visible：控件未挂到已显示的窗体上时 WinForms 的 Visible 恒为 false。
        if (_account?.HasPassword == true && _clearCheck.Checked)
        {
            ClearRequested = true;
            NewPassword = null;
            _errorLabel.Text = string.Empty;
            return true;
        }

        var passwordCheck = PasswordRules.Validate(_newPasswordBox.Text);

        if (passwordCheck.IsFailure)
        {
            ShowError(passwordCheck.Error);
            return false;
        }

        if (!string.Equals(_newPasswordBox.Text, _confirmPasswordBox.Text, StringComparison.Ordinal))
        {
            ShowError("两次输入的口令不一致。");
            return false;
        }

        ClearRequested = false;
        NewPassword = _newPasswordBox.Text;
        _errorLabel.Text = string.Empty;
        return true;
    }

    private void ShowError(string text) => _errorLabel.Text = text;
}
