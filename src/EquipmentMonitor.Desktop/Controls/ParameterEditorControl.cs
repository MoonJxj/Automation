using System.Globalization;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Desktop.Controls;

/// <summary>
/// 通用参数编辑器：按 <see cref="ElementParameterDescriptor"/> 自动生成控件，不硬编码轴/气缸字段。
/// </summary>
/// <remarks>
/// <para><b>这是参数页模板的核心</b>：描述符来自 Core 目录；新增参数只要扩展目录，
/// 本控件不增加字段。</para>
/// <para><b>职责边界</b>：本控件只收集值、做单参数类型/范围校验、标记脏状态；
/// 权限校验、跨字段校验、保存/下发/审计由 <c>IParameterPageSource</c> 实现负责。</para>
/// </remarks>
public sealed partial class ParameterEditorControl : UserControl
{
    private readonly List<Row> _rows = [];

    private IPermissionService? _permissions;
    private ElementParameterSnapshot? _snapshot;
    private ElementParameterSet? _original;
    private bool _loading;

    /// <summary>设计器专用构造。</summary>
    public ParameterEditorControl()
    {
        InitializeComponent();
    }

    /// <summary>脏状态变化。</summary>
    public event EventHandler? DirtyChanged;

    /// <summary>当前是否有未保存修改。</summary>
    public bool IsDirty { get; private set; }

    /// <summary>当前是否至少有一个可编辑字段。</summary>
    public bool CanEdit { get; private set; }

    /// <summary>注入权限服务；未注入时全部按不可编辑处理。</summary>
    public void AttachPermissions(IPermissionService permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        _permissions = permissions;

        if (_snapshot is not null)
        {
            LoadSnapshot(_snapshot);
        }
    }

    /// <summary>按快照重建参数区。</summary>
    public void LoadSnapshot(ElementParameterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _loading = true;

        try
        {
            _snapshot = snapshot;
            _original = snapshot.Values;

            _titleLabel.Text = snapshot.DisplayName;
            _subtitleLabel.Text = $"{snapshot.ElementId} · 读取于 {snapshot.ReadAt.ToLocalTime():HH:mm:ss}";

            ClearRows();

            var groups = snapshot.Descriptors
                .GroupBy(descriptor => string.IsNullOrWhiteSpace(descriptor.Group) ? "参数" : descriptor.Group)
                .ToArray();

            CanEdit = false;

            foreach (var group in groups)
            {
                var groupBox = new GroupBox
                {
                    Text = group.Key,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    MinimumSize = new Size(600, 0),
                    Padding = new Padding(8),
                    Margin = new Padding(4, 4, 4, 8)
                };

                var table = new TableLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    MinimumSize = new Size(590, 0),
                    ColumnCount = 4,
                    Dock = DockStyle.Top,
                    Width = 590
                };

                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

                foreach (var descriptor in group)
                {
                    AddRow(table, descriptor);
                }

                groupBox.Controls.Add(table);
                _groups.Controls.Add(groupBox);
            }

            SetDirty(false);
            SetStatus(string.Empty);
            ResizeGroups();
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>按当前控件构造参数集；单参数非法时返回 <c>false</c> 并给出原因。</summary>
    public bool TryBuildSet(out ElementParameterSet? values, out string error)
    {
        values = null;
        error = string.Empty;

        if (_original is null)
        {
            error = "尚未加载参数。";
            return false;
        }

        var current = _original;

        foreach (var row in _rows)
        {
            if (!row.TryRead(out var value, out var rowError))
            {
                error = rowError;
                return false;
            }

            current = value is null
                ? current.WithoutValue(row.Descriptor.Key)
                : current.WithValue(row.Descriptor.Key, value.Value);
        }

        values = current;
        return true;
    }

    /// <summary>保存成功后调用：把当前值作为新的基线。</summary>
    public void MarkSaved()
    {
        if (!TryBuildSet(out var values, out _) || values is null)
        {
            return;
        }

        _original = values;

        if (_snapshot is not null)
        {
            _snapshot = _snapshot with { Values = values };
        }

        SetDirty(false);
    }

    /// <summary>设置底部状态文本；<paramref name="isError"/> 为 true 时显示红色。</summary>
    public void SetStatus(string text, bool isError = false)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = isError ? Color.Firebrick : SystemColors.GrayText;
    }

