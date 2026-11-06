namespace EquipmentMonitor.Desktop.Forms;

partial class PasswordDialog
{
    private System.Windows.Forms.Label _titleLabel = null!;
    private System.Windows.Forms.Label _accountLabel = null!;
    private System.Windows.Forms.Label _hintLabel = null!;
    private System.Windows.Forms.Label _newPasswordLabel = null!;
    private System.Windows.Forms.TextBox _newPasswordBox = null!;
    private System.Windows.Forms.Label _confirmPasswordLabel = null!;
    private System.Windows.Forms.TextBox _confirmPasswordBox = null!;
    private System.Windows.Forms.CheckBox _clearCheck = null!;
    private System.Windows.Forms.Label _errorLabel = null!;
    private System.Windows.Forms.Button _okButton = null!;
    private System.Windows.Forms.Button _cancelButton = null!;

    /// <summary>设计器生成的界面初始化。运行期由构造函数调用。</summary>
    private void InitializeComponent()
    {
        this._titleLabel = new System.Windows.Forms.Label();
        this._accountLabel = new System.Windows.Forms.Label();
        this._hintLabel = new System.Windows.Forms.Label();
        this._newPasswordLabel = new System.Windows.Forms.Label();
        this._newPasswordBox = new System.Windows.Forms.TextBox();
        this._confirmPasswordLabel = new System.Windows.Forms.Label();
        this._confirmPasswordBox = new System.Windows.Forms.TextBox();
        this._clearCheck = new System.Windows.Forms.CheckBox();
        this._errorLabel = new System.Windows.Forms.Label();
        this._okButton = new System.Windows.Forms.Button();
        this._cancelButton = new System.Windows.Forms.Button();
        this.SuspendLayout();
        //
        // _titleLabel
        //
        this._titleLabel.Font = new System.Drawing.Font(
            "Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
        this._titleLabel.Location = new System.Drawing.Point(16, 14);
        this._titleLabel.Name = "_titleLabel";
        this._titleLabel.Size = new System.Drawing.Size(388, 28);
        this._titleLabel.TabIndex = 0;
        this._titleLabel.Text = "设置口令";
        //
        // _accountLabel
        //
        this._accountLabel.Location = new System.Drawing.Point(18, 46);
        this._accountLabel.Name = "_accountLabel";
        this._accountLabel.Size = new System.Drawing.Size(386, 24);
        this._accountLabel.TabIndex = 1;
        this._accountLabel.Text = "账号：—";
        //
        // _hintLabel
        //
        this._hintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        this._hintLabel.Location = new System.Drawing.Point(18, 72);
        this._hintLabel.Name = "_hintLabel";
        this._hintLabel.Size = new System.Drawing.Size(386, 32);
        this._hintLabel.TabIndex = 2;
        this._hintLabel.Text = "至少 8 个字符；设置后登录须输入口令。";
        //
        // _newPasswordLabel
        //
        this._newPasswordLabel.Location = new System.Drawing.Point(18, 110);
        this._newPasswordLabel.Name = "_newPasswordLabel";
        this._newPasswordLabel.Size = new System.Drawing.Size(86, 24);
        this._newPasswordLabel.TabIndex = 3;
        this._newPasswordLabel.Text = "新口令";
        this._newPasswordLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _newPasswordBox
        //
        this._newPasswordBox.Location = new System.Drawing.Point(104, 108);
        this._newPasswordBox.MaxLength = 128;
        this._newPasswordBox.Name = "_newPasswordBox";
        this._newPasswordBox.Size = new System.Drawing.Size(296, 25);
        this._newPasswordBox.TabIndex = 4;
        this._newPasswordBox.UseSystemPasswordChar = true;
        //
        // _confirmPasswordLabel
        //
        this._confirmPasswordLabel.Location = new System.Drawing.Point(18, 148);
        this._confirmPasswordLabel.Name = "_confirmPasswordLabel";
        this._confirmPasswordLabel.Size = new System.Drawing.Size(86, 24);
        this._confirmPasswordLabel.TabIndex = 5;
        this._confirmPasswordLabel.Text = "确认口令";
        this._confirmPasswordLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _confirmPasswordBox
        //
        this._confirmPasswordBox.Location = new System.Drawing.Point(104, 146);
        this._confirmPasswordBox.MaxLength = 128;
        this._confirmPasswordBox.Name = "_confirmPasswordBox";
        this._confirmPasswordBox.Size = new System.Drawing.Size(296, 25);
        this._confirmPasswordBox.TabIndex = 6;
        this._confirmPasswordBox.UseSystemPasswordChar = true;
        //
        // _clearCheck
        //
        this._clearCheck.Location = new System.Drawing.Point(104, 180);
        this._clearCheck.Name = "_clearCheck";
        this._clearCheck.Size = new System.Drawing.Size(296, 24);
        this._clearCheck.TabIndex = 7;
        this._clearCheck.Text = "清除口令（恢复为工位信任模式）";
        this._clearCheck.UseVisualStyleBackColor = true;
        this._clearCheck.Visible = false;
        //
        // _errorLabel
        //
        this._errorLabel.ForeColor = System.Drawing.Color.Firebrick;
        this._errorLabel.Location = new System.Drawing.Point(18, 210);
        this._errorLabel.Name = "_errorLabel";
        this._errorLabel.Size = new System.Drawing.Size(382, 36);
        this._errorLabel.TabIndex = 8;
        //
        // _okButton
        //
        this._okButton.Location = new System.Drawing.Point(188, 256);
        this._okButton.Name = "_okButton";
        this._okButton.Size = new System.Drawing.Size(100, 32);
        this._okButton.TabIndex = 9;
        this._okButton.Text = "保存";
        this._okButton.UseVisualStyleBackColor = true;
        //
        // _cancelButton
        //
        this._cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        this._cancelButton.Location = new System.Drawing.Point(300, 256);
        this._cancelButton.Name = "_cancelButton";
        this._cancelButton.Size = new System.Drawing.Size(100, 32);
        this._cancelButton.TabIndex = 10;
        this._cancelButton.Text = "取消";
        this._cancelButton.UseVisualStyleBackColor = true;
        //
        // PasswordDialog
        //
        this.AcceptButton = this._okButton;
        this.CancelButton = this._cancelButton;
        this.ClientSize = new System.Drawing.Size(420, 302);
        this.Controls.Add(this._titleLabel);
        this.Controls.Add(this._accountLabel);
        this.Controls.Add(this._hintLabel);
        this.Controls.Add(this._newPasswordLabel);
        this.Controls.Add(this._newPasswordBox);
        this.Controls.Add(this._confirmPasswordLabel);
        this.Controls.Add(this._confirmPasswordBox);
        this.Controls.Add(this._clearCheck);
        this.Controls.Add(this._errorLabel);
        this.Controls.Add(this._okButton);
        this.Controls.Add(this._cancelButton);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "PasswordDialog";
        this.ShowInTaskbar = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "设置口令";
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
