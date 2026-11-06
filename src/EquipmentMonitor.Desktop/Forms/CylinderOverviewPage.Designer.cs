namespace EquipmentMonitor.Desktop.Forms;

partial class CylinderOverviewPage
{
    private AntdUI.Table _table = null!;
    private AntdUI.Label _hint = null!;
    private System.Windows.Forms.Panel _bottomPanel = null!;
    private System.Windows.Forms.Button _openButton = null!;

    private void InitializeComponent()
    {
        this._table = new AntdUI.Table();
        this._hint = new AntdUI.Label();
        this._bottomPanel = new System.Windows.Forms.Panel();
        this._openButton = new System.Windows.Forms.Button();
        this._bottomPanel.SuspendLayout();
        this.SuspendLayout();

        this._table.Dock = System.Windows.Forms.DockStyle.Fill;
        this._table.Location = new System.Drawing.Point(0, 40);
        this._table.Name = "_table";
        this._table.Size = new System.Drawing.Size(1000, 532);
        this._table.TabIndex = 0;

        this._hint.Dock = System.Windows.Forms.DockStyle.Bottom;
        this._hint.Location = new System.Drawing.Point(0, 572);
        this._hint.Name = "_hint";
        this._hint.Size = new System.Drawing.Size(1000, 28);
        this._hint.TabIndex = 2;
        this._hint.Text = "等待采集…";

        this._openButton.Dock = System.Windows.Forms.DockStyle.Left;
        this._openButton.Location = new System.Drawing.Point(0, 0);
        this._openButton.Name = "_openButton";
        this._openButton.Size = new System.Drawing.Size(170, 40);
        this._openButton.TabIndex = 0;
        this._openButton.Text = "打开气缸操作台";
        this._openButton.UseVisualStyleBackColor = true;

        this._bottomPanel.Controls.Add(this._openButton);
        this._bottomPanel.Dock = System.Windows.Forms.DockStyle.Top;
        this._bottomPanel.Location = new System.Drawing.Point(0, 0);
        this._bottomPanel.Name = "_bottomPanel";
        this._bottomPanel.Size = new System.Drawing.Size(1000, 40);
        this._bottomPanel.TabIndex = 1;

        this.ClientSize = new System.Drawing.Size(1000, 600);
        this.Controls.Add(this._table);
        this.Controls.Add(this._bottomPanel);
        this.Controls.Add(this._hint);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.Name = "CylinderOverviewPage";
        this.Text = "气缸控制";
        this._bottomPanel.ResumeLayout(false);
        this.ResumeLayout(false);
    }
}
