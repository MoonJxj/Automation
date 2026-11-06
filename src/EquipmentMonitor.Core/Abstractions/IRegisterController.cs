using EquipmentMonitor.Common;

namespace EquipmentMonitor.Core.Abstractions
{
    /// <summary>
    /// 设备寄存器读写控制。提供原始 16 位寄存器块读写，以及按 <typeparamref name="T"/> 的类型化读写。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>地址约定</b>：所有地址都是零基地址，与 <c>IModbusChannel</c> 一致。
    /// PLC 手册中的 <c>40001</c> 这类表示法由配置层换算，本接口不做猜测。
    /// </para>
    /// <para>
    /// <b>为什么是异步 + <see cref="Result"/></b>：实现最终会落到
    /// <c>IModbusChannel.ReadHoldingRegistersAsync</c> / <c>WriteSingleRegisterAsync</c> /
    /// <c>WriteMultipleRegistersAsync</c>，它们本身就是 <c>Task&lt;Result&gt;</c>。
    /// 同步的 <c>bool</c> 返回值只能告诉调用方“失败”，无法区分离线、超时、从站异常码和地址非法；
    /// 在 UI 线程上再套一层同步等待还会带来死锁风险。
    /// </para>
    /// <para>
    /// <b>为什么用泛型而不是 <c>enum type</c> 参数</b>：类型在编译期确定，调用方无需再按枚举分支并
    /// cast 返回值；不支持的 <typeparamref name="T"/> 会在第一次调用时被
    /// <see cref="RegisterValueConverter.GetRegisterCount{T}"/> 的白名单拒绝，而不是悄悄返回 0 个寄存器。
    /// </para>
    /// <para>
    /// <b>支持的值类型</b>：<see cref="bool"/>、<see cref="byte"/>、<see cref="short"/>、
    /// <see cref="ushort"/>（WORD）、<see cref="int"/>、<see cref="uint"/>（DWORD）、
    /// <see cref="float"/>、<see cref="double"/>。类型化方法必须使用
    /// <see cref="RegisterValueConverter"/> 完成解释与拆拼，避免各实现各写一套字序。
    /// </para>
    /// <para>
    /// <b>失败语义</b>：设备离线、超时、从站异常码等预期内失败返回 <see cref="Result.Fail"/>；
    /// 类型不支持、寄存器数量不符等编程错误由转换器抛异常。
    /// </para>
    /// </remarks>
    public interface IRegisterController : IControllerBase
    {
        /// <summary>
        /// 读取一段连续的保持寄存器。
        /// </summary>
        /// <param name="controllerNo">控制器编号（Modbus 从站号或厂商设备号）。</param>
        /// <param name="startAddress">起始地址（零基）。</param>
        /// <param name="count">读取的 16 位寄存器数量。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>成功时返回长度为 <paramref name="count"/> 的寄存器块；失败时返回带错误码的失败结果。</returns>
        Task<Result<ushort[]>> ReadRegistersAsync(
            int controllerNo,
            ushort startAddress,
            ushort count,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 向一段连续的保持寄存器写入原始值。
        /// </summary>
        /// <param name="controllerNo">控制器编号（Modbus 从站号或厂商设备号）。</param>
        /// <param name="startAddress">起始地址（零基）。</param>
        /// <param name="values">要写入的寄存器块；长度决定写入的寄存器数量。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <param name="verifyReadBack">
        /// 是否写后回读校验。普通点位建议 <c>true</c>；轴命令区通常只写不读（或写入后立即被 PLC 清零），
        /// 命令写入应传 <c>false</c>，由状态区回读判定命令是否生效。
        /// </param>
        Task<Result> WriteRegistersAsync(
            int controllerNo,
            ushort startAddress,
            ushort[] values,
            CancellationToken cancellationToken = default,
            bool verifyReadBack = true);

        /// <summary>
        /// 读取一段连续的线圈。
        /// </summary>
        /// <param name="controllerNo">控制器编号（Modbus 从站号或厂商设备号）。</param>
        /// <param name="startAddress">起始地址（零基）。</param>
        /// <param name="count">读取的线圈数量。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>成功时返回长度为 <paramref name="count"/> 的线圈状态。</returns>
        Task<Result<bool[]>> ReadCoilsAsync(
            int controllerNo,
            ushort startAddress,
            ushort count,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 写入单个线圈。
        /// </summary>
        /// <remarks>
        /// 轴控命令的 <c>Execute</c> 触发是线圈（功能码 05），不是保持寄存器；
        /// 没有这个能力就只能把命令触发挪到保持寄存器，破坏与 PLC 的既有契约。
        /// </remarks>
        /// <param name="verifyReadBack">是否写后回读校验；命令触发线圈通常传 <c>false</c>。</param>
        Task<Result> WriteCoilAsync(
            int controllerNo,
            ushort address,
            bool value,
            CancellationToken cancellationToken = default,
            bool verifyReadBack = true);

        /// <summary>
        /// 读取一个值，并按 <typeparamref name="T"/> 解释。占用的寄存器数由
        /// <see cref="RegisterValueConverter.GetRegisterCount{T}"/> 决定。
        /// </summary>
        /// <typeparam name="T">支持 bool、byte、short、ushort、int、uint、float、double。</typeparam>
        /// <param name="controllerNo">控制器编号（Modbus 从站号或厂商设备号）。</param>
        /// <param name="address">起始地址（零基）。</param>
        /// <param name="wordOrder">32/64 位量的字序；16 位量忽略。默认高字在前。</param>
        /// <param name="highByte">
        /// 仅当 <typeparamref name="T"/> 为 <see cref="byte"/> 时有效：
        /// <c>false</c> 取低 8 位，<c>true</c> 取高 8 位。
        /// </param>
        /// <param name="cancellationToken">取消令牌。</param>
        Task<Result<T>> ReadValueAsync<T>(
            int controllerNo,
            ushort address,
            RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst,
            bool highByte = false,
            CancellationToken cancellationToken = default)
            where T : struct;

        /// <summary>
        /// 写入一个值。占用的寄存器数由 <see cref="RegisterValueConverter.GetRegisterCount{T}"/> 决定。
        /// </summary>
        /// <typeparam name="T">支持 bool、byte、short、ushort、int、uint、float、double。</typeparam>
        /// <param name="controllerNo">控制器编号（Modbus 从站号或厂商设备号）。</param>
        /// <param name="address">起始地址（零基）。</param>
        /// <param name="value">要写入的值。</param>
        /// <param name="wordOrder">32/64 位量的字序；16 位量忽略。默认高字在前。</param>
        /// <param name="highByte">
        /// 仅当 <typeparamref name="T"/> 为 <see cref="byte"/> 时有效：
        /// <c>false</c> 写低 8 位，<c>true</c> 写高 8 位。
        /// </param>
        /// <param name="cancellationToken">取消令牌。</param>
        Task<Result> WriteValueAsync<T>(
            int controllerNo,
            ushort address,
            T value,
            RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst,
            bool highByte = false,
            CancellationToken cancellationToken = default)
            where T : struct;
    }
}
