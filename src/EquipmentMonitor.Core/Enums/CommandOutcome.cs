namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 元件命令被<b>接受之后</b>的终态，语义对齐 PLCopen 功能块输出
/// （Done / CommandAborted / Error）并补充上位机必需的 <see cref="Timeout"/>。
/// </summary>
/// <remarks>
/// <para><b>不接受</b>的命令不产生本枚举值：未知命令、参数非法、互锁拒绝走 <c>Result.Fail</c>，
/// 与 <c>EvaluateCommand</c> 同源（见 <c>IElementCommandTarget</c>）。</para>
/// <para><see cref="Unknown"/> 放 0：默认值不得被当作成功。</para>
/// </remarks>
public enum CommandOutcome
{
    /// <summary>未初始化。默认值，不表示任何真实结果。</summary>
    Unknown = 0,

    /// <summary>成功完成：回读满足期望，不是“写成功”。</summary>
    Done = 1,

    /// <summary>被新命令或停止命令中断（当前框架尚未实现中断，保留语义）。</summary>
    CommandAborted = 2,

    /// <summary>执行中失败：控制器拒绝、设备报警、回读与期望相反。</summary>
    Error = 3,

    /// <summary>回读在超时窗口内始终未满足期望。</summary>
    Timeout = 4,

    /// <summary>
    /// 命令已下发，尚未观察到位（本框架当前轴契约的默认结果）。
    /// </summary>
    /// <remarks>
    /// <para>新框架的轴命令语义是“命令写入控制器”，到位由 <c>RefreshAsync</c> 轮询状态判断
    /// （见交接文档 §4.2）。因此成功下发返回本值，而不是假称 <see cref="Done"/>。</para>
    /// <para>UI 应显示“已下发”，并在状态列观察到位；把 <see cref="Accepted"/> 当成功终态会误导操作工。</para>
    /// </remarks>
    Accepted = 5
}
