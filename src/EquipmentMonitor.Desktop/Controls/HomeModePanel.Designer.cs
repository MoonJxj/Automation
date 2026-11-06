namespace EquipmentMonitor.Desktop.Controls;

partial class HomeModePanel
{
    private System.Windows.Forms.GroupBox _group = null!;
    private System.Windows.Forms.Button _homeButton = null!;
    private System.Windows.Forms.Label _hintLabel = null!;

    private void InitializeComponent()
    {
        _group = new System.Windows.Forms.GroupBox();
        _homeButton = new System.Windows.Forms.Button();
        _hintLabel = new System.Windows.Forms.Label();
        _group.SuspendLayout();
        SuspendLayout();

        _homeButton.Location = new System.Drawing.Point(24, 30);
        _homeButton.Size = new System.Drawing.Size(120, 32);
        _homeButton.Text = "回原点";
        _homeButton.UseVisualStyleBackColor = true;

        _hintLabel.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);
        _hintLabel.Location = new System.Drawing.Point(24, 88);
        _hintLabel.Size = new System.Drawing.Size(452, 52);
        _hintLabel.Text = "执行 MC_Home；回原点前请确认轴周围无人。\r\n命令下发后，到位由状态观察。";

        _group.Controls.Add(_homeButton);
        _group.Controls.Add(_hintLabel);
        _group.Dock = System.Windows.Forms.DockStyle.Fill;
        _group.Text = "回原点";

        AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        Controls.Add(_group);
        Name = "HomeModePanel";
        Size = new System.Drawing.Size(500, 184);
        _group.ResumeLayout(false);
        _group.PerformLayout();
        ResumeLayout(false);
    }
}
