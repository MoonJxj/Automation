namespace EquipmentMonitor.Desktop.Forms;

partial class UserEditDialog
{
    private System.Windows.Forms.Label _titleLabel = null!;
    private System.Windows.Forms.Label _hintLabel = null!;
    private System.Windows.Forms.Label _userNameLabel = null!;
    private System.Windows.Forms.TextBox _userNameBox = null!;
    private System.Windows.Forms.Label _displayLabel = null!;
    private System.Windows.Forms.TextBox _displayBox = null!;
    private System.Windows.Forms.Label _roleLabel = null!;
    private System.Windows.Forms.ComboBox _roleCombo = null!;
    private System.Windows.Forms.Label _descriptionLabel = null!;
    private System.Windows.Forms.Label _errorLabel = null!;
    private System.Windows.Forms.Button _okButton = null!;
    private System.Windows.Forms.Button _cancelButton = null!;

    /// <summary>设计器生成的界面初始化。运行期由构造函数调用。</summary>
    private void InitializeComponent()
    {
        this._titleLabel = new System.Windows.Forms.Label();
        this._hintLabel = new System.Windows.Forms.Label();
        this._userNameLabel = new System.Windows.Forms.Label();
        this._userNameBox = new System.Windows.Forms.TextBox();
        this._displayLabel = new System.Windows.Forms.Label();
        this._displayBox = new System.Windows.Forms.TextBox();
        this._roleLabel = new System.Windows.Forms.Label();
        this._roleCombo = new System.Windows.Forms.ComboBox();
        this._descriptionLabel = new System.Windows.Forms.Label();
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
        this._titleLabel.Text = "新建用户";
        //
        // _hintLabel
        //
        this._hintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        this._hintLabel.Location = new System.Drawing.Point(18, 46);
        this._hintLabel.Name = "_hintLabel";
        this._hintLabel.Size = new System.Drawing.Size(386, 34);
        this._hintLabel.TabIndex = 1;
        this._hintLabel.Text = "账号名不可改；新账号默认未设口令。";
        //
        // _userNameLabel
        //
        this._userNameLabel.Location = new System.Drawing.Point(18, 90);
        this._userNameLabel.Name = "_userNameLabel";
        this._userNameLabel.Size = new System.Drawing.Size(80, 24);
        this._userNameLabel.TabIndex = 2;
        this._userNameLabel.Text = "账号";
        this._userNameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _userNameBox
        //
        this._userNameBox.Location = new System.Drawing.Point(104, 88);
        this._userNameBox.MaxLength = 32;
        this._userNameBox.Name = "_userNameBox";
        this._userNameBox.Size = new System.Drawing.Size(296, 25);
        this._userNameBox.TabIndex = 3;
        //
        // _displayLabel
        //
        this._displayLabel.Location = new System.Drawing.Point(18, 128);
        this._displayLabel.Name = "_displayLabel";
        this._displayLabel.Size = new System.Drawing.Size(80, 24);
        this._displayLabel.TabIndex = 4;
        this._displayLabel.Text = "显示名";
        this._displayLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _displayBox
        //
        this._displayBox.Location = new System.Drawing.Point(104, 126);
        this._displayBox.MaxLength = 40;
        this._displayBox.Name = "_displayBox";
        this._displayBox.Size = new System.Drawing.Size(296, 25);
        this._displayBox.TabIndex = 5;
        //
        // _roleLabel
        //
        this._roleLabel.Location = new System.Drawing.Point(18, 166);
        this._roleLabel.Name = "_roleLabel";
        this._roleLabel.Size = new System.Drawing.Size(80, 24);
        this._roleLabel.TabIndex = 6;
        this._roleLabel.Text = "角色";
        this._roleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _roleCombo
        //
        this._roleCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this._roleCombo.Location = new System.Drawing.Point(104, 164);
        this._roleCombo.Name = "_roleCombo";
        this._roleCombo.Size = new System.Drawing.Size(296, 25);
        this._roleCombo.TabIndex = 7;
        //
        // _descriptionLabel
        //
        this._descriptionLabel.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
        this._descriptionLabel.Location = new System.Drawing.Point(104, 194);
        this._descriptionLabel.Name = "_descriptionLabel";
        this._descriptionLabel.Size = new System.Drawing.Size(296, 36);
        this._descriptionLabel.TabIndex = 8;
        //
        // _errorLabel
        //
        this._errorLabel.ForeColor = System.Drawing.Color.Firebrick;
        this._errorLabel.Location = new System.Drawing.Point(18, 232);
        this._errorLabel.Name = "_errorLabel";
        this._errorLabel.Size = new System.Drawing.Size(382, 36);
        this._errorLabel.TabIndex = 9;
        //
        // _okButton
        //
        this._okButton.Location = new System.Drawing.Point(188, 274);
        this._okButton.Name = "_okButton";
        this._okButton.Size = new System.Drawing.Size(100, 32);
        this._okButton.TabIndex = 10;
        this._okButton.Text = "保存";
        this._okButton.UseVisualStyleBackColor = true;
        //
        // _cancelButton
        //
        this._cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        this._cancelButton.Location = new System.Drawing.Point(300, 274);
        this._cancelButton.Name = "_cancelButton";
        this._cancelButton.Size = new System.Drawing.Size(100, 32);
        this._cancelButton.TabIndex = 11;
        this._cancelButton.Text = "取消";
        this._cancelButton.UseVisualStyleBackColor = true;
        //
        // UserEditDialog
        //
        this.AcceptButton = this._okButton;
        this.CancelButton = this._cancelButton;
        this.ClientSize = new System.Drawing.Size(420, 320);
        this.Controls.Add(this._titleLabel);
        this.Controls.Add(this._hintLabel);
        this.Controls.Add(this._userNameLabel);
        this.Controls.Add(this._userNameBox);
        this.Controls.Add(this._displayLabel);
        this.Controls.Add(this._displayBox);
        this.Controls.Add(this._roleLabel);
        this.Controls.Add(this._roleCombo);
        this.Controls.Add(this._descriptionLabel);
        this.Controls.Add(this._errorLabel);
        this.Controls.Add(this._okButton);
        this.Controls.Add(this._cancelButton);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "UserEditDialog";
        this.ShowInTaskbar = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "新建用户";
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
