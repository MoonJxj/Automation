namespace EquipmentMonitor.Desktop.Forms;

partial class StationBypassDialog
{
    private System.Windows.Forms.Label _titleLabel = null!;
    private System.Windows.Forms.Label _subtitleLabel = null!;
    private System.Windows.Forms.Label _bannerLabel = null!;
    private System.Windows.Forms.FlowLayoutPanel _rowsPanel = null!;
    private System.Windows.Forms.Label _hintLabel = null!;
    private System.Windows.Forms.Label _statusLabel = null!;
    private System.Windows.Forms.Button _saveButton = null!;
    private System.Windows.Forms.Button _closeButton = null!;

    private void InitializeComponent()
    {
        this._titleLabel = new System.Windows.Forms.Label();
        this._subtitleLabel = new System.Windows.Forms.Label();
        this._bannerLabel = new System.Windows.Forms.Label();
        this._rowsPanel = new System.Windows.Forms.FlowLayoutPanel();
        this._hintLabel = new System.Windows.Forms.Label();
        this._statusLabel = new System.Windows.Forms.Label();
        this._saveButton = new System.Windows.Forms.Button();
        this._closeButton = new System.Windows.Forms.Button();
        this.SuspendLayout();
        //
        // _titleLabel
        //
        this._titleLabel.AutoSize = true;
        this._titleLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 14F, System.Drawing.FontStyle.Bold);
        this._titleLabel.Location = new System.Drawing.Point(18, 14);
        this._titleLabel.Name = "_titleLabel";
        this._titleLabel.Text = "工位屏蔽";
        //
        // _subtitleLabel
        //
        this._subtitleLabel.Location = new System.Drawing.Point(20, 50);
        this._subtitleLabel.Name = "_subtitleLabel";
        this._subtitleLabel.Size = new System.Drawing.Size(570, 36);
        this._subtitleLabel.Text = "屏蔽后自动循环跳过该工位；必须填写原因，变更写入审计。";
        //
        // _bannerLabel
        //
        this._bannerLabel.BackColor = System.Drawing.Color.FromArgb(255, 248, 225);
        this._bannerLabel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this._bannerLabel.ForeColor = System.Drawing.Color.FromArgb(110, 80, 0);
        this._bannerLabel.Location = new System.Drawing.Point(20, 92);
        this._bannerLabel.Name = "_bannerLabel";
        this._bannerLabel.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);
        this._bannerLabel.Size = new System.Drawing.Size(580, 44);
        this._bannerLabel.Text = "示例实现：状态仅保存在内存，重启后恢复；尚未下发 PLC。";
        this._bannerLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _rowsPanel
        //
        this._rowsPanel.AutoScroll = true;
        this._rowsPanel.BackColor = System.Drawing.Color.White;
        this._rowsPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this._rowsPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
        this._rowsPanel.Location = new System.Drawing.Point(20, 146);
        this._rowsPanel.Name = "_rowsPanel";
        this._rowsPanel.Padding = new System.Windows.Forms.Padding(6);
        this._rowsPanel.Size = new System.Drawing.Size(580, 284);
        this._rowsPanel.TabIndex = 0;
        this._rowsPanel.WrapContents = false;
        //
        // _hintLabel
        //
        this._hintLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        this._hintLabel.Location = new System.Drawing.Point(20, 438);
        this._hintLabel.Name = "_hintLabel";
        this._hintLabel.Size = new System.Drawing.Size(580, 22);
        this._hintLabel.Text = "操作者取自当前会话；原因最多 200 字。";
        //
        // _statusLabel
        //
        this._statusLabel.Location = new System.Drawing.Point(20, 466);
        this._statusLabel.Name = "_statusLabel";
        this._statusLabel.Size = new System.Drawing.Size(340, 44);
        this._statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _saveButton
        //
        this._saveButton.Location = new System.Drawing.Point(378, 470);
        this._saveButton.Name = "_saveButton";
        this._saveButton.Size = new System.Drawing.Size(100, 36);
        this._saveButton.TabIndex = 1;
        this._saveButton.Text = "保存";
        this._saveButton.UseVisualStyleBackColor = true;
        //
        // _closeButton
        //
        this._closeButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        this._closeButton.Location = new System.Drawing.Point(490, 470);
        this._closeButton.Name = "_closeButton";
        this._closeButton.Size = new System.Drawing.Size(100, 36);
        this._closeButton.TabIndex = 2;
        this._closeButton.Text = "关闭";
        this._closeButton.UseVisualStyleBackColor = true;
        //
        // StationBypassDialog
        //
        this.AcceptButton = this._saveButton;
        this.CancelButton = this._closeButton;
        this.ClientSize = new System.Drawing.Size(620, 520);
        this.Controls.Add(this._titleLabel);
        this.Controls.Add(this._subtitleLabel);
        this.Controls.Add(this._bannerLabel);
        this.Controls.Add(this._rowsPanel);
        this.Controls.Add(this._hintLabel);
        this.Controls.Add(this._statusLabel);
        this.Controls.Add(this._saveButton);
        this.Controls.Add(this._closeButton);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "StationBypassDialog";
        this.ShowInTaskbar = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "工位屏蔽";
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
