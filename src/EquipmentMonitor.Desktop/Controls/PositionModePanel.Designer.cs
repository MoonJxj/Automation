namespace EquipmentMonitor.Desktop.Controls;

partial class PositionModePanel
{
    private System.Windows.Forms.GroupBox _group = null!;
    private System.Windows.Forms.RadioButton _absoluteRadio = null!;
    private System.Windows.Forms.RadioButton _relativeRadio = null!;
    private System.Windows.Forms.Label _targetLabel = null!;
    private System.Windows.Forms.TextBox _targetPositionBox = null!;
    private System.Windows.Forms.Label _targetUnitLabel = null!;
    private System.Windows.Forms.Button _moveButton = null!;
    private System.Windows.Forms.Label _hintLabel = null!;

    private void InitializeComponent()
    {
        _group = new GroupBox();
        _absoluteRadio = new RadioButton();
        _relativeRadio = new RadioButton();
        _targetLabel = new Label();
        _targetPositionBox = new TextBox();
        _targetUnitLabel = new Label();
        _moveButton = new Button();
        _hintLabel = new Label();
        _group.SuspendLayout();
        SuspendLayout();
        // 
        // _group
        // 
        _group.Controls.Add(_absoluteRadio);
        _group.Controls.Add(_relativeRadio);
        _group.Controls.Add(_targetLabel);
        _group.Controls.Add(_targetPositionBox);
        _group.Controls.Add(_targetUnitLabel);
        _group.Controls.Add(_moveButton);
        _group.Controls.Add(_hintLabel);
        _group.Dock = DockStyle.Fill;
        _group.Location = new Point(0, 0);
        _group.Name = "_group";
        _group.Size = new Size(500, 184);
        _group.TabIndex = 0;
        _group.TabStop = false;
        _group.Text = "定位";
        // 
        // _absoluteRadio
        // 
        _absoluteRadio.Checked = true;
        _absoluteRadio.Location = new Point(16, 24);
        _absoluteRadio.Name = "_absoluteRadio";
        _absoluteRadio.Size = new Size(90, 29);
        _absoluteRadio.TabIndex = 0;
        _absoluteRadio.TabStop = true;
        _absoluteRadio.Text = "绝对";
        // 
        // _relativeRadio
        // 
        _relativeRadio.Location = new Point(112, 24);
        _relativeRadio.Name = "_relativeRadio";
        _relativeRadio.Size = new Size(90, 29);
        _relativeRadio.TabIndex = 1;
        _relativeRadio.Text = "相对";
        // 
        // _targetLabel
        // 
        _targetLabel.Location = new Point(16, 56);
        _targetLabel.Name = "_targetLabel";
        _targetLabel.Size = new Size(104, 22);
        _targetLabel.TabIndex = 2;
        _targetLabel.Text = "目标位置：";
        _targetLabel.TextAlign = ContentAlignment.MiddleRight;
        // 
        // _targetPositionBox
        // 
        _targetPositionBox.Location = new Point(126, 52);
        _targetPositionBox.Name = "_targetPositionBox";
        _targetPositionBox.Size = new Size(100, 30);
        _targetPositionBox.TabIndex = 3;
        _targetPositionBox.Text = "0.000";
        _targetPositionBox.TextAlign = HorizontalAlignment.Right;
        // 
        // _targetUnitLabel
        // 
        _targetUnitLabel.Location = new Point(230, 56);
        _targetUnitLabel.Name = "_targetUnitLabel";
        _targetUnitLabel.Size = new Size(50, 22);
        _targetUnitLabel.TabIndex = 4;
        _targetUnitLabel.Text = "mm";
        // 
        // _moveButton
        // 
        _moveButton.Location = new Point(284, 48);
        _moveButton.Name = "_moveButton";
        _moveButton.Size = new Size(100, 32);
        _moveButton.TabIndex = 5;
        _moveButton.Text = "定位";
        _moveButton.UseVisualStyleBackColor = true;
        // 
        // _hintLabel
        // 
        _hintLabel.ForeColor = Color.FromArgb(100, 100, 100);
        _hintLabel.Location = new Point(16, 98);
        _hintLabel.Name = "_hintLabel";
        _hintLabel.Size = new Size(460, 52);
        _hintLabel.TabIndex = 6;
        _hintLabel.Text = "绝对：移动到目标坐标；相对：从当前位置位移。\r\n命令下发后，到位由状态观察。";
        // 
        // PositionModePanel
        // 
        AutoScaleMode = AutoScaleMode.None;
        Controls.Add(_group);
        Name = "PositionModePanel";
        Size = new Size(500, 184);
        _group.ResumeLayout(false);
        _group.PerformLayout();
        ResumeLayout(false);
    }
}
