using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Desktop.Parameter;

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 通用参数设置页模板：左侧元件列表，右侧按描述符生成的参数编辑器。
/// </summary>
/// <remarks>
/// <para><b>通用</b>：页面不知道“轴”或“气缸”字段，只依赖 <see cref="IParameterPageSource"/>；
/// 具体项目实现该接口即可复用本页。</para>
/// <para><b>骨架阶段</b>：默认注册 <see cref="TemplateParameterPageSource"/>（内存示例），
/// 因此页面可运行、可编辑；接入真实参数服务后替换 DI 注册即可。</para>
/// </remarks>
public sealed partial class ParameterSettingsPage : Form
{
    private readonly IParameterPageSource? _source;
    private readonly IPermissionService? _permissions;

    private IReadOnlyList<ParameterTarget> _targets = [];
    private ParameterTarget? _current;
    private bool _busy;

    /// <summary>设计器专用构造：只初始化界面与更新方式列表。</summary>
    public ParameterSettingsPage()
    {
        InitializeComponent();

        _modeCombo.Items.Add(new UpdateModeItem("仅保存", ParameterUpdateMode.SaveOnly));
        _modeCombo.Items.Add(new UpdateModeItem("保存并下发", ParameterUpdateMode.SaveAndApply));
        _modeCombo.Items.Add(new UpdateModeItem("仅下发", ParameterUpdateMode.ApplyOnly));
        _modeCombo.SelectedIndex = 0;

        _saveButton.Enabled = false;
        _elementList.SelectedIndexChanged += async (_, _) => await LoadSelectedAsync().ConfigureAwait(true);
        _reloadButton.Click += async (_, _) => await LoadSelectedAsync(forceReload: true).ConfigureAwait(true);
        _saveButton.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
        _editor.DirtyChanged += (_, _) => UpdateDirtyState();
    }

    /// <summary>构造通用参数页。</summary>
    public ParameterSettingsPage(IParameterPageSource source, IPermissionService permissions)
        : this()
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(permissions);

        _source = source;
        _permissions = permissions;

        _editor.AttachPermissions(permissions);

        Load += async (_, _) => await LoadTargetsAsync().ConfigureAwait(true);
    }

    /// <summary>当前元件数量（测试用）。</summary>
    internal int TargetCount => _targets.Count;

    /// <summary>当前选中的元件（测试用）。</summary>
    internal ParameterTarget? CurrentTarget => _current;

    /// <summary>参数编辑器（测试用）。</summary>
    internal Controls.ParameterEditorControl Editor => _editor;

    /// <summary>选择元件（测试用；生产路径为列表选择）。</summary>
    internal void SelectTargetForTest(int index)
    {
        if (index >= 0 && index < _elementList.Items.Count)
        {
            _elementList.SelectedIndex = index;
        }
    }

    private async Task LoadTargetsAsync()
    {
        var source = _source;

        if (source is null)
        {
            _editor.SetStatus("参数数据源未注册。", isError: true);
            return;
        }

        try
        {
            var result = await source.ListAsync().ConfigureAwait(true);

            if (result.IsFailure)
            {
                _editor.SetStatus($"读取参数元件失败：{result.Error}", isError: true);
                return;
            }

            _targets = result.Value;
            _elementList.Items.Clear();

            foreach (var target in _targets)
            {
                _elementList.Items.Add(target);
            }

            _banner.Text = source.Banner ?? string.Empty;
            _banner.Visible = !string.IsNullOrWhiteSpace(_banner.Text);

            if (_targets.Count > 0)
            {
                _elementList.SelectedIndex = 0;
            }
            else
            {
                _editor.SetStatus("没有可编辑的参数。", isError: true);
            }
        }
        catch (Exception ex)
        {
            _editor.SetStatus($"读取参数元件异常：{ex.Message}", isError: true);
        }
    }

    private async Task LoadSelectedAsync(bool forceReload = false)
    {
        var source = _source;

        if (source is null || _busy || _elementList.SelectedItem is not ParameterTarget target)
        {
            return;
        }

        if (!forceReload && _editor.IsDirty &&
            MessageBox.Show(
                this,
                "当前参数有未保存修改，确定重新加载并丢弃吗？",
                "参数未保存",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.OK)
        {
            return;
        }

        _busy = true;

        try
        {
            var result = await source.GetAsync(target.ElementId).ConfigureAwait(true);

            if (result.IsFailure)
            {
                _editor.SetStatus($"读取 {target.DisplayName} 参数失败：{result.Error}", isError: true);
                return;
            }

            _current = target;
            _editor.LoadSnapshot(result.Value);
            _editor.SetStatus(string.Empty);
            UpdateDirtyState();
        }
        catch (Exception ex)
        {
            _editor.SetStatus($"读取参数异常：{ex.Message}", isError: true);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task SaveAsync()
    {
        var source = _source;
        var target = _current;

        if (source is null || target is null || _busy)
        {
            return;
        }

        if (!_editor.TryBuildSet(out var values, out var error) || values is null)
        {
            _editor.SetStatus(error, isError: true);
            return;
        }

        if (!_editor.IsDirty)
        {
            _editor.SetStatus("没有需要保存的修改。");
            return;
        }

        var mode = _modeCombo.SelectedItem is UpdateModeItem item
            ? item.Mode
            : ParameterUpdateMode.SaveOnly;

        _busy = true;
        _saveButton.Enabled = false;

        try
        {
            var result = await source.SaveAsync(target.ElementId, values, mode).ConfigureAwait(true);

            if (result.IsFailure)
            {
                _editor.SetStatus($"保存失败：{result.Error}", isError: true);
                return;
            }

            _editor.MarkSaved();

            var change = result.Value;
            var message = $"已保存（{change.ChangedKeys.Count} 项）";

            if (change.AppliedToController)
            {
                message += "，已下发控制器";
            }

            if (change.RestartRequired)
            {
                message += "；部分参数重启后生效";
            }

            _editor.SetStatus(message);
            UpdateDirtyState();
        }
        catch (Exception ex)
        {
            _editor.SetStatus($"保存异常：{ex.Message}", isError: true);
        }
        finally
        {
            _busy = false;
            UpdateDirtyState();
        }
    }

    private void UpdateDirtyState()
    {
        _saveButton.Enabled = _editor.IsDirty && !_busy;
    }

    private sealed record UpdateModeItem(string Name, ParameterUpdateMode Mode)
    {
        public override string ToString() => Name;
    }
}
