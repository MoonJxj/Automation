namespace EquipmentMonitor.Desktop.Controls;

partial class ParameterEditorControl
{
    private System.Windows.Forms.Panel _headerPanel = null!;
    private System.Windows.Forms.Label _titleLabel = null!;
    private System.Windows.Forms.Label _subtitleLabel = null!;
    private System.Windows.Forms.Label _dirtyLabel = null!;
    private System.Windows.Forms.FlowLayoutPanel _groups = null!;
    private System.Windows.Forms.Label _statusLabel = null!;

    private void InitializeComponent()
    {
        _headerPanel = new System.Windows.Forms.Panel();
        _titleLabel = new System.Windows.Forms.Label();
        _subtitleLabel = new System.Windows.Forms.Label();
        _dirtyLabel = new System.Windows.Forms.Label();
        _groups = new System.Windows.Forms.FlowLayoutPanel();
        _statusLabel = new System.Windows.Forms.Label();
        _headerPanel.SuspendLayout();
        SuspendLayout();

        _titleLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
        _titleLabel.Location = new System.Drawing.Point(12, 10);
        _titleLabel.Size = new System.Drawing.Size(420, 28);
        _titleLabel.Text = "参数";

        _subtitleLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _subtitleLabel.Location = new System.Drawing.Point(14, 38);
        _subtitleLabel.Size = new System.Drawing.Size(520, 22);
        _subtitleLabel.Text = "选择左侧元件后编辑参数。";

        _dirtyLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        _dirtyLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _dirtyLabel.Location = new System.Drawing.Point(600, 18);
        _dirtyLabel.Size = new System.Drawing.Size(140, 24);
        _dirtyLabel.Text = "未修改";
        _dirtyLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_subtitleLabel);
        _headerPanel.Controls.Add(_dirtyLabel);
        _headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
        _headerPanel.Size = new System.Drawing.Size(760, 66);

        _groups.AutoScroll = true;
        _groups.Dock = System.Windows.Forms.DockStyle.Fill;
        _groups.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
        _groups.Padding = new System.Windows.Forms.Padding(8);
        _groups.WrapContents = false;

        _statusLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
        _statusLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        _statusLabel.Size = new System.Drawing.Size(760, 28);
        _statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

        AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        Controls.Add(_groups);
        Controls.Add(_statusLabel);
        Controls.Add(_headerPanel);
        Name = "ParameterEditorControl";
        Size = new System.Drawing.Size(760, 600);
        _headerPanel.ResumeLayout(false);
        ResumeLayout(false);
    }
}
