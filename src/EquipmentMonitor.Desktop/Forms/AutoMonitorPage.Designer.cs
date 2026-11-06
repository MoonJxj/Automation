namespace EquipmentMonitor.Desktop.Forms;

partial class AutoMonitorPage
{
    private System.Windows.Forms.TableLayoutPanel _rootLayout = null!;
    private System.Windows.Forms.Panel _statusPanel = null!;
    private System.Windows.Forms.TableLayoutPanel _statusLayout = null!;
    private System.Windows.Forms.Panel _infoPanel = null!;
    private System.Windows.Forms.Panel _buttonPanel = null!;
    private System.Windows.Forms.Label _stateLabel = null!;
    private System.Windows.Forms.Label _summaryLabel = null!;
    private System.Windows.Forms.Label _runHintLabel = null!;
    private System.Windows.Forms.Button _startButton = null!;
    private System.Windows.Forms.Button _stopButton = null!;
    private System.Windows.Forms.Button _resetButton = null!;
    private System.Windows.Forms.TabControl _tabs = null!;
    private System.Windows.Forms.TabPage _axisTab = null!;
    private System.Windows.Forms.TabPage _cylinderTab = null!;

    private void InitializeComponent()
    {
        this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
        this._statusPanel = new System.Windows.Forms.Panel();
        this._statusLayout = new System.Windows.Forms.TableLayoutPanel();
        this._infoPanel = new System.Windows.Forms.Panel();
        this._buttonPanel = new System.Windows.Forms.Panel();
        this._stateLabel = new System.Windows.Forms.Label();
        this._summaryLabel = new System.Windows.Forms.Label();
        this._runHintLabel = new System.Windows.Forms.Label();
        this._startButton = new System.Windows.Forms.Button();
        this._stopButton = new System.Windows.Forms.Button();
        this._resetButton = new System.Windows.Forms.Button();
        this._tabs = new System.Windows.Forms.TabControl();
        this._axisTab = new System.Windows.Forms.TabPage();
        this._cylinderTab = new System.Windows.Forms.TabPage();
        this._rootLayout.SuspendLayout();
        this._statusPanel.SuspendLayout();
        this._statusLayout.SuspendLayout();
        this._infoPanel.SuspendLayout();
        this._buttonPanel.SuspendLayout();
        this._tabs.SuspendLayout();
        this.SuspendLayout();
        //
        // _stateLabel
        //
        this._stateLabel.AutoSize = false;
        this._stateLabel.Dock = System.Windows.Forms.DockStyle.Top;
        this._stateLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 15F, System.Drawing.FontStyle.Bold);
        this._stateLabel.ForeColor = System.Drawing.Color.DimGray;
        this._stateLabel.Location = new System.Drawing.Point(14, 8);
        this._stateLabel.Name = "_stateLabel";
        this._stateLabel.Size = new System.Drawing.Size(660, 38);
        this._stateLabel.TabIndex = 0;
        this._stateLabel.Text = "设备状态：—";
        this._stateLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _summaryLabel
        //
        this._summaryLabel.AutoEllipsis = true;
        this._summaryLabel.AutoSize = false;
        this._summaryLabel.Dock = System.Windows.Forms.DockStyle.Top;
        this._summaryLabel.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
        this._summaryLabel.Location = new System.Drawing.Point(14, 46);
        this._summaryLabel.Name = "_summaryLabel";
        this._summaryLabel.Size = new System.Drawing.Size(660, 20);
        this._summaryLabel.TabIndex = 1;
        this._summaryLabel.Text = "等待采集…";
        this._summaryLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _runHintLabel
        //
        this._runHintLabel.AutoEllipsis = true;
        this._runHintLabel.AutoSize = false;
        this._runHintLabel.Dock = System.Windows.Forms.DockStyle.Top;
        this._runHintLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.5F);
        this._runHintLabel.ForeColor = System.Drawing.Color.Gray;
        this._runHintLabel.Location = new System.Drawing.Point(14, 66);
        this._runHintLabel.Name = "_runHintLabel";
        this._runHintLabel.Size = new System.Drawing.Size(660, 20);
        this._runHintLabel.TabIndex = 2;
        this._runHintLabel.Text = "启动=伺服使能；停止=MC_Halt；自动流程未接 PLC。";
        this._runHintLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        //
        // _infoPanel
        //
        this._infoPanel.Controls.Add(this._runHintLabel);
        this._infoPanel.Controls.Add(this._summaryLabel);
        this._infoPanel.Controls.Add(this._stateLabel);
        this._infoPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this._infoPanel.Location = new System.Drawing.Point(0, 0);
        this._infoPanel.Name = "_infoPanel";
        this._infoPanel.Padding = new System.Windows.Forms.Padding(14, 8, 6, 0);
        this._infoPanel.Size = new System.Drawing.Size(686, 104);
        this._infoPanel.TabIndex = 0;
        //
        // _startButton
        //
        this._startButton.Enabled = false;
        this._startButton.Location = new System.Drawing.Point(2, 34);
        this._startButton.Name = "_startButton";
        this._startButton.Size = new System.Drawing.Size(94, 36);
        this._startButton.TabIndex = 0;
        this._startButton.Text = "启动设备";
        this._startButton.UseVisualStyleBackColor = true;
        //
        // _stopButton
        //
        this._stopButton.Enabled = false;
        this._stopButton.Location = new System.Drawing.Point(102, 34);
        this._stopButton.Name = "_stopButton";
        this._stopButton.Size = new System.Drawing.Size(94, 36);
        this._stopButton.TabIndex = 1;
        this._stopButton.Text = "停止设备";
        this._stopButton.UseVisualStyleBackColor = true;
        //
        // _resetButton
        //
        this._resetButton.Enabled = false;
        this._resetButton.Location = new System.Drawing.Point(202, 34);
        this._resetButton.Name = "_resetButton";
        this._resetButton.Size = new System.Drawing.Size(100, 36);
        this._resetButton.TabIndex = 2;
        this._resetButton.Text = "复位轴故障";
        this._resetButton.UseVisualStyleBackColor = true;
        //
        // _buttonPanel
        //
        this._buttonPanel.Controls.Add(this._startButton);
        this._buttonPanel.Controls.Add(this._stopButton);
        this._buttonPanel.Controls.Add(this._resetButton);
        this._buttonPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this._buttonPanel.Location = new System.Drawing.Point(686, 0);
        this._buttonPanel.Name = "_buttonPanel";
        this._buttonPanel.Size = new System.Drawing.Size(314, 104);
        this._buttonPanel.TabIndex = 1;
        //
        // _statusLayout
        //
        this._statusLayout.ColumnCount = 2;
        this._statusLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._statusLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 314F));
        this._statusLayout.Controls.Add(this._infoPanel, 0, 0);
        this._statusLayout.Controls.Add(this._buttonPanel, 1, 0);
        this._statusLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this._statusLayout.Location = new System.Drawing.Point(0, 0);
        this._statusLayout.Name = "_statusLayout";
        this._statusLayout.RowCount = 1;
        this._statusLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._statusLayout.Size = new System.Drawing.Size(1000, 104);
        this._statusLayout.TabIndex = 0;
        //
        // _statusPanel
        //
        this._statusPanel.BackColor = System.Drawing.Color.FromArgb(248, 249, 251);
        this._statusPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this._statusPanel.Controls.Add(this._statusLayout);
        this._statusPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this._statusPanel.Location = new System.Drawing.Point(0, 0);
        this._statusPanel.Name = "_statusPanel";
        this._statusPanel.Size = new System.Drawing.Size(1000, 104);
        this._statusPanel.TabIndex = 0;
        //
        // _axisTab
        //
        this._axisTab.Location = new System.Drawing.Point(4, 26);
        this._axisTab.Name = "_axisTab";
        this._axisTab.Padding = new System.Windows.Forms.Padding(3);
        this._axisTab.Size = new System.Drawing.Size(992, 466);
        this._axisTab.TabIndex = 0;
        this._axisTab.Text = "伺服轴";
        this._axisTab.UseVisualStyleBackColor = true;
        //
        // _cylinderTab
        //
        this._cylinderTab.Location = new System.Drawing.Point(4, 26);
        this._cylinderTab.Name = "_cylinderTab";
        this._cylinderTab.Padding = new System.Windows.Forms.Padding(3);
        this._cylinderTab.Size = new System.Drawing.Size(992, 466);
        this._cylinderTab.TabIndex = 1;
        this._cylinderTab.Text = "气缸";
        this._cylinderTab.UseVisualStyleBackColor = true;
        //
        // _tabs
        //
        this._tabs.Controls.Add(this._axisTab);
        this._tabs.Controls.Add(this._cylinderTab);
        this._tabs.Dock = System.Windows.Forms.DockStyle.Fill;
        this._tabs.Location = new System.Drawing.Point(0, 104);
        this._tabs.Name = "_tabs";
        this._tabs.SelectedIndex = 0;
        this._tabs.Size = new System.Drawing.Size(1000, 496);
        this._tabs.TabIndex = 1;
        //
        // _rootLayout
        //
        this._rootLayout.ColumnCount = 1;
        this._rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._rootLayout.Controls.Add(this._statusPanel, 0, 0);
        this._rootLayout.Controls.Add(this._tabs, 0, 1);
        this._rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
        this._rootLayout.Location = new System.Drawing.Point(0, 0);
        this._rootLayout.Name = "_rootLayout";
        this._rootLayout.RowCount = 2;
        this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 104F));
        this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._rootLayout.Size = new System.Drawing.Size(1000, 600);
        this._rootLayout.TabIndex = 0;
        //
        // AutoMonitorPage
        //
        this.ClientSize = new System.Drawing.Size(1000, 600);
        this.Controls.Add(this._rootLayout);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.Name = "AutoMonitorPage";
        this.Text = "自动监控";
        this._rootLayout.ResumeLayout(false);
        this._statusPanel.ResumeLayout(false);
        this._statusLayout.ResumeLayout(false);
        this._infoPanel.ResumeLayout(false);
        this._buttonPanel.ResumeLayout(false);
        this._tabs.ResumeLayout(false);
        this.ResumeLayout(false);
    }
}
