namespace EquipmentMonitor.Desktop.Controls;

partial class JogModePanel
{
    private System.Windows.Forms.GroupBox _group = null!;
    private System.Windows.Forms.Label _velocityLabel = null!;
    private System.Windows.Forms.TextBox _velocityBox = null!;
    private System.Windows.Forms.Label _velocityUnitLabel = null!;
    private System.Windows.Forms.Label _accelLabel = null!;
    private System.Windows.Forms.TextBox _accelBox = null!;
    private System.Windows.Forms.Label _accelUnitLabel = null!;
    private System.Windows.Forms.CheckBox _jerkLimitCheck = null!;
    private System.Windows.Forms.Label _jerkLabel = null!;
    private System.Windows.Forms.TextBox _jerkBox = null!;
    private System.Windows.Forms.Label _jerkUnitLabel = null!;

    private void InitializeComponent()
    {
        _group = new System.Windows.Forms.GroupBox();
        _velocityLabel = new System.Windows.Forms.Label();
        _velocityBox = new System.Windows.Forms.TextBox();
        _velocityUnitLabel = new System.Windows.Forms.Label();
        _accelLabel = new System.Windows.Forms.Label();
        _accelBox = new System.Windows.Forms.TextBox();
        _accelUnitLabel = new System.Windows.Forms.Label();
        _jerkLimitCheck = new System.Windows.Forms.CheckBox();
        _jerkLabel = new System.Windows.Forms.Label();
        _jerkBox = new System.Windows.Forms.TextBox();
        _jerkUnitLabel = new System.Windows.Forms.Label();
        _group.SuspendLayout();
        SuspendLayout();

        _velocityLabel.Location = new System.Drawing.Point(196, 34);
        _velocityLabel.Size = new System.Drawing.Size(70, 22);
        _velocityLabel.Text = "速度：";
        _velocityLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        _velocityBox.Location = new System.Drawing.Point(270, 30);
        _velocityBox.ReadOnly = true;
        _velocityBox.Size = new System.Drawing.Size(80, 26);
        _velocityBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

        _velocityUnitLabel.Location = new System.Drawing.Point(352, 34);
        _velocityUnitLabel.Size = new System.Drawing.Size(60, 22);
        _velocityUnitLabel.Text = "mm/s";

        _accelLabel.Location = new System.Drawing.Point(116, 78);
        _accelLabel.Size = new System.Drawing.Size(150, 22);
        _accelLabel.Text = "加速度/减速度：";
        _accelLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        _accelBox.Location = new System.Drawing.Point(270, 74);
        _accelBox.ReadOnly = true;
        _accelBox.Size = new System.Drawing.Size(80, 26);
        _accelBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

        _accelUnitLabel.Location = new System.Drawing.Point(352, 78);
        _accelUnitLabel.Size = new System.Drawing.Size(70, 22);
        _accelUnitLabel.Text = "mm/s²";

        _jerkLimitCheck.Enabled = false;
        _jerkLimitCheck.Location = new System.Drawing.Point(116, 120);
        _jerkLimitCheck.Size = new System.Drawing.Size(190, 24);
        _jerkLimitCheck.Text = "激活加加速度限值";

        _jerkLabel.Location = new System.Drawing.Point(186, 154);
        _jerkLabel.Size = new System.Drawing.Size(80, 22);
        _jerkLabel.Text = "加加速度：";
        _jerkLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        _jerkBox.Enabled = false;
        _jerkBox.Location = new System.Drawing.Point(270, 150);
        _jerkBox.ReadOnly = true;
        _jerkBox.Size = new System.Drawing.Size(80, 26);
        _jerkBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

        _jerkUnitLabel.Location = new System.Drawing.Point(352, 154);
        _jerkUnitLabel.Size = new System.Drawing.Size(80, 22);
        _jerkUnitLabel.Text = "mm/s³";

        _group.Controls.Add(_velocityLabel);
        _group.Controls.Add(_velocityBox);
        _group.Controls.Add(_velocityUnitLabel);
        _group.Controls.Add(_accelLabel);
        _group.Controls.Add(_accelBox);
        _group.Controls.Add(_accelUnitLabel);
        _group.Controls.Add(_jerkLimitCheck);
        _group.Controls.Add(_jerkLabel);
        _group.Controls.Add(_jerkBox);
        _group.Controls.Add(_jerkUnitLabel);
        _group.Dock = System.Windows.Forms.DockStyle.Fill;
        _group.Text = "点动";

        AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        Controls.Add(_group);
        Name = "JogModePanel";
        Size = new System.Drawing.Size(500, 184);
        _group.ResumeLayout(false);
        _group.PerformLayout();
        ResumeLayout(false);
    }
}
