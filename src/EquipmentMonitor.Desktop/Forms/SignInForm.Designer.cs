namespace EquipmentMonitor.Desktop.Forms;

partial class SignInForm
{
    private System.Windows.Forms.Label _titleLabel = null!;
    private System.Windows.Forms.Label _hintLabel = null!;
    private System.Windows.Forms.Label _accountLabel = null!;
    private System.Windows.Forms.ComboBox _accountCombo = null!;
    private System.Windows.Forms.Label _roleLabel = null!;
    private System.Windows.Forms.Label _roleDescriptionLabel = null!;
    private System.Windows.Forms.Label _passwordLabel = null!;
    private System.Windows.Forms.TextBox _passwordBox = null!;
    private System.Windows.Forms.Label _errorLabel = null!;
    private System.Windows.Forms.Button _signInButton = null!;
    private System.Windows.Forms.Button _cancelButton = null!;

    /// <summary>设计器生成的界面初始化。运行期由构造函数调用。</summary>
    private void InitializeComponent()
    {
        this._titleLabel = new System.Windows.Forms.Label();
        this._hintLabel = new System.Windows.Forms.Label();
        this._accountLabel = new System.Windows.Forms.Label();
        this._accountCombo = new System.Windows.Forms.ComboBox();
        this._roleLabel = new System.Windows.Forms.Label();
        this._roleDescriptionLabel = new System.Windows.Forms.Label();
        this._passwordLabel = new System.Windows.Forms.Label();
        this._passwordBox = new System.Windows.Forms.TextBox();
        this._errorLabel = new System.Windows.Forms.Label();
        this._signInButton = new System.Windows.Forms.Button();
        this._cancelButton = new System.Windows.Forms.Button();
        this.SuspendLayout();
        //
        // _titleLabel
        //
        this._titleLabel.Font = new System.Drawing.Font(
            "Microsoft YaHei UI", 14F, System.Drawing.FontStyle.Bold);
        this._titleLabel.Location = new System.Drawing.Point(16, 14);
        this._titleLabel.Name = "_titleLabel";
        this._titleLabel.Size = new System.Drawing.Size(428, 32);
        this._titleLabel.TabIndex = 0;
        this._titleLabel.Text = "登录 — 设备监控上位机";
        //
        // _hintLabel
        //
        this._hintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        this._hintLabel.Location = new System.Drawing.Point(18, 50);
        this._hintLabel.Name = "_hintLabel";
        this._hintLabel.Size = new System.Drawing.Size(428, 36);
        this._hintLabel.TabIndex = 1;
        this._hintLabel.Text = "角色由管理员分配；未设口令即工位信任。";
        //
        // _accountLabel
        //
        this._accountLabel.Location = new System.Drawing.Point(18, 98);
        this._accountLabel.Name = "_accountLabel";
        this._accountLabel.Size = new System.Drawing.Size(80, 24);
        this._accountLabel.TabIndex = 2;
        this._accountLabel.Text = "账号";
        this._accountLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _accountCombo
        //
        this._accountCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this._accountCombo.Location = new System.Drawing.Point(104, 96);
        this._accountCombo.Name = "_accountCombo";
        this._accountCombo.Size = new System.Drawing.Size(340, 25);
        this._accountCombo.TabIndex = 3;
        //
        // _roleLabel
        //
        this._roleLabel.Location = new System.Drawing.Point(104, 128);
        this._roleLabel.Name = "_roleLabel";
        this._roleLabel.Size = new System.Drawing.Size(340, 24);
        this._roleLabel.TabIndex = 4;
        this._roleLabel.Text = "角色：—";
        //
        // _roleDescriptionLabel
        //
        this._roleDescriptionLabel.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
        this._roleDescriptionLabel.Location = new System.Drawing.Point(104, 154);
        this._roleDescriptionLabel.Name = "_roleDescriptionLabel";
        this._roleDescriptionLabel.Size = new System.Drawing.Size(340, 36);
        this._roleDescriptionLabel.TabIndex = 5;
        //
        // _passwordLabel
        //
        this._passwordLabel.Location = new System.Drawing.Point(18, 200);
        this._passwordLabel.Name = "_passwordLabel";
        this._passwordLabel.Size = new System.Drawing.Size(80, 24);
        this._passwordLabel.TabIndex = 6;
        this._passwordLabel.Text = "口令";
        this._passwordLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _passwordBox
        //
        this._passwordBox.Location = new System.Drawing.Point(104, 198);
        this._passwordBox.MaxLength = 128;
        this._passwordBox.Name = "_passwordBox";
        this._passwordBox.Size = new System.Drawing.Size(340, 25);
        this._passwordBox.TabIndex = 7;
        this._passwordBox.UseSystemPasswordChar = true;
        //
        // _errorLabel
        //
        this._errorLabel.ForeColor = System.Drawing.Color.Firebrick;
        this._errorLabel.Location = new System.Drawing.Point(18, 232);
        this._errorLabel.Name = "_errorLabel";
        this._errorLabel.Size = new System.Drawing.Size(426, 44);
        this._errorLabel.TabIndex = 8;
        //
        // _signInButton
        //
        this._signInButton.Location = new System.Drawing.Point(188, 290);
        this._signInButton.Name = "_signInButton";
        this._signInButton.Size = new System.Drawing.Size(120, 36);
        this._signInButton.TabIndex = 9;
        this._signInButton.Text = "登录";
        this._signInButton.UseVisualStyleBackColor = true;
        //
        // _cancelButton
        //
        this._cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        this._cancelButton.Location = new System.Drawing.Point(324, 290);
        this._cancelButton.Name = "_cancelButton";
        this._cancelButton.Size = new System.Drawing.Size(120, 36);
        this._cancelButton.TabIndex = 10;
        this._cancelButton.Text = "取消";
        this._cancelButton.UseVisualStyleBackColor = true;
        //
        // SignInForm
        //
        this.AcceptButton = this._signInButton;
        this.CancelButton = this._cancelButton;
        this.ClientSize = new System.Drawing.Size(462, 340);
        this.Controls.Add(this._titleLabel);
        this.Controls.Add(this._hintLabel);
        this.Controls.Add(this._accountLabel);
        this.Controls.Add(this._accountCombo);
        this.Controls.Add(this._roleLabel);
        this.Controls.Add(this._roleDescriptionLabel);
        this.Controls.Add(this._passwordLabel);
        this.Controls.Add(this._passwordBox);
        this.Controls.Add(this._errorLabel);
        this.Controls.Add(this._signInButton);
        this.Controls.Add(this._cancelButton);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "SignInForm";
        this.ShowInTaskbar = true;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "登录";
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
