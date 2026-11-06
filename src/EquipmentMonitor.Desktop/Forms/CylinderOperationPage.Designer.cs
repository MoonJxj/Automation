using EquipmentMonitor.Desktop.Controls;

namespace EquipmentMonitor.Desktop.Forms;

partial class CylinderOperationPage
{
    private CylinderOperationPanel _panel = null!;

    private void InitializeComponent()
    {
        this._panel = new CylinderOperationPanel();
        this.SuspendLayout();

        this._panel.Dock = System.Windows.Forms.DockStyle.Fill;
        this._panel.Location = new System.Drawing.Point(8, 8);
        this._panel.Name = "_panel";
        this._panel.Size = new System.Drawing.Size(640, 540);
        this._panel.TabIndex = 0;

        this.ClientSize = new System.Drawing.Size(656, 556);
        this.Controls.Add(this._panel);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "CylinderOperationPage";
        this.Padding = new System.Windows.Forms.Padding(8);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "气缸操作台";
        this.ResumeLayout(false);
    }
}