    /// <summary>当前行数（测试用）。</summary>
    internal int RowCount => _rows.Count;

    /// <summary>当前可编辑行数（测试用）。</summary>
    internal int EditableRowCount => _rows.Count(row => row.Editor.Enabled);

    /// <summary>按参数键设置编辑值（测试用）。</summary>
    internal bool SetEditorValueForTest(string key, string text)
    {
        var row = _rows.FirstOrDefault(item => item.Descriptor.Key == key);

        if (row is null)
        {
            return false;
        }

        if (row.Editor is CheckBox check)
        {
            check.Checked = bool.TryParse(text, out var flag) && flag;
            return true;
        }

        if (row.Editor is ComboBox combo)
        {
            combo.SelectedItem = text;
            return true;
        }

        row.Editor.Text = text;
        return true;
    }

    private void AddRow(TableLayoutPanel table, ElementParameterDescriptor descriptor)
    {
        var label = new Label
        {
            Text = descriptor.DisplayName,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Height = 26,
            Dock = DockStyle.Fill
        };

        if (!string.IsNullOrWhiteSpace(descriptor.Description))
        {
            var tip = new ToolTip();
            tip.SetToolTip(label, descriptor.Description);
        }

        var editor = CreateEditor(descriptor, out var read);
        editor.Dock = DockStyle.Fill;

        var unit = new Label
        {
            Text = descriptor.Unit ?? string.Empty,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Height = 26,
            Dock = DockStyle.Fill
        };

        var apply = new Label
        {
            Text = descriptor.IsReadOnly ? "只读" : ApplyText(descriptor.ApplyMode),
            ForeColor = descriptor.IsReadOnly ? SystemColors.GrayText : SystemColors.ControlText,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Height = 26,
            Dock = DockStyle.Fill
        };

        var editable = !descriptor.IsReadOnly
                       && (_permissions?.HasPermission(descriptor.RequiredPermission) ?? false);

        editor.Enabled = editable;
        CanEdit |= editable;

        table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        table.Controls.Add(label, 0, table.RowCount - 1);
        table.Controls.Add(editor, 1, table.RowCount - 1);
        table.Controls.Add(unit, 2, table.RowCount - 1);
        table.Controls.Add(apply, 3, table.RowCount - 1);

        _rows.Add(new Row(descriptor, editor, read));
    }

