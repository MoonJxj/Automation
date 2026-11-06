namespace EquipmentMonitor.Core.Models;

/// <summary>
/// 机台的静态元数据（<b>共有属性</b>）。存 SQLite，供大数据平台直接查询。
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="MachineCode"/> 是跨系统的唯一关联键。</b>
/// MES 识别用它、大数据关联用它、Api 查询用它 —— 它一旦推给外部系统就不应再改。
/// </para>
/// <para>
/// 本类不含任何运行时状态（那在 <see cref="MachineSummary"/> / <see cref="MachineSnapshot"/> 里）。
/// 元数据是配置，运行期不变；状态每次采集都变。两者分离后，
/// 大数据平台可以只读元数据表，不必关心上位机的采集节奏。
/// </para>
/// </remarks>
public sealed record MachineMetadata
{
    /// <summary>
    /// 机台唯一编码。<b>跨系统标识，推送给 MES 后不可更改。</b>
    /// </summary>
    /// <remarks>
    /// 建议用现场既有的资产编号（如 <c>L1-CNC-01</c>），
    /// 不要用数据库自增主键 —— 那在跨系统关联时毫无意义。
    /// </remarks>
    public required string MachineCode { get; init; }

    /// <summary>机台名称（界面展示用）。</summary>
    public required string Name { get; init; }

    /// <summary>设备型号。</summary>
    public string? Model { get; init; }

    /// <summary>所属产线编码。</summary>
    public string? LineCode { get; init; }

    /// <summary>物理位置（车间 / 工位）。</summary>
    public string? Location { get; init; }

    /// <summary>
    /// 责任人。
    /// </summary>
    /// <remarks>
    /// 与 <c>MachineOptions.ResponsiblePerson</c>、<c>MachineEntity.ResponsiblePerson</c>
    /// 以及数据库列 <c>responsible_person</c> 同名。<b>这条链路上不允许有第二个叫法</b> ——
    /// 早先这里叫 <c>Owner</c>，于是配置里的 <c>ResponsiblePerson</c> 一到领域模型就换了名字：
    /// 读代码的人要在脑子里维护一张对照表，写新代码的人则会各选各的。
    /// </remarks>
    public string? ResponsiblePerson { get; init; }

    /// <summary>投运日期（UTC）。用于设备台账与折旧统计。</summary>
    public DateTimeOffset? CommissionedAt { get; init; }

    /// <summary>备注。</summary>
    public string? Remark { get; init; }

    /// <summary>是否启用。禁用的机台不参与采集。</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// 校验元数据合法性。配置加载时调用。
    /// </summary>
    /// <param name="now">
    /// 当前时刻（UTC），<b>由调用方注入</b>。
    /// </param>
    /// <exception cref="InvalidOperationException">配置有误。</exception>
    /// <remarks>
    /// <b>为什么 <paramref name="now"/> 要注入而不是内部取时钟：</b>
    /// 见 docs/conventions.md 的「时间注入」约束。内部调用
    /// <c>DateTimeOffset.UtcNow</c> 会让「投运日期是否合理」这条规则
    /// 无法被确定地测试 —— 测试要么依赖运行时的真实日期（在 2029 年跑会变红），
    /// 要么只能覆盖 1970 那一侧。注入之后，测试可以断言
    /// 「以 2026-01-01 为基准，投运日期 2028-06-01 会被拒绝」这样确定的命题。
    /// </remarks>
    public void Validate(DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(MachineCode))
        {
            throw new InvalidOperationException("机台元数据缺少 MachineCode。");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException($"机台 {MachineCode} 缺少名称。");
        }

        // 点号是 MES 数据点命名空间的分隔符（Machine.{Code}.Element...）。
        // 出现在编码里会让命名空间产生歧义 —— 一旦推送出去就再也改不回来了。
        if (MachineCode.Contains('.', StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"机台编码 {MachineCode} 不能包含点号 —— 点号是 MES 数据点命名空间的分隔符。");
        }

        if (CommissionedAt is { } commissioned)
        {
            // 投运日期是台账数据，写错会让折旧统计出错。给一个粗略的合理性检查：
            // 早于 1970 或晚于当前时间 1 年以上，几乎必然是笔误。
            // now 由调用方注入 —— 本方法不碰时钟。
            if (commissioned.Year < 1970 || commissioned > now.AddYears(1))
            {
                throw new InvalidOperationException(
                    $"机台 {MachineCode} 的投运日期 {commissioned:yyyy-MM-dd} 不合理，" +
                    "请检查是否为笔误。");
            }
        }
    }

    /// <summary>
    /// 返回用于日志输出的简明字符串。
    /// </summary>
    public override string ToString()
    {
        // 如果是禁用状态，在日志里标注出来，方便排查“为什么没数据”
        string status = IsEnabled ? "" : " [已禁用]";

        return $"MachineMetadata({MachineCode}, '{Name}', Line={LineCode ?? "-"}, " +
               $"Location={Location ?? "-"}){status}";
    }
}
