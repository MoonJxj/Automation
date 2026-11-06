namespace EquipmentMonitor.Desktop.Forms;

partial class UserManagementPage
{
    private System.Windows.Forms.FlowLayoutPanel _toolbar = null!;
    private System.Windows.Forms.Button _createButton = null!;
    private System.Windows.Forms.Button _editButton = null!;
    private System.Windows.Forms.Button _toggleButton = null!;
    private System.Windows.Forms.Button _passwordButton = null!;
    private System.Windows.Forms.Button _deleteButton = null!;
    private System.Windows.Forms.Button _refreshButton = null!;
    private AntdUI.Table _table = null!;
    private AntdUI.Label _hint = null!;

    /// <summary>设计器生成的界面初始化。运行期由构造函数调用。</summary>
    private void InitializeComponent()
    {
        this._toolbar = new System.Windows.Forms.FlowLayoutPanel();
        this._createButton = new System.Windows.Forms.Button();
        this._editButton = new System.Windows.Forms.Button();
        this._toggleButton = new System.Windows.Forms.Button();
        this._passwordButton = new System.Windows.Forms.Button();
        this._deleteButton = new System.Windows.Forms.Button();
        this._refreshButton = new System.Windows.Forms.Button();
        this._table = new AntdUI.Table();
        this._hint = new AntdUI.Label();
        this._toolbar.SuspendLayout();
        this.SuspendLayout();
        //
        // _createButton
        //
        this._createButton.Location = new System.Drawing.Point(8, 6);
        this._createButton.Margin = new System.Windows.Forms.Padding(4);
        this._createButton.Name = "_createButton";
        this._createButton.Size = new System.Drawing.Size(100, 32);
        this._createButton.TabIndex = 0;
        this._createButton.Text = "新建用户";
        this._createButton.UseVisualStyleBackColor = true;
        //
        // _editButton
        //
        this._editButton.Location = new System.Drawing.Point(116, 6);
        this._editButton.Margin = new System.Windows.Forms.Padding(4);
        this._editButton.Name = "_editButton";
        this._editButton.Size = new System.Drawing.Size(90, 32);
        this._editButton.TabIndex = 1;
        this._editButton.Text = "编辑";
        this._editButton.UseVisualStyleBackColor = true;
        //
        // _toggleButton
        //
        this._toggleButton.Location = new System.Drawing.Point(214, 6);
        this._toggleButton.Margin = new System.Windows.Forms.Padding(4);
        this._toggleButton.Name = "_toggleButton";
        this._toggleButton.Size = new System.Drawing.Size(100, 32);
        this._toggleButton.TabIndex = 2;
        this._toggleButton.Text = "停用账号";
        this._toggleButton.UseVisualStyleBackColor = true;
        //
        // _passwordButton
        //
        this._passwordButton.Location = new System.Drawing.Point(322, 6);
        this._passwordButton.Margin = new System.Windows.Forms.Padding(4);
        this._passwordButton.Name = "_passwordButton";
        this._passwordButton.Size = new System.Drawing.Size(100, 32);
        this._passwordButton.TabIndex = 3;
        this._passwordButton.Text = "设置口令";
        this._passwordButton.UseVisualStyleBackColor = true;
        //
        // _deleteButton
        //
        this._deleteButton.Location = new System.Drawing.Point(430, 6);
        this._deleteButton.Margin = new System.Windows.Forms.Padding(4);
        this._deleteButton.Name = "_deleteButton";
        this._deleteButton.Size = new System.Drawing.Size(90, 32);
        this._deleteButton.TabIndex = 4;
        this._deleteButton.Text = "删除账号";
        this._deleteButton.UseVisualStyleBackColor = true;
        //
        // _refreshButton
        //
        this._refreshButton.Location = new System.Drawing.Point(528, 6);
        this._refreshButton.Margin = new System.Windows.Forms.Padding(4);
        this._refreshButton.Name = "_refreshButton";
        this._refreshButton.Size = new System.Drawing.Size(80, 32);
        this._refreshButton.TabIndex = 5;
        this._refreshButton.Text = "刷新";
        this._refreshButton.UseVisualStyleBackColor = true;
        //
        // _toolbar
        //
        this._toolbar.Controls.Add(this._createButton);
        this._toolbar.Controls.Add(this._editButton);
        this._toolbar.Controls.Add(this._toggleButton);
        this._toolbar.Controls.Add(this._passwordButton);
        this._toolbar.Controls.Add(this._deleteButton);
        this._toolbar.Controls.Add(this._refreshButton);
        this._toolbar.Dock = System.Windows.Forms.DockStyle.Top;
        this._toolbar.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
        this._toolbar.Location = new System.Drawing.Point(0, 0);
        this._toolbar.Name = "_toolbar";
        this._toolbar.Padding = new System.Windows.Forms.Padding(4);
        this._toolbar.Size = new System.Drawing.Size(1000, 44);
        this._toolbar.TabIndex = 0;
        this._toolbar.WrapContents = false;
        //
        // _table
        //
        this._table.Dock = System.Windows.Forms.DockStyle.Fill;
        this._table.Location = new System.Drawing.Point(0, 44);
        this._table.Name = "_table";
        this._table.Size = new System.Drawing.Size(1000, 528);
        this._table.TabIndex = 1;
        //
        // _hint
        //
        this._hint.Dock = System.Windows.Forms.DockStyle.Bottom;
        this._hint.Location = new System.Drawing.Point(0, 572);
        this._hint.Name = "_hint";
        this._hint.Size = new System.Drawing.Size(1000, 28);
        this._hint.TabIndex = 2;
        this._hint.Text = "正在加载账号…";
        //
        // UserManagementPage
        //
        this.ClientSize = new System.Drawing.Size(1000, 600);
        this.Controls.Add(this._table);
        this.Controls.Add(this._hint);
        this.Controls.Add(this._toolbar);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.Name = "UserManagementPage";
        this.Text = "用户管理";
        this._toolbar.ResumeLayout(false);
        this.ResumeLayout(false);
    }
}
