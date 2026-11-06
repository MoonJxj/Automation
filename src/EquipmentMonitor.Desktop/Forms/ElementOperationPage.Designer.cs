using EquipmentMonitor.Desktop.Controls;

namespace EquipmentMonitor.Desktop.Forms;

partial class ElementOperationPage
{
    private AxisOperationPanel _panel = null!;

    private void InitializeComponent()
    {
        this._panel = new AxisOperationPanel();
        this.SuspendLayout();

        this._panel.Dock = System.Windows.Forms.DockStyle.Fill;
        this._panel.Location = new System.Drawing.Point(8, 8);
        this._panel.Name = "_panel";
        this._panel.Size = new System.Drawing.Size(860, 690);
        this._panel.TabIndex = 0;

        this.ClientSize = new System.Drawing.Size(876, 706);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        this.Controls.Add(this._panel);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "ElementOperationPage";
        this.Padding = new System.Windows.Forms.Padding(8);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "操作台";
        this.ResumeLayout(false);
    }
}
