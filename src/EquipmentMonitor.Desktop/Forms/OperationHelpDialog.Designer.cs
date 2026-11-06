namespace EquipmentMonitor.Desktop.Forms;

partial class OperationHelpDialog
{
    private System.Windows.Forms.Label _titleLabel = null!;
    private System.Windows.Forms.TextBox _helpBox = null!;
    private System.Windows.Forms.Button _closeButton = null!;

    private void InitializeComponent()
    {
        this._titleLabel = new System.Windows.Forms.Label();
        this._helpBox = new System.Windows.Forms.TextBox();
        this._closeButton = new System.Windows.Forms.Button();
        this.SuspendLayout();
        //
        // _titleLabel
        //
        this._titleLabel.AutoSize = true;
        this._titleLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 14F, System.Drawing.FontStyle.Bold);
        this._titleLabel.Location = new System.Drawing.Point(18, 14);
        this._titleLabel.Name = "_titleLabel";
        this._titleLabel.Text = "操作帮助";
        //
        // _helpBox
        //
        this._helpBox.BackColor = System.Drawing.Color.White;
        this._helpBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this._helpBox.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F);
        this._helpBox.Location = new System.Drawing.Point(20, 56);
        this._helpBox.Multiline = true;
        this._helpBox.Name = "_helpBox";
        this._helpBox.ReadOnly = true;
        this._helpBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this._helpBox.Size = new System.Drawing.Size(660, 440);
        this._helpBox.TabIndex = 0;
        this._helpBox.Text = "一、六个模块\r\n" +
            "  自动监控  伺服轴与气缸的只读总览，用于巡检。\r\n" +
            "  手动操作  按工位执行气缸 / 伺服动作；动作前需二次确认。\r\n" +
            "  功能选择  工位屏蔽、换型配方、设备维护等现场任务入口。\r\n" +
            "  参数设置  修改轴 / 气缸参数；按权限逐字段控制。\r\n" +
            "  报警信息  查看活动 / 历史报警，确认已知悉。\r\n" +
            "  用户管理  账号、角色与口令（仅管理员）。\r\n" +
            "\r\n" +
            "二、权限\r\n" +
            "  · 页面按钮的显隐只是提示，真正的拦截在服务入口。\r\n" +
            "  · 修改设备配置：设备工程师、管理员；修改工艺参数：工艺工程师及以上。\r\n" +
            "  · 操作设备：操作员及以上；动作前请确认工位无人、防护门已关闭。\r\n" +
            "\r\n" +
            "三、工位屏蔽（功能选择页示例）\r\n" +
            "  · 屏蔽后自动循环跳过该工位；必须填写原因，变更写入审计。\r\n" +
            "  · 当前为内存示例：重启后恢复，尚未下发 PLC；接设备后以现场为准。\r\n" +
            "\r\n" +
            "四、安全边界\r\n" +
            "  本软件不替代急停、安全门与机械硬限位；异常时先按急停，再排查。";
        //
        // _closeButton
        //
        this._closeButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        this._closeButton.Location = new System.Drawing.Point(580, 508);
        this._closeButton.Name = "_closeButton";
        this._closeButton.Size = new System.Drawing.Size(100, 36);
        this._closeButton.TabIndex = 1;
        this._closeButton.Text = "关闭";
        this._closeButton.UseVisualStyleBackColor = true;
        //
        // OperationHelpDialog
        //
        this.CancelButton = this._closeButton;
        this.ClientSize = new System.Drawing.Size(700, 560);
        this.Controls.Add(this._titleLabel);
        this.Controls.Add(this._helpBox);
        this.Controls.Add(this._closeButton);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "OperationHelpDialog";
        this.ShowInTaskbar = false;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "操作帮助";
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