    private Control CreateEditor(ElementParameterDescriptor descriptor, out Func<(CommandValue? Value, string Error)> read)
    {
        var value = _original is not null && _original.TryGetValue(descriptor.Key, out var existing)
            ? existing
            : CommandValue.None;

        switch (descriptor.ValueKind)
        {
            case CommandValueKind.Bool:
            {
                var check = new CheckBox
                {
                    Text = string.Empty,
                    Checked = value.TryAsBool(out var flag) && flag,
                    AutoSize = false,
                    Height = 26
                };

                read = () => (CommandValue.FromBool(check.Checked), string.Empty);
                check.CheckedChanged += (_, _) => OnEdited();
                return check;
            }

            case CommandValueKind.Text when descriptor.Choices.Count > 0:
            {
                var combo = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Height = 26
                };

                combo.Items.AddRange(descriptor.Choices.Cast<object>().ToArray());

                if (value.TryAsText(out var text) && descriptor.Choices.Contains(text, StringComparer.Ordinal))
                {
                    combo.SelectedItem = text;
                }
                else if (combo.Items.Count > 0)
                {
                    combo.SelectedIndex = 0;
                }

                read = () => combo.SelectedItem is string selected
                    ? (CommandValue.FromText(selected), string.Empty)
                    : ((CommandValue?)null, string.Empty);

                combo.SelectedIndexChanged += (_, _) => OnEdited();
                return combo;
            }

            case CommandValueKind.Text:
            {
                var box = new TextBox
                {
                    Height = 26,
                    Text = value.TryAsText(out var text) ? text : string.Empty
                };

                read = () =>
                {
                    if (string.IsNullOrWhiteSpace(box.Text))
                    {
                        return ((CommandValue?)null, string.Empty);
                    }

                    return (CommandValue.FromText(box.Text.Trim()), string.Empty);
                };

                box.TextChanged += (_, _) => OnEdited();
                return box;
            }

            default:
            {
                var box = new TextBox
                {
                    Height = 26,
                    TextAlign = HorizontalAlignment.Right,
                    Text = value.TryAsNumber(out var number)
                        ? number.ToString($"F{descriptor.Precision ?? 3}", CultureInfo.CurrentCulture)
                        : string.Empty
                };

                read = () =>
                {
                    if (string.IsNullOrWhiteSpace(box.Text))
                    {
                        return ((CommandValue?)null, string.Empty);
                    }

                    if (!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed) &&
                        !double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                    {
                        return (null, $"{descriptor.DisplayName} 不是有效数值。");
                    }

                    if (descriptor.IsOutOfRange(parsed))
                    {
                        return (null, $"{descriptor.DisplayName}={parsed} 超出范围 [{descriptor.Minimum}, {descriptor.Maximum}]。");
                    }

                    return (CommandValue.FromNumber(parsed), string.Empty);
                };

                box.TextChanged += (_, _) => OnEdited();
                return box;
            }
        }
    }

    private void OnEdited()
    {
        if (_loading)
        {
            return;
        }

        var dirty = TryBuildSet(out var values, out var error);

        if (!dirty)
        {
            SetStatus(error, isError: true);
            SetDirty(true);
            return;
        }

        SetStatus(string.Empty);

        var changed = values is not null
                      && _original is not null
                      && !ValuesEqual(values, _original);

        SetDirty(changed);
    }

    private void SetDirty(bool dirty)
    {
        if (IsDirty == dirty)
        {
            _dirtyLabel.Text = dirty ? "已修改（未保存）" : "未修改";
            _dirtyLabel.ForeColor = dirty ? Color.Firebrick : SystemColors.GrayText;
            return;
        }

        IsDirty = dirty;
        _dirtyLabel.Text = dirty ? "已修改（未保存）" : "未修改";
        _dirtyLabel.ForeColor = dirty ? Color.Firebrick : SystemColors.GrayText;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearRows()
    {
        foreach (Control control in _groups.Controls)
        {
            control.Dispose();
        }

        _groups.Controls.Clear();
        _rows.Clear();
        CanEdit = false;
    }

    private void ResizeGroups()
    {
        var width = Math.Max(620, _groups.ClientSize.Width - 28);

        foreach (Control control in _groups.Controls)
        {
            control.Width = width;

            foreach (Control child in control.Controls)
            {
                child.Width = width - 16;
            }
        }
    }

    /// <inheritdoc />
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ResizeGroups();
    }

    private static bool ValuesEqual(ElementParameterSet left, ElementParameterSet right)
    {
        if (left.Values.Count != right.Values.Count)
        {
            return false;
        }

        foreach (var pair in left.Values)
        {
            if (!right.Values.TryGetValue(pair.Key, out var other) || other != pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static string ApplyText(ParameterApplyMode mode) => mode switch
    {
        ParameterApplyMode.Immediate => "立即生效",
        ParameterApplyMode.NextCommand => "下一条命令",
        ParameterApplyMode.Restart => "重启后生效",
        _ => mode.ToString()
    };

    private sealed record Row(
        ElementParameterDescriptor Descriptor,
        Control Editor,
        Func<(CommandValue? Value, string Error)> Read)
    {
        public bool TryRead(out CommandValue? value, out string error)
        {
            var result = Read();
            value = result.Value;
            error = result.Error;
            return string.IsNullOrEmpty(result.Error);
        }
    }
}
