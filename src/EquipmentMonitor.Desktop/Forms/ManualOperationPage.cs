using EquipmentMonitor.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentMonitor.Desktop.Forms;

/// <summary>
/// 手动操作页：固定工位1/工位2 × 执行器类型（气缸/伺服/机械手）。
/// </summary>
/// <remarks>
/// <para>骨架阶段只做“工位过滤 + 复用现有操作台弹窗”；具体设备实施时在这里挂更多执行器页签，
/// 不需要改导航与权限。</para>
/// <para>工位清单来自 <see cref="StationCatalog"/>；工位数量变化时只改目录，不改本页。</para>
/// </remarks>
public sealed partial class ManualOperationPage : Form
{
    private ElementOverviewPage? _axisPage;
    private CylinderOverviewPage? _cylinderPage;

    /// <summary>设计器专用构造。</summary>
    public ManualOperationPage()
    {
        InitializeComponent();
    }

    /// <summary>构造手动操作页。</summary>
    public ManualOperationPage(IServiceProvider services)
        : this()
    {
        ArgumentNullException.ThrowIfNull(services);

        _axisPage = services.GetRequiredService<ElementOverviewPage>();
        _axisPage.StationFilter = StationCatalog.Station1;
        Embed(_axisPage, _axisTab);

        _cylinderPage = services.GetRequiredService<CylinderOverviewPage>();
        _cylinderPage.StationFilter = StationCatalog.Station1;
        Embed(_cylinderPage, _cylinderTab);

        _station1Button.Click += (_, _) => SelectStation(StationCatalog.Station1);
        _station2Button.Click += (_, _) => SelectStation(StationCatalog.Station2);

        SelectStation(StationCatalog.Station1);
    }

    /// <summary>当前工位编号（测试用）。</summary>
    internal int StationId { get; private set; } = StationCatalog.Station1;

    /// <summary>伺服页（测试用）。</summary>
    internal ElementOverviewPage? AxisPage => _axisPage;

    /// <summary>气缸页（测试用）。</summary>
    internal CylinderOverviewPage? CylinderPage => _cylinderPage;

    /// <summary>切换工位（测试用；生产路径为工位按钮）。</summary>
    internal void SelectStationForTest(int stationId) => SelectStation(stationId);

    private void SelectStation(int stationId)
    {
        if (!StationCatalog.Contains(stationId))
        {
            return;
        }

        StationId = stationId;

        if (_axisPage is not null)
        {
            _axisPage.StationFilter = stationId;
            _axisPage.RefreshView();
        }

        if (_cylinderPage is not null)
        {
            _cylinderPage.StationFilter = stationId;
            _cylinderPage.RefreshView();
        }

        Highlight(_station1Button, stationId == StationCatalog.Station1);
        Highlight(_station2Button, stationId == StationCatalog.Station2);
    }

    private static void Embed(Form page, TabPage tab)
    {
        page.TopLevel = false;
        page.FormBorderStyle = FormBorderStyle.None;
        page.Dock = DockStyle.Fill;
        tab.Controls.Add(page);
        page.Show();
    }

    private static void Highlight(Button button, bool selected)
        => button.BackColor = selected ? Color.FromArgb(210, 225, 250) : SystemColors.Control;
}
