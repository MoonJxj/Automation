namespace EquipmentMonitor.Desktop.Controls;

partial class AxisOperationPanel
{
    private System.Windows.Forms.Panel _headerPanel = null!;
    private System.Windows.Forms.Label _mainLabel = null!;
    private System.Windows.Forms.Button _mainActivateButton = null!;
    private System.Windows.Forms.Button _mainDeactivateButton = null!;
    private System.Windows.Forms.Label _axisLabel = null!;
    private System.Windows.Forms.Button _axisEnableButton = null!;
    private System.Windows.Forms.Button _axisDisableButton = null!;
    private System.Windows.Forms.Panel _stripePanel = null!;

    private System.Windows.Forms.GroupBox _commandGroup = null!;
    private System.Windows.Forms.RadioButton _jogModeRadio = null!;
    private System.Windows.Forms.RadioButton _positionModeRadio = null!;
    private System.Windows.Forms.RadioButton _homeModeRadio = null!;

    private System.Windows.Forms.GroupBox _jogGroup = null!;
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

    private System.Windows.Forms.GroupBox _positionGroup = null!;
    private System.Windows.Forms.RadioButton _absoluteRadio = null!;
    private System.Windows.Forms.RadioButton _relativeRadio = null!;
    private System.Windows.Forms.Label _targetLabel = null!;
    private System.Windows.Forms.TextBox _targetPositionBox = null!;
    private System.Windows.Forms.Label _targetUnitLabel = null!;
    private System.Windows.Forms.Button _moveButton = null!;

    private System.Windows.Forms.GroupBox _homeGroup = null!;
    private System.Windows.Forms.Button _homeButton = null!;

    private System.Windows.Forms.Button _jogBackwardButton = null!;
    private System.Windows.Forms.Button _jogForwardButton = null!;
    private System.Windows.Forms.Button _stopButton = null!;

    private System.Windows.Forms.GroupBox _statusGroup = null!;
    private System.Windows.Forms.Panel _enabledIndicator = null!;
    private System.Windows.Forms.Label _enabledLabel = null!;
    private System.Windows.Forms.Panel _homedIndicator = null!;
    private System.Windows.Forms.Label _homedLabel = null!;
    private System.Windows.Forms.Panel _readyIndicator = null!;
    private System.Windows.Forms.Label _readyLabel = null!;
    private System.Windows.Forms.Panel _axisErrorIndicator = null!;
    private System.Windows.Forms.Label _axisErrorLabel = null!;
    private System.Windows.Forms.Panel _driveErrorIndicator = null!;
    private System.Windows.Forms.Label _driveErrorLabel = null!;
    private System.Windows.Forms.Button _ackButton = null!;

    private System.Windows.Forms.Label _infoLabel = null!;
    private System.Windows.Forms.TextBox _infoBox = null!;

    private System.Windows.Forms.GroupBox _currentGroup = null!;
    private System.Windows.Forms.Label _positionLabel = null!;
    private System.Windows.Forms.TextBox _positionValueBox = null!;
    private System.Windows.Forms.Label _positionUnitLabel = null!;
    private System.Windows.Forms.Label _actualVelocityLabel = null!;
    private System.Windows.Forms.TextBox _actualVelocityBox = null!;
    private System.Windows.Forms.Label _actualVelocityUnitLabel = null!;

    private System.Windows.Forms.Label _errorLabel = null!;
    private System.Windows.Forms.TextBox _errorBox = null!;
    private System.Windows.Forms.Label _hintLabel = null!;

