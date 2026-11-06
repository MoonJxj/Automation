namespace EquipmentMonitor.Desktop.Controls;

partial class CylinderOperationPanel
{
    private System.Windows.Forms.Label _titleLabel = null!;
    private System.Windows.Forms.Label _subtitleLabel = null!;
    private System.Windows.Forms.Panel _stripePanel = null!;

    private System.Windows.Forms.GroupBox _statusGroup = null!;
    private System.Windows.Forms.Panel _extendedIndicator = null!;
    private System.Windows.Forms.Label _extendedLabel = null!;
    private System.Windows.Forms.Panel _retractedIndicator = null!;
    private System.Windows.Forms.Label _retractedLabel = null!;
    private System.Windows.Forms.Panel _movingIndicator = null!;
    private System.Windows.Forms.Label _movingLabel = null!;
    private System.Windows.Forms.Panel _faultIndicator = null!;
    private System.Windows.Forms.Label _faultLabel = null!;

    private System.Windows.Forms.GroupBox _sensorGroup = null!;
    private System.Windows.Forms.Label _extendedSensorLabel = null!;
    private System.Windows.Forms.Label _retractedSensorLabel = null!;

    private System.Windows.Forms.Button _extendButton = null!;
    private System.Windows.Forms.Button _retractButton = null!;
    private System.Windows.Forms.Button _haltButton = null!;
    private System.Windows.Forms.Button _resetButton = null!;

    private System.Windows.Forms.Label _infoLabel = null!;
    private System.Windows.Forms.TextBox _infoBox = null!;
    private System.Windows.Forms.Label _errorLabel = null!;
    private System.Windows.Forms.TextBox _errorBox = null!;
    private System.Windows.Forms.Label _hintLabel = null!;

