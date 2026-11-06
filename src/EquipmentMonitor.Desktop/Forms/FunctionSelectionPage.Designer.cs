namespace EquipmentMonitor.Desktop.Forms;

partial class FunctionSelectionPage
{
    private System.Windows.Forms.TableLayoutPanel _rootLayout = null!;
    private System.Windows.Forms.Panel _headerPanel = null!;
    private System.Windows.Forms.Label _titleLabel = null!;
    private System.Windows.Forms.Label _subtitleLabel = null!;
    private System.Windows.Forms.FlowLayoutPanel _functionGrid = null!;
    private System.Windows.Forms.Panel _footerPanel = null!;
    private System.Windows.Forms.Label _statusLabel = null!;

    private void InitializeComponent()
    {
        this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
        this._headerPanel = new System.Windows.Forms.Panel();
        this._titleLabel = new System.Windows.Forms.Label();
        this._subtitleLabel = new System.Windows.Forms.Label();
        this._functionGrid = new System.Windows.Forms.FlowLayoutPanel();
        this._footerPanel = new System.Windows.Forms.Panel();
        this._statusLabel = new System.Windows.Forms.Label();
        this._rootLayout.SuspendLayout();
        this._headerPanel.SuspendLayout();
        this._footerPanel.SuspendLayout();
        this.SuspendLayout();
        //
        // _titleLabel
        //
        this._titleLabel.AutoSize = true;
        this._titleLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 14F, System.Drawing.FontStyle.Bold);
        this._titleLabel.Location = new System.Drawing.Point(18, 12);
        this._titleLabel.Name = "_titleLabel";
        this._titleLabel.TabIndex = 0;
        this._titleLabel.Text = "功能选择";
        //
        // _subtitleLabel
        //
        this._subtitleLabel.AutoSize = true;
        this._subtitleLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        this._subtitleLabel.Location = new System.Drawing.Point(20, 50);
        this._subtitleLabel.Name = "_subtitleLabel";
        this._subtitleLabel.TabIndex = 1;
        this._subtitleLabel.Text = "按现场任务选择功能；灰色卡片表示当前账号没有该权限。";
        //
        // _headerPanel
        //
        this._headerPanel.Controls.Add(this._subtitleLabel);
        this._headerPanel.Controls.Add(this._titleLabel);
        this._headerPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this._headerPanel.Location = new System.Drawing.Point(3, 3);
        this._headerPanel.Name = "_headerPanel";
        this._headerPanel.Size = new System.Drawing.Size(994, 84);
        this._headerPanel.TabIndex = 0;
        //
        // _functionGrid
        //
        this._functionGrid.AutoScroll = true;
        this._functionGrid.BackColor = System.Drawing.Color.FromArgb(247, 248, 250);
        this._functionGrid.Dock = System.Windows.Forms.DockStyle.Fill;
        this._functionGrid.Location = new System.Drawing.Point(3, 93);
        this._functionGrid.Name = "_functionGrid";
        this._functionGrid.Padding = new System.Windows.Forms.Padding(8);
        this._functionGrid.Size = new System.Drawing.Size(994, 454);
        this._functionGrid.TabIndex = 1;
        //
        // _statusLabel
        //
        this._statusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
        this._statusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        this._statusLabel.Location = new System.Drawing.Point(0, 0);
        this._statusLabel.Name = "_statusLabel";
        this._statusLabel.Padding = new System.Windows.Forms.Padding(18, 0, 18, 0);
        this._statusLabel.Size = new System.Drawing.Size(994, 44);
        this._statusLabel.TabIndex = 0;
        this._statusLabel.Text = "请选择功能。";
        this._statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _footerPanel
        //
        this._footerPanel.BackColor = System.Drawing.Color.White;
        this._footerPanel.Controls.Add(this._statusLabel);
        this._footerPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this._footerPanel.Location = new System.Drawing.Point(3, 553);
        this._footerPanel.Name = "_footerPanel";
        this._footerPanel.Size = new System.Drawing.Size(994, 44);
        this._footerPanel.TabIndex = 2;
        //
        // _rootLayout
        //
        this._rootLayout.ColumnCount = 1;
        this._rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._rootLayout.Controls.Add(this._headerPanel, 0, 0);
        this._rootLayout.Controls.Add(this._functionGrid, 0, 1);
        this._rootLayout.Controls.Add(this._footerPanel, 0, 2);
        this._rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this._rootLayout.Location = new System.Drawing.Point(0, 0);
        this._rootLayout.Name = "_rootLayout";
        this._rootLayout.RowCount = 3;
        this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
        this._rootLayout.Size = new System.Drawing.Size(1000, 600);
        this._rootLayout.TabIndex = 0;
        //
        // FunctionSelectionPage
        //
        this.ClientSize = new System.Drawing.Size(1000, 600);
        this.Controls.Add(this._rootLayout);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.Name = "FunctionSelectionPage";
        this.Text = "功能选择";
        this._rootLayout.ResumeLayout(false);
        this._rootLayout.PerformLayout();
        this._headerPanel.ResumeLayout(false);
        this._headerPanel.PerformLayout();
        this._footerPanel.ResumeLayout(false);
        this.ResumeLayout(false);
    }
}
