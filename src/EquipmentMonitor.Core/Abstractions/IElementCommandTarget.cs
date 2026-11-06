using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 可操作元件的命令契约：命令表（给 UI 自动生成界面）、可用性评估、执行。
/// </summary>
/// <remarks>
/// <para><b>为什么是独立接口而不是给 <see cref="IElement"/> 加写方法</b>：
/// 传感器等元件没有命令，读契约不应被写能力污染；UI 也不需要为“有没有命令”分支。</para>
///
/// <para><b>失败语义：两层</b></para>
/// <list type="bullet">
///   <item><b>接受层</b>（<c>Result</c>）：命令该不该执行。失败 ⇔ 未写出任何报文，
///   错误码见 <see cref="CommandRejectionCodes"/>，判据与 <see cref="EvaluateCommand"/> 同源。</item>
///   <item><b>执行层</b>（<see cref="ElementCommandResult.Outcome"/>）：执行得怎么样，
///   终态由回读决定（Done / Aborted / Error / Timeout），不是“写成功”。</item>
/// </list>
///
/// <para><b>线程模型</b>：<see cref="Commands"/> 与 <see cref="EvaluateCommand"/> 任意线程只读；
/// <see cref="ExecuteCommandAsync"/> 可在 UI 线程发起，但实现必须把控制器调用放到后台线程，
/// 且同一元件上的命令串行执行（元件内已有命令闸门）。</para>
///
/// <para><b>实现方必须承担</b>：权限 <c>RequirePermission(MachineOperate)</c>（失败抛
/// <see cref="UnauthorizedAccessException"/>）、每次调用成败写审计、未决等待可取消。</para>
///
/// <para><b>本版未包含</b> <c>DescribeWritePlan</c>：新框架的寄存器地址封装在控制器内部，
/// 元件不知道“将写哪个地址”。二次确认显示元件/命令/参数与后果说明，地址级预演留给
/// 后续 Modbus 专用路径。详见 <c>docs/module-design/10-desktop.md §8</c>。</para>
/// </remarks>
public interface IElementCommandTarget
{
    /// <summary>命令表。空列表表示本元件不支持操作。</summary>
    IReadOnlyList<ElementCommandDescriptor> Commands { get; }

    /// <summary>
    /// 评估一条命令当前可否执行（UI 置灰与原因提示的依据）。纯查询：不改状态、不写审计、不发报文。
    /// </summary>
    /// <param name="commandKey">命令键。未知键返回 Blocked，不抛异常。</param>
    CommandAvailability EvaluateCommand(string commandKey);

    /// <summary>执行一条命令并等待其到达终态。</summary>
    /// <param name="request">命令请求（键 + 参数）。</param>
    /// <param name="cancellationToken">
    /// 取消令牌。⚠️ 仅取消“等待”：控制器调用一旦发起，物理动作已经发出，
    /// 取消不会撤回动作，只会让调用方提前放弃观察。
    /// </param>
    /// <returns>接受层失败返回 <c>Result.Fail</c>；已接受并到终态返回 <c>Result.Ok(结果)</c>。</returns>
    Task<Result<ElementCommandResult>> ExecuteCommandAsync(
        ElementCommandRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>命令被拒绝（未执行）时 <c>Result.ErrorCode</c> 的取值。</summary>
public static class CommandRejectionCodes
{
    /// <summary>命令键不在命令表中。</summary>
    public const string UnknownCommand = "COMMAND_UNKNOWN";

    /// <summary>参数缺失 / 类型不符 / 越界。未写出任何报文。</summary>
    public const string InvalidArgument = "COMMAND_INVALID_ARGUMENT";

    /// <summary>互锁拒绝：元件当前状态不允许该命令。</summary>
    public const string Blocked = "COMMAND_BLOCKED";

    /// <summary>元件未初始化或已释放。</summary>
    public const string NotReady = "COMMAND_NOT_READY";

    /// <summary>调用方在等待中被取消。</summary>
    public const string Cancelled = "COMMAND_CANCELLED";
}
