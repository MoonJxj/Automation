using EquipmentMonitor.Desktop.Controls;
using EquipmentMonitor.Desktop.Parameter;

namespace EquipmentMonitor.Desktop.Forms;

partial class ParameterSettingsPage
{
    private System.Windows.Forms.Panel _toolbar = null!;
    private System.Windows.Forms.Button _reloadButton = null!;
    private System.Windows.Forms.Button _saveButton = null!;
    private System.Windows.Forms.ComboBox _modeCombo = null!;
    private System.Windows.Forms.Label _banner = null!;
    private System.Windows.Forms.SplitContainer _split = null!;
    private System.Windows.Forms.ListBox _elementList = null!;
    private System.Windows.Forms.Panel _editorHost = null!;
    private ParameterEditorControl _editor = null!;

    private void InitializeComponent()
    {
        _toolbar = new System.Windows.Forms.Panel();
        _reloadButton = new System.Windows.Forms.Button();
        _saveButton = new System.Windows.Forms.Button();
        _modeCombo = new System.Windows.Forms.ComboBox();
        _banner = new System.Windows.Forms.Label();
        _split = new System.Windows.Forms.SplitContainer();
        _elementList = new System.Windows.Forms.ListBox();
        _editorHost = new System.Windows.Forms.Panel();
        _editor = new ParameterEditorControl();
        _toolbar.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_split).BeginInit();
        _split.Panel1.SuspendLayout();
        _split.Panel2.SuspendLayout();
        _split.SuspendLayout();
        SuspendLayout();

        _reloadButton.Location = new System.Drawing.Point(8, 8);
        _reloadButton.Size = new System.Drawing.Size(100, 32);
        _reloadButton.Text = "重新加载";
        _reloadButton.UseVisualStyleBackColor = true;

        _saveButton.Location = new System.Drawing.Point(116, 8);
        _saveButton.Size = new System.Drawing.Size(110, 32);
        _saveButton.Text = "保存";
        _saveButton.UseVisualStyleBackColor = true;

        var modeLabel = new System.Windows.Forms.Label
        {
            Text = "更新方式",
            Location = new System.Drawing.Point(244, 14),
            Size = new System.Drawing.Size(70, 22),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };

        _modeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        _modeCombo.Location = new System.Drawing.Point(318, 10);
        _modeCombo.Size = new System.Drawing.Size(170, 26);

        _toolbar.Controls.Add(_reloadButton);
        _toolbar.Controls.Add(_saveButton);
        _toolbar.Controls.Add(modeLabel);
        _toolbar.Controls.Add(_modeCombo);
        _toolbar.Dock = System.Windows.Forms.DockStyle.Top;
        _toolbar.Size = new System.Drawing.Size(1000, 48);

        _banner.Dock = System.Windows.Forms.DockStyle.Top;
        _banner.ForeColor = System.Drawing.Color.FromArgb(150, 90, 0);
        _banner.Padding = new System.Windows.Forms.Padding(10, 4, 10, 4);
        _banner.Size = new System.Drawing.Size(1000, 34);
        _banner.Text = string.Empty;
        _banner.Visible = false;

        _elementList.Dock = System.Windows.Forms.DockStyle.Fill;
        _elementList.IntegralHeight = false;
        _elementList.ItemHeight = 24;
        _elementList.DisplayMember = nameof(ParameterTarget.DisplayName);

        _split.Panel1.Controls.Add(_elementList);
        _split.Dock = System.Windows.Forms.DockStyle.Fill;
        _split.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
        _split.IsSplitterFixed = true;
        _split.Panel2.Controls.Add(_editorHost);
        _split.Size = new System.Drawing.Size(1000, 518);
        _split.SplitterDistance = 250;
        _split.SplitterWidth = 4;

        _editorHost.Dock = System.Windows.Forms.DockStyle.Fill;
        _editorHost.Padding = new System.Windows.Forms.Padding(4);
        _editor.Dock = System.Windows.Forms.DockStyle.Fill;
        _editorHost.Controls.Add(_editor);

        ClientSize = new System.Drawing.Size(1000, 600);
        Controls.Add(_split);
        Controls.Add(_banner);
        Controls.Add(_toolbar);
        Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        Name = "ParameterSettingsPage";
        Text = "参数设置";
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        _toolbar.ResumeLayout(false);
        _split.Panel1.ResumeLayout(false);
        _split.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_split).EndInit();
        _split.ResumeLayout(false);
        ResumeLayout(false);
    }
}