    private void InitializeComponent()
    {
        _headerPanel = new System.Windows.Forms.Panel();
        _mainLabel = new System.Windows.Forms.Label();
        _mainActivateButton = new System.Windows.Forms.Button();
        _mainDeactivateButton = new System.Windows.Forms.Button();
        _axisLabel = new System.Windows.Forms.Label();
        _axisEnableButton = new System.Windows.Forms.Button();
        _axisDisableButton = new System.Windows.Forms.Button();
        _stripePanel = new System.Windows.Forms.Panel();
        _commandGroup = new System.Windows.Forms.GroupBox();
        _jogModeRadio = new System.Windows.Forms.RadioButton();
        _positionModeRadio = new System.Windows.Forms.RadioButton();
        _homeModeRadio = new System.Windows.Forms.RadioButton();
        _jogGroup = new System.Windows.Forms.GroupBox();
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
        _positionGroup = new System.Windows.Forms.GroupBox();
        _absoluteRadio = new System.Windows.Forms.RadioButton();
        _relativeRadio = new System.Windows.Forms.RadioButton();
        _targetLabel = new System.Windows.Forms.Label();
        _targetPositionBox = new System.Windows.Forms.TextBox();
        _targetUnitLabel = new System.Windows.Forms.Label();
        _moveButton = new System.Windows.Forms.Button();
        _homeGroup = new System.Windows.Forms.GroupBox();
        _homeButton = new System.Windows.Forms.Button();
        _jogBackwardButton = new System.Windows.Forms.Button();
        _jogForwardButton = new System.Windows.Forms.Button();
        _stopButton = new System.Windows.Forms.Button();
        _statusGroup = new System.Windows.Forms.GroupBox();
        _enabledIndicator = new System.Windows.Forms.Panel();
        _enabledLabel = new System.Windows.Forms.Label();
        _homedIndicator = new System.Windows.Forms.Panel();
        _homedLabel = new System.Windows.Forms.Label();
        _readyIndicator = new System.Windows.Forms.Panel();
        _readyLabel = new System.Windows.Forms.Label();
        _axisErrorIndicator = new System.Windows.Forms.Panel();
        _axisErrorLabel = new System.Windows.Forms.Label();
        _driveErrorIndicator = new System.Windows.Forms.Panel();
        _driveErrorLabel = new System.Windows.Forms.Label();
        _ackButton = new System.Windows.Forms.Button();
        _infoLabel = new System.Windows.Forms.Label();
        _infoBox = new System.Windows.Forms.TextBox();
        _currentGroup = new System.Windows.Forms.GroupBox();
        _positionLabel = new System.Windows.Forms.Label();
        _positionValueBox = new System.Windows.Forms.TextBox();
        _positionUnitLabel = new System.Windows.Forms.Label();
        _actualVelocityLabel = new System.Windows.Forms.Label();
        _actualVelocityBox = new System.Windows.Forms.TextBox();
        _actualVelocityUnitLabel = new System.Windows.Forms.Label();
        _errorLabel = new System.Windows.Forms.Label();
        _errorBox = new System.Windows.Forms.TextBox();
        _hintLabel = new System.Windows.Forms.Label();
        _headerPanel.SuspendLayout();
        _commandGroup.SuspendLayout();
        _jogGroup.SuspendLayout();
        _positionGroup.SuspendLayout();
        _homeGroup.SuspendLayout();
        _statusGroup.SuspendLayout();
        _currentGroup.SuspendLayout();
        SuspendLayout();

        _mainLabel.Location = new System.Drawing.Point(12, 22);
        _mainLabel.Size = new System.Drawing.Size(70, 24);
        _mainLabel.Text = "主控：";
        _mainLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

        _mainActivateButton.Location = new System.Drawing.Point(84, 16);
        _mainActivateButton.Size = new System.Drawing.Size(76, 30);
        _mainActivateButton.Text = "激活";
        _mainActivateButton.UseVisualStyleBackColor = true;

        _mainDeactivateButton.Location = new System.Drawing.Point(166, 16);
        _mainDeactivateButton.Size = new System.Drawing.Size(76, 30);
        _mainDeactivateButton.Text = "禁用";
        _mainDeactivateButton.UseVisualStyleBackColor = true;

        _axisLabel.Location = new System.Drawing.Point(282, 22);
        _axisLabel.Size = new System.Drawing.Size(36, 24);
        _axisLabel.Text = "轴：";
        _axisLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

        _axisEnableButton.Location = new System.Drawing.Point(320, 16);
        _axisEnableButton.Size = new System.Drawing.Size(76, 30);
        _axisEnableButton.Text = "启用";
        _axisEnableButton.UseVisualStyleBackColor = true;

        _axisDisableButton.Location = new System.Drawing.Point(402, 16);
        _axisDisableButton.Size = new System.Drawing.Size(76, 30);
        _axisDisableButton.Text = "禁用";
        _axisDisableButton.UseVisualStyleBackColor = true;

        _headerPanel.Controls.Add(_mainLabel);
        _headerPanel.Controls.Add(_mainActivateButton);
        _headerPanel.Controls.Add(_mainDeactivateButton);
        _headerPanel.Controls.Add(_axisLabel);
        _headerPanel.Controls.Add(_axisEnableButton);
        _headerPanel.Controls.Add(_axisDisableButton);
        _headerPanel.Location = new System.Drawing.Point(12, 10);
        _headerPanel.Size = new System.Drawing.Size(820, 60);

        _stripePanel.BackColor = System.Drawing.Color.FromArgb(255, 153, 0);
        _stripePanel.Location = new System.Drawing.Point(12, 78);
        _stripePanel.Size = new System.Drawing.Size(820, 14);

        _jogModeRadio.Checked = true;
        _jogModeRadio.Location = new System.Drawing.Point(16, 26);
        _jogModeRadio.Size = new System.Drawing.Size(104, 36);
        _jogModeRadio.TabStop = true;
        _jogModeRadio.Text = "点动";

        _positionModeRadio.Location = new System.Drawing.Point(16, 69);
        _positionModeRadio.Size = new System.Drawing.Size(104, 36);
        _positionModeRadio.Text = "定位";

        _homeModeRadio.Location = new System.Drawing.Point(16, 112);
        _homeModeRadio.Size = new System.Drawing.Size(104, 36);
        _homeModeRadio.Text = "回原点";

        _commandGroup.Controls.Add(_jogModeRadio);
        _commandGroup.Controls.Add(_positionModeRadio);
        _commandGroup.Controls.Add(_homeModeRadio);
        _commandGroup.Location = new System.Drawing.Point(20, 104);
        _commandGroup.Size = new System.Drawing.Size(500, 163);
        _commandGroup.Text = "命令";

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

        _jogGroup.Controls.Add(_velocityLabel);
        _jogGroup.Controls.Add(_velocityBox);
        _jogGroup.Controls.Add(_velocityUnitLabel);
        _jogGroup.Controls.Add(_accelLabel);
        _jogGroup.Controls.Add(_accelBox);
        _jogGroup.Controls.Add(_accelUnitLabel);
        _jogGroup.Controls.Add(_jerkLimitCheck);
        _jogGroup.Controls.Add(_jerkLabel);
        _jogGroup.Controls.Add(_jerkBox);
        _jogGroup.Controls.Add(_jerkUnitLabel);
        _jogGroup.Location = new System.Drawing.Point(20, 273);
        _jogGroup.Size = new System.Drawing.Size(500, 184);
        _jogGroup.Text = "点动";

        _absoluteRadio.Checked = true;
        _absoluteRadio.Location = new System.Drawing.Point(16, 18);
        _absoluteRadio.Size = new System.Drawing.Size(90, 22);
        _absoluteRadio.TabStop = true;
        _absoluteRadio.Text = "绝对";

        _relativeRadio.Location = new System.Drawing.Point(112, 18);
        _relativeRadio.Size = new System.Drawing.Size(90, 22);
        _relativeRadio.Text = "相对";

        _targetLabel.Location = new System.Drawing.Point(16, 52);
        _targetLabel.Size = new System.Drawing.Size(104, 22);
        _targetLabel.Text = "目标位置：";
        _targetLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        _targetPositionBox.Location = new System.Drawing.Point(126, 48);
        _targetPositionBox.Size = new System.Drawing.Size(100, 26);
        _targetPositionBox.Text = "0.000";
        _targetPositionBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

        _targetUnitLabel.Location = new System.Drawing.Point(230, 52);
        _targetUnitLabel.Size = new System.Drawing.Size(50, 22);
        _targetUnitLabel.Text = "mm";

        _moveButton.Location = new System.Drawing.Point(284, 44);
        _moveButton.Size = new System.Drawing.Size(100, 32);
        _moveButton.Text = "定位";
        _moveButton.UseVisualStyleBackColor = true;

        _positionGroup.Controls.Add(_absoluteRadio);
        _positionGroup.Controls.Add(_relativeRadio);
        _positionGroup.Controls.Add(_targetLabel);
        _positionGroup.Controls.Add(_targetPositionBox);
        _positionGroup.Controls.Add(_targetUnitLabel);
        _positionGroup.Controls.Add(_moveButton);
        _positionGroup.Location = new System.Drawing.Point(20, 273);
        _positionGroup.Size = new System.Drawing.Size(500, 110);
        _positionGroup.Text = "定位";
        _positionGroup.Visible = false;

        _homeButton.Location = new System.Drawing.Point(24, 30);
        _homeButton.Size = new System.Drawing.Size(120, 32);
        _homeButton.Text = "回原点";
        _homeButton.UseVisualStyleBackColor = true;

        _homeGroup.Controls.Add(_homeButton);
        _homeGroup.Location = new System.Drawing.Point(20, 273);
        _homeGroup.Size = new System.Drawing.Size(500, 90);
        _homeGroup.Text = "回原点";
        _homeGroup.Visible = false;

        _jogBackwardButton.Location = new System.Drawing.Point(20, 464);
        _jogBackwardButton.Size = new System.Drawing.Size(150, 36);
        _jogBackwardButton.Text = "反向";
        _jogBackwardButton.UseVisualStyleBackColor = true;

        _jogForwardButton.Location = new System.Drawing.Point(180, 464);
        _jogForwardButton.Size = new System.Drawing.Size(150, 36);
        _jogForwardButton.Text = "正向";
        _jogForwardButton.UseVisualStyleBackColor = true;

        _stopButton.Location = new System.Drawing.Point(20, 508);
        _stopButton.Size = new System.Drawing.Size(310, 32);
        _stopButton.Text = "停止";
        _stopButton.UseVisualStyleBackColor = true;

        _enabledIndicator.BackColor = System.Drawing.Color.DimGray;
        _enabledIndicator.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        _enabledIndicator.Location = new System.Drawing.Point(14, 30);
        _enabledIndicator.Size = new System.Drawing.Size(14, 14);

        _enabledLabel.Location = new System.Drawing.Point(34, 26);
        _enabledLabel.Size = new System.Drawing.Size(100, 22);
        _enabledLabel.Text = "已启用";

        _homedIndicator.BackColor = System.Drawing.Color.DimGray;
        _homedIndicator.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        _homedIndicator.Location = new System.Drawing.Point(14, 62);
        _homedIndicator.Size = new System.Drawing.Size(14, 14);

        _homedLabel.Location = new System.Drawing.Point(34, 58);
        _homedLabel.Size = new System.Drawing.Size(100, 22);
        _homedLabel.Text = "已归位";

        _readyIndicator.BackColor = System.Drawing.Color.DimGray;
        _readyIndicator.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        _readyIndicator.Location = new System.Drawing.Point(14, 94);
        _readyIndicator.Size = new System.Drawing.Size(14, 14);

        _readyLabel.Location = new System.Drawing.Point(34, 90);
        _readyLabel.Size = new System.Drawing.Size(100, 22);
        _readyLabel.Text = "就绪";

        _axisErrorIndicator.BackColor = System.Drawing.Color.DimGray;
        _axisErrorIndicator.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        _axisErrorIndicator.Location = new System.Drawing.Point(170, 30);
        _axisErrorIndicator.Size = new System.Drawing.Size(14, 14);

        _axisErrorLabel.Location = new System.Drawing.Point(190, 26);
        _axisErrorLabel.Size = new System.Drawing.Size(110, 22);
        _axisErrorLabel.Text = "轴错误";

        _driveErrorIndicator.BackColor = System.Drawing.Color.DimGray;
        _driveErrorIndicator.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        _driveErrorIndicator.Location = new System.Drawing.Point(170, 62);
        _driveErrorIndicator.Size = new System.Drawing.Size(14, 14);

        _driveErrorLabel.Location = new System.Drawing.Point(190, 58);
        _driveErrorLabel.Size = new System.Drawing.Size(110, 22);
        _driveErrorLabel.Text = "驱动装置错误";

        _ackButton.Location = new System.Drawing.Point(170, 100);
        _ackButton.Size = new System.Drawing.Size(90, 28);
        _ackButton.Text = "确认";
        _ackButton.UseVisualStyleBackColor = true;

        _statusGroup.Controls.Add(_enabledIndicator);
        _statusGroup.Controls.Add(_enabledLabel);
        _statusGroup.Controls.Add(_homedIndicator);
        _statusGroup.Controls.Add(_homedLabel);
        _statusGroup.Controls.Add(_readyIndicator);
        _statusGroup.Controls.Add(_readyLabel);
        _statusGroup.Controls.Add(_axisErrorIndicator);
        _statusGroup.Controls.Add(_axisErrorLabel);
        _statusGroup.Controls.Add(_driveErrorIndicator);
        _statusGroup.Controls.Add(_driveErrorLabel);
        _statusGroup.Controls.Add(_ackButton);
        _statusGroup.Location = new System.Drawing.Point(532, 104);
        _statusGroup.Size = new System.Drawing.Size(320, 140);
        _statusGroup.Text = "轴状态";

        _infoLabel.Location = new System.Drawing.Point(532, 273);
        _infoLabel.Size = new System.Drawing.Size(120, 22);
        _infoLabel.Text = "信息性消息：";

        _infoBox.BackColor = System.Drawing.SystemColors.Window;
        _infoBox.Location = new System.Drawing.Point(532, 299);
        _infoBox.Multiline = true;
        _infoBox.ReadOnly = true;
        _infoBox.Size = new System.Drawing.Size(320, 56);

        _positionLabel.Location = new System.Drawing.Point(14, 34);
        _positionLabel.Size = new System.Drawing.Size(72, 22);
        _positionLabel.Text = "位置：";
        _positionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        _positionValueBox.BackColor = System.Drawing.Color.FromArgb(255, 255, 225);
        _positionValueBox.Location = new System.Drawing.Point(94, 30);
        _positionValueBox.ReadOnly = true;
        _positionValueBox.Size = new System.Drawing.Size(120, 26);
        _positionValueBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

        _positionUnitLabel.Location = new System.Drawing.Point(222, 34);
        _positionUnitLabel.Size = new System.Drawing.Size(60, 22);
        _positionUnitLabel.Text = "mm";

        _actualVelocityLabel.Location = new System.Drawing.Point(14, 76);
        _actualVelocityLabel.Size = new System.Drawing.Size(72, 22);
        _actualVelocityLabel.Text = "速度：";
        _actualVelocityLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;

        _actualVelocityBox.BackColor = System.Drawing.Color.FromArgb(255, 255, 225);
        _actualVelocityBox.Location = new System.Drawing.Point(94, 72);
        _actualVelocityBox.ReadOnly = true;
        _actualVelocityBox.Size = new System.Drawing.Size(120, 26);
        _actualVelocityBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;

        _actualVelocityUnitLabel.Location = new System.Drawing.Point(222, 76);
        _actualVelocityUnitLabel.Size = new System.Drawing.Size(80, 22);
        _actualVelocityUnitLabel.Text = "mm/s";

        _currentGroup.Controls.Add(_positionLabel);
        _currentGroup.Controls.Add(_positionValueBox);
        _currentGroup.Controls.Add(_positionUnitLabel);
        _currentGroup.Controls.Add(_actualVelocityLabel);
        _currentGroup.Controls.Add(_actualVelocityBox);
        _currentGroup.Controls.Add(_actualVelocityUnitLabel);
        _currentGroup.Location = new System.Drawing.Point(532, 361);
        _currentGroup.Size = new System.Drawing.Size(320, 120);
        _currentGroup.Text = "当前值";

        _errorLabel.Location = new System.Drawing.Point(20, 554);
        _errorLabel.Size = new System.Drawing.Size(100, 22);
        _errorLabel.Text = "错误消息：";

        _errorBox.BackColor = System.Drawing.SystemColors.Window;
        _errorBox.Location = new System.Drawing.Point(20, 580);
        _errorBox.Multiline = true;
        _errorBox.ReadOnly = true;
        _errorBox.Size = new System.Drawing.Size(820, 56);

        _hintLabel.ForeColor = System.Drawing.Color.FromArgb(150, 90, 0);
        _hintLabel.Location = new System.Drawing.Point(20, 662);
        _hintLabel.Size = new System.Drawing.Size(820, 22);

        AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        Controls.Add(_headerPanel);
        Controls.Add(_stripePanel);
        Controls.Add(_commandGroup);
        Controls.Add(_jogGroup);
        Controls.Add(_positionGroup);
        Controls.Add(_homeGroup);
        Controls.Add(_jogBackwardButton);
        Controls.Add(_jogForwardButton);
        Controls.Add(_stopButton);
        Controls.Add(_statusGroup);
        Controls.Add(_infoLabel);
        Controls.Add(_infoBox);
        Controls.Add(_currentGroup);
        Controls.Add(_errorLabel);
        Controls.Add(_errorBox);
        Controls.Add(_hintLabel);
        Name = "AxisOperationPanel";
        Size = new System.Drawing.Size(860, 690);
        _headerPanel.ResumeLayout(false);
        _commandGroup.ResumeLayout(false);
        _jogGroup.ResumeLayout(false);
        _jogGroup.PerformLayout();
        _positionGroup.ResumeLayout(false);
        _positionGroup.PerformLayout();
        _homeGroup.ResumeLayout(false);
        _statusGroup.ResumeLayout(false);
        _currentGroup.ResumeLayout(false);
        _currentGroup.PerformLayout();
        ResumeLayout(false);
    }
}
