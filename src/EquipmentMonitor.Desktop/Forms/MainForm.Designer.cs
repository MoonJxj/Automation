namespace EquipmentMonitor.Desktop.Forms;

partial class MainForm
{
    private System.Windows.Forms.TableLayoutPanel _rootLayout = null!;
    private System.Windows.Forms.TableLayoutPanel _bodyLayout = null!;
    private System.Windows.Forms.FlowLayoutPanel _navPanel = null!;
    private System.Windows.Forms.Panel _contentPanel = null!;
    private System.Windows.Forms.Label _navTitle = null!;
    private System.Windows.Forms.StatusStrip _statusStrip = null!;
    private System.Windows.Forms.ToolStripStatusLabel _userLabel = null!;
    private System.Windows.Forms.ToolStripStatusLabel _switchUserLabel = null!;
    private System.Windows.Forms.ToolStripStatusLabel _axesLabel = null!;
    private System.Windows.Forms.ToolStripStatusLabel _cylindersLabel = null!;
    private System.Windows.Forms.ToolStripStatusLabel _refreshLabel = null!;
    private System.Windows.Forms.ToolStripStatusLabel _alarmLabel = null!;
    private System.Windows.Forms.ToolStripStatusLabel _errorLabel = null!;

    /// <summary>设计器生成的界面初始化。运行期由构造函数调用。</summary>
    private void InitializeComponent()
    {
        this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
        this._bodyLayout = new System.Windows.Forms.TableLayoutPanel();
        this._navPanel = new System.Windows.Forms.FlowLayoutPanel();
        this._navTitle = new System.Windows.Forms.Label();
        this._contentPanel = new System.Windows.Forms.Panel();
        this._statusStrip = new System.Windows.Forms.StatusStrip();
        this._userLabel = new System.Windows.Forms.ToolStripStatusLabel();
        this._switchUserLabel = new System.Windows.Forms.ToolStripStatusLabel();
        this._axesLabel = new System.Windows.Forms.ToolStripStatusLabel();
        this._cylindersLabel = new System.Windows.Forms.ToolStripStatusLabel();
        this._refreshLabel = new System.Windows.Forms.ToolStripStatusLabel();
        this._alarmLabel = new System.Windows.Forms.ToolStripStatusLabel();
        this._errorLabel = new System.Windows.Forms.ToolStripStatusLabel();
        this._rootLayout.SuspendLayout();
        this._bodyLayout.SuspendLayout();
        this._navPanel.SuspendLayout();
        this._statusStrip.SuspendLayout();
        this.SuspendLayout();
        //
        // _navTitle
        //
        this._navTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold);
        this._navTitle.Location = new System.Drawing.Point(8, 8);
        this._navTitle.Name = "_navTitle";
        this._navTitle.Size = new System.Drawing.Size(160, 30);
        this._navTitle.TabIndex = 0;
        this._navTitle.Text = "导航";
        this._navTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _navPanel
        //
        this._navPanel.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
        this._navPanel.Controls.Add(this._navTitle);
        this._navPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this._navPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
        this._navPanel.Location = new System.Drawing.Point(0, 0);
        this._navPanel.Name = "_navPanel";
        this._navPanel.Padding = new System.Windows.Forms.Padding(8);
        this._navPanel.Size = new System.Drawing.Size(180, 674);
        this._navPanel.TabIndex = 0;
        this._navPanel.WrapContents = false;
        //
        // _contentPanel
        //
        this._contentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this._contentPanel.Location = new System.Drawing.Point(180, 0);
        this._contentPanel.Name = "_contentPanel";
        this._contentPanel.Padding = new System.Windows.Forms.Padding(8);
        this._contentPanel.Size = new System.Drawing.Size(1000, 674);
        this._contentPanel.TabIndex = 1;
        //
        // _bodyLayout
        //
        this._bodyLayout.ColumnCount = 2;
        this._bodyLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
        this._bodyLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._bodyLayout.Controls.Add(this._navPanel, 0, 0);
        this._bodyLayout.Controls.Add(this._contentPanel, 1, 0);
        this._bodyLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this._bodyLayout.Location = new System.Drawing.Point(0, 0);
        this._bodyLayout.Name = "_bodyLayout";
        this._bodyLayout.RowCount = 1;
        this._bodyLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._bodyLayout.Size = new System.Drawing.Size(1180, 674);
        this._bodyLayout.TabIndex = 0;
        //
        // _userLabel
        //
        this._userLabel.Name = "_userLabel";
        this._userLabel.Size = new System.Drawing.Size(220, 17);
        this._userLabel.Text = "用户：未登录";
        //
        // _switchUserLabel
        //
        this._switchUserLabel.IsLink = true;
        this._switchUserLabel.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
        this._switchUserLabel.Name = "_switchUserLabel";
        this._switchUserLabel.Size = new System.Drawing.Size(80, 17);
        this._switchUserLabel.Text = "切换用户";
        this._switchUserLabel.Visible = false;
        //
        // _axesLabel
        //
        this._axesLabel.Name = "_axesLabel";
        this._axesLabel.Size = new System.Drawing.Size(57, 17);
        this._axesLabel.Text = "轴：—";
        this._cylindersLabel.Name = "_cylindersLabel";
        this._cylindersLabel.Size = new System.Drawing.Size(80, 17);
        this._cylindersLabel.Text = "气缸：—";
        //
        // _refreshLabel
        //
        this._refreshLabel.Name = "_refreshLabel";
        this._refreshLabel.Size = new System.Drawing.Size(120, 17);
        this._refreshLabel.Text = "刷新：—";
        //
        // _alarmLabel
        //
        this._alarmLabel.IsLink = true;
        this._alarmLabel.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
        this._alarmLabel.Name = "_alarmLabel";
        this._alarmLabel.Size = new System.Drawing.Size(150, 17);
        this._alarmLabel.Text = "报警：—";
        //
        // _errorLabel
        //
        this._errorLabel.Name = "_errorLabel";
        this._errorLabel.Size = new System.Drawing.Size(900, 17);
        this._errorLabel.Spring = true;
        this._errorLabel.Text = "采集：—";
        this._errorLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _statusStrip
        //
        this._statusStrip.Dock = System.Windows.Forms.DockStyle.Fill;
        this._statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._userLabel,
            this._switchUserLabel,
            this._axesLabel,
            this._cylindersLabel,
            this._refreshLabel,
            this._alarmLabel,
            this._errorLabel});
        this._statusStrip.Location = new System.Drawing.Point(0, 674);
        this._statusStrip.Name = "_statusStrip";
        this._statusStrip.SizingGrip = false;
        this._statusStrip.Size = new System.Drawing.Size(1180, 24);
        this._statusStrip.TabIndex = 1;
        //
        // _rootLayout
        //
        this._rootLayout.ColumnCount = 1;
        this._rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._rootLayout.Controls.Add(this._bodyLayout, 0, 0);
        this._rootLayout.Controls.Add(this._statusStrip, 0, 1);
        this._rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this._rootLayout.Location = new System.Drawing.Point(0, 0);
        this._rootLayout.Name = "_rootLayout";
        this._rootLayout.RowCount = 2;
        this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this._rootLayout.Size = new System.Drawing.Size(1180, 698);
        this._rootLayout.TabIndex = 0;
        //
        // MainForm
        //
        this.ClientSize = new System.Drawing.Size(1180, 698);
        this.Controls.Add(this._rootLayout);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.MinimumSize = new System.Drawing.Size(1180, 640);
        this.Name = "MainForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "设备监控上位机 — 轴控与气缸";
        this._rootLayout.ResumeLayout(false);
        this._rootLayout.PerformLayout();
        this._bodyLayout.ResumeLayout(false);
        this._navPanel.ResumeLayout(false);
        this._statusStrip.ResumeLayout(false);
        this._statusStrip.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
