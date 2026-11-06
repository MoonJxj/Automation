namespace EquipmentMonitor.Desktop.Forms;

partial class ManualOperationPage
{
    private System.Windows.Forms.Panel _headerPanel = null!;
    private System.Windows.Forms.Label _titleLabel = null!;
    private System.Windows.Forms.Button _station1Button = null!;
    private System.Windows.Forms.Button _station2Button = null!;
    private System.Windows.Forms.TabControl _tabs = null!;
    private System.Windows.Forms.TabPage _cylinderTab = null!;
    private System.Windows.Forms.TabPage _axisTab = null!;
    private System.Windows.Forms.TabPage _robotTab = null!;
    private System.Windows.Forms.Label _robotHint = null!;

    private void InitializeComponent()
    {
        this._headerPanel = new System.Windows.Forms.Panel();
        this._titleLabel = new System.Windows.Forms.Label();
        this._station1Button = new System.Windows.Forms.Button();
        this._station2Button = new System.Windows.Forms.Button();
        this._tabs = new System.Windows.Forms.TabControl();
        this._cylinderTab = new System.Windows.Forms.TabPage();
        this._axisTab = new System.Windows.Forms.TabPage();
        this._robotTab = new System.Windows.Forms.TabPage();
        this._robotHint = new System.Windows.Forms.Label();
        this._headerPanel.SuspendLayout();
        this._tabs.SuspendLayout();
        this._robotTab.SuspendLayout();
        this.SuspendLayout();

        this._titleLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
        this._titleLabel.Location = new System.Drawing.Point(12, 12);
        this._titleLabel.Name = "_titleLabel";
        this._titleLabel.Size = new System.Drawing.Size(160, 28);
        this._titleLabel.Text = "手动操作";

        this._station1Button.Location = new System.Drawing.Point(190, 10);
        this._station1Button.Name = "_station1Button";
        this._station1Button.Size = new System.Drawing.Size(110, 32);
        this._station1Button.Text = "工位1";
        this._station1Button.UseVisualStyleBackColor = true;

        this._station2Button.Location = new System.Drawing.Point(308, 10);
        this._station2Button.Name = "_station2Button";
        this._station2Button.Size = new System.Drawing.Size(110, 32);
        this._station2Button.Text = "工位2";
        this._station2Button.UseVisualStyleBackColor = true;

        this._headerPanel.Controls.Add(this._titleLabel);
        this._headerPanel.Controls.Add(this._station1Button);
        this._headerPanel.Controls.Add(this._station2Button);
        this._headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
        this._headerPanel.Location = new System.Drawing.Point(0, 0);
        this._headerPanel.Name = "_headerPanel";
        this._headerPanel.Size = new System.Drawing.Size(1000, 52);

        this._robotHint.Dock = System.Windows.Forms.DockStyle.Fill;
        this._robotHint.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
        this._robotHint.ForeColor = System.Drawing.SystemColors.GrayText;
        this._robotHint.Location = new System.Drawing.Point(0, 0);
        this._robotHint.Name = "_robotHint";
        this._robotHint.Padding = new System.Windows.Forms.Padding(24);
        this._robotHint.Size = new System.Drawing.Size(992, 544);
        this._robotHint.Text = "机械手动作：骨架占位。接入机械手控制器与点位后，在此按工位显示动作按钮。";
        this._robotHint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

        this._robotTab.Controls.Add(this._robotHint);
        this._robotTab.Text = "机械手动作";

        this._cylinderTab.Text = "气缸动作";
        this._axisTab.Text = "伺服动作";

        this._tabs.Controls.Add(this._cylinderTab);
        this._tabs.Controls.Add(this._axisTab);
        this._tabs.Controls.Add(this._robotTab);
        this._tabs.Dock = System.Windows.Forms.DockStyle.Fill;
        this._tabs.Location = new System.Drawing.Point(0, 52);
        this._tabs.Name = "_tabs";
        this._tabs.SelectedIndex = 0;
        this._tabs.Size = new System.Drawing.Size(1000, 548);

        this.ClientSize = new System.Drawing.Size(1000, 600);
        this.Controls.Add(this._tabs);
        this.Controls.Add(this._headerPanel);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.Name = "ManualOperationPage";
        this.Text = "手动操作";
        this._headerPanel.ResumeLayout(false);
        this._tabs.ResumeLayout(false);
        this._robotTab.ResumeLayout(false);
        this.ResumeLayout(false);
    }
}