    private void InitializeComponent()
    {
        this._titleLabel = new System.Windows.Forms.Label();
        this._subtitleLabel = new System.Windows.Forms.Label();
        this._stripePanel = new System.Windows.Forms.Panel();
        this._statusGroup = new System.Windows.Forms.GroupBox();
        this._extendedIndicator = new System.Windows.Forms.Panel();
        this._extendedLabel = new System.Windows.Forms.Label();
        this._retractedIndicator = new System.Windows.Forms.Panel();
        this._retractedLabel = new System.Windows.Forms.Label();
        this._movingIndicator = new System.Windows.Forms.Panel();
        this._movingLabel = new System.Windows.Forms.Label();
        this._faultIndicator = new System.Windows.Forms.Panel();
        this._faultLabel = new System.Windows.Forms.Label();
        this._sensorGroup = new System.Windows.Forms.GroupBox();
        this._extendedSensorLabel = new System.Windows.Forms.Label();
        this._retractedSensorLabel = new System.Windows.Forms.Label();
        this._extendButton = new System.Windows.Forms.Button();
        this._retractButton = new System.Windows.Forms.Button();
        this._haltButton = new System.Windows.Forms.Button();
        this._resetButton = new System.Windows.Forms.Button();
        this._infoLabel = new System.Windows.Forms.Label();
        this._infoBox = new System.Windows.Forms.TextBox();
        this._errorLabel = new System.Windows.Forms.Label();
        this._errorBox = new System.Windows.Forms.TextBox();
        this._hintLabel = new System.Windows.Forms.Label();
        this._statusGroup.SuspendLayout();
        this._sensorGroup.SuspendLayout();
        this.SuspendLayout();

        this._titleLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
        this._titleLabel.Location = new System.Drawing.Point(16, 12);
        this._titleLabel.Name = "_titleLabel";
        this._titleLabel.Size = new System.Drawing.Size(400, 28);
        this._titleLabel.Text = "气缸";

        this._subtitleLabel.ForeColor = System.Drawing.SystemColors.GrayText;
        this._subtitleLabel.Location = new System.Drawing.Point(16, 42);
        this._subtitleLabel.Name = "_subtitleLabel";
        this._subtitleLabel.Size = new System.Drawing.Size(600, 22);
        this._subtitleLabel.Text = "双控 · 动作超时 2.0s";

        this._stripePanel.BackColor = System.Drawing.Color.FromArgb(255, 153, 0);
        this._stripePanel.Location = new System.Drawing.Point(12, 70);
        this._stripePanel.Name = "_stripePanel";
        this._stripePanel.Size = new System.Drawing.Size(616, 10);

        this._extendedIndicator.BackColor = System.Drawing.Color.DimGray;
        this._extendedIndicator.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this._extendedIndicator.Location = new System.Drawing.Point(16, 30);
        this._extendedIndicator.Name = "_extendedIndicator";
        this._extendedIndicator.Size = new System.Drawing.Size(14, 14);

        this._extendedLabel.Location = new System.Drawing.Point(36, 26);
        this._extendedLabel.Name = "_extendedLabel";
        this._extendedLabel.Size = new System.Drawing.Size(100, 22);
        this._extendedLabel.Text = "伸出到位";

        this._retractedIndicator.BackColor = System.Drawing.Color.DimGray;
        this._retractedIndicator.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this._retractedIndicator.Location = new System.Drawing.Point(160, 30);
        this._retractedIndicator.Name = "_retractedIndicator";
        this._retractedIndicator.Size = new System.Drawing.Size(14, 14);

        this._retractedLabel.Location = new System.Drawing.Point(180, 26);
        this._retractedLabel.Name = "_retractedLabel";
        this._retractedLabel.Size = new System.Drawing.Size(100, 22);
        this._retractedLabel.Text = "缩回到位";

        this._movingIndicator.BackColor = System.Drawing.Color.DimGray;
        this._movingIndicator.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this._movingIndicator.Location = new System.Drawing.Point(306, 30);
        this._movingIndicator.Name = "_movingIndicator";
        this._movingIndicator.Size = new System.Drawing.Size(14, 14);

        this._movingLabel.Location = new System.Drawing.Point(326, 26);
        this._movingLabel.Name = "_movingLabel";
        this._movingLabel.Size = new System.Drawing.Size(100, 22);
        this._movingLabel.Text = "动作中";

        this._faultIndicator.BackColor = System.Drawing.Color.DimGray;
        this._faultIndicator.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this._faultIndicator.Location = new System.Drawing.Point(452, 30);
        this._faultIndicator.Name = "_faultIndicator";
        this._faultIndicator.Size = new System.Drawing.Size(14, 14);

        this._faultLabel.Location = new System.Drawing.Point(472, 26);
        this._faultLabel.Name = "_faultLabel";
        this._faultLabel.Size = new System.Drawing.Size(100, 22);
        this._faultLabel.Text = "故障";

        this._statusGroup.Controls.Add(this._extendedIndicator);
        this._statusGroup.Controls.Add(this._extendedLabel);
        this._statusGroup.Controls.Add(this._retractedIndicator);
        this._statusGroup.Controls.Add(this._retractedLabel);
        this._statusGroup.Controls.Add(this._movingIndicator);
        this._statusGroup.Controls.Add(this._movingLabel);
        this._statusGroup.Controls.Add(this._faultIndicator);
        this._statusGroup.Controls.Add(this._faultLabel);
        this._statusGroup.Location = new System.Drawing.Point(20, 92);
        this._statusGroup.Name = "_statusGroup";
        this._statusGroup.Size = new System.Drawing.Size(600, 78);
        this._statusGroup.Text = "气缸状态";

        this._extendedSensorLabel.Location = new System.Drawing.Point(12, 24);
        this._extendedSensorLabel.Name = "_extendedSensorLabel";
        this._extendedSensorLabel.Size = new System.Drawing.Size(240, 22);
        this._extendedSensorLabel.Text = "伸出限位：OFF";

        this._retractedSensorLabel.Location = new System.Drawing.Point(12, 46);
        this._retractedSensorLabel.Name = "_retractedSensorLabel";
        this._retractedSensorLabel.Size = new System.Drawing.Size(240, 22);
        this._retractedSensorLabel.Text = "缩回限位：ON";

        this._sensorGroup.Controls.Add(this._extendedSensorLabel);
        this._sensorGroup.Controls.Add(this._retractedSensorLabel);
        this._sensorGroup.Location = new System.Drawing.Point(20, 184);
        this._sensorGroup.Name = "_sensorGroup";
        this._sensorGroup.Size = new System.Drawing.Size(280, 74);
        this._sensorGroup.Text = "限位反馈";

        this._extendButton.Location = new System.Drawing.Point(20, 276);
        this._extendButton.Name = "_extendButton";
        this._extendButton.Size = new System.Drawing.Size(140, 42);
        this._extendButton.Text = "伸出";
        this._extendButton.UseVisualStyleBackColor = true;

        this._retractButton.Location = new System.Drawing.Point(176, 276);
        this._retractButton.Name = "_retractButton";
        this._retractButton.Size = new System.Drawing.Size(140, 42);
        this._retractButton.Text = "缩回";
        this._retractButton.UseVisualStyleBackColor = true;

        this._haltButton.Location = new System.Drawing.Point(332, 276);
        this._haltButton.Name = "_haltButton";
        this._haltButton.Size = new System.Drawing.Size(140, 42);
        this._haltButton.Text = "断阀";
        this._haltButton.UseVisualStyleBackColor = true;

        this._resetButton.Location = new System.Drawing.Point(488, 276);
        this._resetButton.Name = "_resetButton";
        this._resetButton.Size = new System.Drawing.Size(132, 42);
        this._resetButton.Text = "复位";
        this._resetButton.UseVisualStyleBackColor = true;

        this._infoLabel.Location = new System.Drawing.Point(20, 334);
        this._infoLabel.Name = "_infoLabel";
        this._infoLabel.Size = new System.Drawing.Size(120, 22);
        this._infoLabel.Text = "信息性消息：";

        this._infoBox.BackColor = System.Drawing.SystemColors.Window;
        this._infoBox.Location = new System.Drawing.Point(20, 358);
        this._infoBox.Multiline = true;
        this._infoBox.Name = "_infoBox";
        this._infoBox.ReadOnly = true;
        this._infoBox.Size = new System.Drawing.Size(600, 52);

        this._errorLabel.Location = new System.Drawing.Point(20, 420);
        this._errorLabel.Name = "_errorLabel";
        this._errorLabel.Size = new System.Drawing.Size(100, 22);
        this._errorLabel.Text = "错误消息：";

        this._errorBox.BackColor = System.Drawing.SystemColors.Window;
        this._errorBox.Location = new System.Drawing.Point(20, 444);
        this._errorBox.Multiline = true;
        this._errorBox.Name = "_errorBox";
        this._errorBox.ReadOnly = true;
        this._errorBox.Size = new System.Drawing.Size(600, 52);

        this._hintLabel.ForeColor = System.Drawing.Color.FromArgb(150, 90, 0);
        this._hintLabel.Location = new System.Drawing.Point(20, 506);
        this._hintLabel.Name = "_hintLabel";
        this._hintLabel.Size = new System.Drawing.Size(600, 22);

        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        this.Controls.Add(this._titleLabel);
        this.Controls.Add(this._subtitleLabel);
        this.Controls.Add(this._stripePanel);
        this.Controls.Add(this._statusGroup);
        this.Controls.Add(this._sensorGroup);
        this.Controls.Add(this._extendButton);
        this.Controls.Add(this._retractButton);
        this.Controls.Add(this._haltButton);
        this.Controls.Add(this._resetButton);
        this.Controls.Add(this._infoLabel);
        this.Controls.Add(this._infoBox);
        this.Controls.Add(this._errorLabel);
        this.Controls.Add(this._errorBox);
        this.Controls.Add(this._hintLabel);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.Name = "CylinderOperationPanel";
        this.Size = new System.Drawing.Size(640, 540);
        this._statusGroup.ResumeLayout(false);
        this._sensorGroup.ResumeLayout(false);
        this.ResumeLayout(false);
    }
}
