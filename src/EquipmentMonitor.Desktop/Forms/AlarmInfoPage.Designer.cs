namespace EquipmentMonitor.Desktop.Forms;

partial class AlarmInfoPage
{
    private AntdUI.Table _table = null!;
    private System.Windows.Forms.FlowLayoutPanel _toolPanel = null!;
    private System.Windows.Forms.ComboBox _levelFilter = null!;
    private System.Windows.Forms.ComboBox _stateFilter = null!;
    private System.Windows.Forms.Button _refreshButton = null!;
    private System.Windows.Forms.Button _historyButton = null!;
    private System.Windows.Forms.Button _ackButton = null!;
    private System.Windows.Forms.Button _ackAllButton = null!;
    private AntdUI.Label _hint = null!;
    private AntdUI.Label _detailLabel = null!;

    private void InitializeComponent()
    {
        this._table = new AntdUI.Table();
        this._toolPanel = new System.Windows.Forms.FlowLayoutPanel();
        this._levelFilter = new System.Windows.Forms.ComboBox();
        this._stateFilter = new System.Windows.Forms.ComboBox();
        this._refreshButton = new System.Windows.Forms.Button();
        this._historyButton = new System.Windows.Forms.Button();
        this._ackButton = new System.Windows.Forms.Button();
        this._ackAllButton = new System.Windows.Forms.Button();
        this._hint = new AntdUI.Label();
        this._detailLabel = new AntdUI.Label();
        this._toolPanel.SuspendLayout();
        this.SuspendLayout();

        //
        // _table
        //
        this._table.Dock = System.Windows.Forms.DockStyle.Fill;
        this._table.Location = new System.Drawing.Point(0, 44);
        this._table.Name = "_table";
        this._table.Size = new System.Drawing.Size(1000, 480);
        this._table.TabIndex = 1;

        //
        // _levelFilter
        //
        this._levelFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this._levelFilter.Name = "_levelFilter";
        this._levelFilter.Size = new System.Drawing.Size(110, 25);
        this._levelFilter.TabIndex = 0;

        //
        // _stateFilter
        //
        this._stateFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this._stateFilter.Name = "_stateFilter";
        this._stateFilter.Size = new System.Drawing.Size(130, 25);
        this._stateFilter.TabIndex = 1;

        //
        // _refreshButton
        //
        this._refreshButton.Name = "_refreshButton";
        this._refreshButton.Size = new System.Drawing.Size(70, 28);
        this._refreshButton.TabIndex = 2;
        this._refreshButton.Text = "刷新";
        this._refreshButton.UseVisualStyleBackColor = true;

        //
        // _historyButton
        //
        this._historyButton.Name = "_historyButton";
        this._historyButton.Size = new System.Drawing.Size(90, 28);
        this._historyButton.TabIndex = 3;
        this._historyButton.Text = "历史报警";
        this._historyButton.UseVisualStyleBackColor = true;

        //
        // _ackButton
        //
        this._ackButton.Name = "_ackButton";
        this._ackButton.Size = new System.Drawing.Size(70, 28);
        this._ackButton.TabIndex = 4;
        this._ackButton.Text = "确认";
        this._ackButton.UseVisualStyleBackColor = true;

        //
        // _ackAllButton
        //
        this._ackAllButton.Name = "_ackAllButton";
        this._ackAllButton.Size = new System.Drawing.Size(90, 28);
        this._ackAllButton.TabIndex = 5;
        this._ackAllButton.Text = "全部确认";
        this._ackAllButton.UseVisualStyleBackColor = true;

        //
        // _toolPanel
        //
        this._toolPanel.Controls.Add(this._levelFilter);
        this._toolPanel.Controls.Add(this._stateFilter);
        this._toolPanel.Controls.Add(this._refreshButton);
        this._toolPanel.Controls.Add(this._historyButton);
        this._toolPanel.Controls.Add(this._ackButton);
        this._toolPanel.Controls.Add(this._ackAllButton);
        this._toolPanel.Dock = System.Windows.Forms.DockStyle.Top;
        this._toolPanel.Location = new System.Drawing.Point(0, 0);
        this._toolPanel.Name = "_toolPanel";
        this._toolPanel.Padding = new System.Windows.Forms.Padding(8, 6, 8, 6);
        this._toolPanel.Size = new System.Drawing.Size(1000, 44);
        this._toolPanel.TabIndex = 0;
        this._toolPanel.WrapContents = false;

        //
        // _hint
        //
        this._hint.Dock = System.Windows.Forms.DockStyle.Bottom;
        this._hint.Location = new System.Drawing.Point(0, 572);
        this._hint.Name = "_hint";
        this._hint.Size = new System.Drawing.Size(1000, 28);
        this._hint.TabIndex = 3;
        this._hint.Text = "等待报警采集…";

        //
        // _detailLabel
        //
        this._detailLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
        this._detailLabel.Location = new System.Drawing.Point(0, 524);
        this._detailLabel.Name = "_detailLabel";
        this._detailLabel.Size = new System.Drawing.Size(1000, 48);
        this._detailLabel.TabIndex = 2;
        this._detailLabel.Text = "选中一条报警查看处理建议。";

        //
        // AlarmInfoPage
        //
        this.ClientSize = new System.Drawing.Size(1000, 600);
        this.Controls.Add(this._table);
        this.Controls.Add(this._detailLabel);
        this.Controls.Add(this._hint);
        this.Controls.Add(this._toolPanel);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.Name = "AlarmInfoPage";
        this.Text = "报警信息";
        this._toolPanel.ResumeLayout(false);
        this.ResumeLayout(false);
    }
}
