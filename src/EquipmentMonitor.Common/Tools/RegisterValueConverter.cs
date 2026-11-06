namespace EquipmentMonitor.Common;

/// <summary>
/// 多寄存器量的字序。仅影响 32 位及以上、跨两个以上寄存器的类型。
/// </summary>
/// <remarks>
/// <para>
/// 寄存器内部字节序固定按 Modbus 线序（大端）处理，本枚举只描述“哪个 16 位字排在前面”。
/// <see cref="HighWordFirst"/> 对应 ABCD（registers[0] 是最高字），
/// <see cref="LowWordFirst"/> 对应 CDAB（registers[0] 是最低字）。
/// </para>
/// <para>
/// 显式赋值保证跨进程/序列化稳定；不要调整已有取值。
/// </para>
/// </remarks>
public enum RegisterWordOrder
{
    /// <summary>高字在前（registers[0] 是最高 16 位字）。</summary>
    HighWordFirst = 0,

    /// <summary>低字在前（registers[0] 是最低 16 位字）。</summary>
    LowWordFirst = 1
}

/// <summary>
/// 设备寄存器与 .NET 基础数值类型之间的转换。
/// </summary>
/// <remarks>
/// <para>
/// 寄存器统一用 <see cref="ushort"/> 表示（一个 16 位字）。支持的类型及占用寄存器数：
/// <list type="table">
///   <item><term><see cref="bool"/></term><description>1 个寄存器；true=1，false=0；读取时非 0 即 true。</description></item>
///   <item><term><see cref="byte"/></term><description>1 个寄存器；默认取/写低 8 位，<c>highByte=true</c> 时取/写高 8 位。</description></item>
///   <item><term><see cref="short"/> / <see cref="ushort"/></term><description>1 个寄存器；<see cref="ushort"/> 即 WORD。</description></item>
///   <item><term><see cref="int"/> / <see cref="uint"/></term><description>2 个寄存器；<see cref="uint"/> 即 DWORD。</description></item>
///   <item><term><see cref="float"/></term><description>2 个寄存器；IEEE-754 32 位。</description></item>
///   <item><term><see cref="double"/></term><description>4 个寄存器；IEEE-754 64 位。</description></item>
/// </list>
/// </para>
/// <para>
/// <b>失败语义</b>：寄存器数量、类型或字序不合法的调用属于编程错误，直接抛异常；
/// 设备离线、超时、从站异常码属于预期内失败，由调用方用 <see cref="Result.Fail"/> 表达，不经过本类。
/// </para>
/// <para>
/// <b>字序</b>：32 位量的 <see cref="RegisterWordOrder.HighWordFirst"/> 为
/// <c>((uint)registers[0] &lt;&lt; 16) | registers[1]</c>，与现有
/// <c>ModbusElementBase.AsInt32</c> 的逐位结果一致；64 位量按 16 位字整体反序。
/// </para>
/// </remarks>
public static class RegisterValueConverter
{
    /// <summary>
    /// 获取类型 <typeparamref name="T"/> 在寄存器中占用的 16 位字数。
    /// </summary>
    /// <typeparam name="T">数值类型；支持 bool、byte、short、ushort、int、uint、float、double。</typeparam>
    /// <returns>1（bool/byte/short/ushort）、2（int/uint/float）或 4（double）。</returns>
    /// <exception cref="NotSupportedException">类型不在支持列表内。</exception>
    public static int GetRegisterCount<T>() where T : struct
    {
        if (typeof(T) == typeof(bool) ||
            typeof(T) == typeof(byte) ||
            typeof(T) == typeof(short) ||
            typeof(T) == typeof(ushort))
        {
            return 1;
        }

        if (typeof(T) == typeof(int) ||
            typeof(T) == typeof(uint) ||
            typeof(T) == typeof(float))
        {
            return 2;
        }

        if (typeof(T) == typeof(double))
        {
            return 4;
        }

        throw new NotSupportedException(
            $"寄存器转换不支持类型 {typeof(T).Name}。" +
            "支持 bool、byte、short、ushort、int、uint、float、double（WORD=ushort，DWORD=uint）。");
    }

    /// <summary>
    /// 把一个寄存器块解释为 <typeparamref name="T"/>。
    /// </summary>
    /// <typeparam name="T">数值类型；支持 bool、byte、short、ushort、int、uint、float、double。</typeparam>
    /// <param name="registers">
    /// 寄存器块。长度必须恰好等于 <see cref="GetRegisterCount{T}"/>；多余的寄存器请先切片。
    /// </param>
    /// <param name="wordOrder">32/64 位量的字序；16 位量忽略。</param>
    /// <param name="highByte">
    /// 仅当 <typeparamref name="T"/> 为 <see cref="byte"/> 时有效：
    /// <c>false</c> 取低 8 位，<c>true</c> 取高 8 位。
    /// </param>
    /// <returns>解释后的值。</returns>
    /// <exception cref="ArgumentException">寄存器数量与类型不符。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="wordOrder"/> 不是已定义的字序。</exception>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> 不在支持列表内。</exception>
    public static T ToValue<T>(
        ReadOnlySpan<ushort> registers,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst,
        bool highByte = false)
        where T : struct
    {
        var requiredCount = GetRegisterCount<T>();
        ValidateWordOrder(wordOrder);
        RequireExactLength(registers, requiredCount);

        object value;
        if (typeof(T) == typeof(bool))
        {
            value = ToBool(registers[0]);
        }
        else if (typeof(T) == typeof(byte))
        {
            value = ToByte(registers[0], highByte);
        }
        else if (typeof(T) == typeof(short))
        {
            value = ToInt16(registers[0]);
        }
        else if (typeof(T) == typeof(ushort))
        {
            value = registers[0];
        }
        else if (typeof(T) == typeof(int))
        {
            value = ToInt32(registers, wordOrder);
        }
        else if (typeof(T) == typeof(uint))
        {
            value = ToUInt32(registers, wordOrder);
        }
        else if (typeof(T) == typeof(float))
        {
            value = ToSingle(registers, wordOrder);
        }
        else if (typeof(T) == typeof(double))
        {
            value = ToDouble(registers, wordOrder);
        }
        else
        {
            // GetRegisterCount<T> 已经拦截了不支持的类型；这里是穷尽分支的兜底。
            throw new NotSupportedException($"寄存器转换不支持类型 {typeof(T).Name}。");
        }

        return (T)value;
    }

    /// <summary>
    /// 把 <typeparamref name="T"/> 拆成寄存器块。
    /// </summary>
    /// <typeparam name="T">数值类型；支持 bool、byte、short、ushort、int、uint、float、double。</typeparam>
    /// <param name="value">要写入的值。</param>
    /// <param name="wordOrder">32/64 位量的字序；16 位量忽略。</param>
    /// <param name="highByte">
    /// 仅当 <typeparamref name="T"/> 为 <see cref="byte"/> 时有效：
    /// <c>false</c> 写入低 8 位，<c>true</c> 写入高 8 位。
    /// </param>
    /// <returns>长度为 <see cref="GetRegisterCount{T}"/> 的寄存器块。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="wordOrder"/> 不是已定义的字序。</exception>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> 不在支持列表内。</exception>
    public static ushort[] FromValue<T>(
        T value,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst,
        bool highByte = false)
        where T : struct
    {
        GetRegisterCount<T>();
        ValidateWordOrder(wordOrder);

        object boxed = value;
        if (typeof(T) == typeof(bool))
        {
            return new[] { FromBool((bool)boxed) };
        }

        if (typeof(T) == typeof(byte))
        {
            return new[] { FromByte((byte)boxed, highByte) };
        }

        if (typeof(T) == typeof(short))
        {
            return new[] { FromInt16((short)boxed) };
        }

        if (typeof(T) == typeof(ushort))
        {
            return new[] { (ushort)boxed };
        }

        if (typeof(T) == typeof(int))
        {
            return FromInt32((int)boxed, wordOrder);
        }

        if (typeof(T) == typeof(uint))
        {
            return FromUInt32((uint)boxed, wordOrder);
        }

        if (typeof(T) == typeof(float))
        {
            return FromSingle((float)boxed, wordOrder);
        }

        if (typeof(T) == typeof(double))
        {
            return FromDouble((double)boxed, wordOrder);
        }

        // GetRegisterCount<T> 已经拦截了不支持的类型；这里是穷尽分支的兜底。
        throw new NotSupportedException($"寄存器转换不支持类型 {typeof(T).Name}。");
    }

    /// <summary>把单个寄存器解释为 <see cref="bool"/>：0 为 false，非 0 为 true。</summary>
    public static bool ToBool(ushort word) => word != 0;

    /// <summary>把 <see cref="bool"/> 写入单个寄存器：true 写 1，false 写 0。</summary>
    public static ushort FromBool(bool value) => value ? (ushort)1 : (ushort)0;

    /// <summary>
    /// 取单个寄存器中的一个字节。
    /// </summary>
    /// <param name="word">寄存器值。</param>
    /// <param name="highByte"><c>false</c> 取低 8 位，<c>true</c> 取高 8 位。</param>
    public static byte ToByte(ushort word, bool highByte = false)
        => highByte ? (byte)(word >> 8) : (byte)(word & 0xFF);

    /// <summary>
    /// 把一个字节放进单个寄存器。
    /// </summary>
    /// <param name="value">字节值。</param>
    /// <param name="highByte"><c>false</c> 放低 8 位，<c>true</c> 放高 8 位。</param>
    public static ushort FromByte(byte value, bool highByte = false)
        => highByte ? (ushort)(value << 8) : (ushort)value;

    /// <summary>把单个寄存器按补码解释为 <see cref="short"/>（INT）。</summary>
    public static short ToInt16(ushort word) => unchecked((short)word);

    /// <summary>把 <see cref="short"/>（INT）的补码位模式写入单个寄存器。</summary>
    public static ushort FromInt16(short value) => unchecked((ushort)value);

    /// <summary>
    /// 把两个寄存器解释为 <see cref="int"/>（DINT）。
    /// </summary>
    /// <param name="registers">恰好两个寄存器。</param>
    /// <param name="wordOrder">字序；<see cref="RegisterWordOrder.HighWordFirst"/> 时 registers[0] 为高字。</param>
    public static int ToInt32(
        ReadOnlySpan<ushort> registers,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst)
        => unchecked((int)ToUInt32(registers, wordOrder));

    /// <summary>把 <see cref="int"/>（DINT）拆成两个寄存器。</summary>
    public static ushort[] FromInt32(
        int value,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst)
        => FromUInt32(unchecked((uint)value), wordOrder);

    /// <summary>
    /// 把两个寄存器解释为 <see cref="uint"/>（DWORD）。
    /// </summary>
    /// <param name="registers">恰好两个寄存器。</param>
    /// <param name="wordOrder">字序；<see cref="RegisterWordOrder.HighWordFirst"/> 时 registers[0] 为高字。</param>
    public static uint ToUInt32(
        ReadOnlySpan<ushort> registers,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst)
    {
        ValidateWordOrder(wordOrder);
        RequireExactLength(registers, 2);

        return wordOrder == RegisterWordOrder.HighWordFirst
            ? ((uint)registers[0] << 16) | registers[1]
            : ((uint)registers[1] << 16) | registers[0];
    }

    /// <summary>把 <see cref="uint"/>（DWORD）拆成两个寄存器。</summary>
    public static ushort[] FromUInt32(
        uint value,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst)
    {
        ValidateWordOrder(wordOrder);

        var highWord = (ushort)(value >> 16);
        var lowWord = (ushort)(value & 0xFFFF);

        return wordOrder == RegisterWordOrder.HighWordFirst
            ? new[] { highWord, lowWord }
            : new[] { lowWord, highWord };
    }

    /// <summary>
    /// 把两个寄存器按 IEEE-754 位模式解释为 <see cref="float"/>（REAL）。
    /// </summary>
    public static float ToSingle(
        ReadOnlySpan<ushort> registers,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst)
        => BitConverter.UInt32BitsToSingle(ToUInt32(registers, wordOrder));

    /// <summary>把 <see cref="float"/>（REAL）按 IEEE-754 位模式拆成两个寄存器。</summary>
    public static ushort[] FromSingle(
        float value,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst)
        => FromUInt32(BitConverter.SingleToUInt32Bits(value), wordOrder);

    /// <summary>
    /// 把四个寄存器按 IEEE-754 位模式解释为 <see cref="double"/>（LREAL）。
    /// </summary>
    /// <param name="registers">恰好四个寄存器。</param>
    /// <param name="wordOrder">
    /// 字序；<see cref="RegisterWordOrder.HighWordFirst"/> 时 registers[0] 是最高字，
    /// <see cref="RegisterWordOrder.LowWordFirst"/> 时 registers[0] 是最低字。
    /// </param>
    public static double ToDouble(
        ReadOnlySpan<ushort> registers,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst)
    {
        ValidateWordOrder(wordOrder);
        RequireExactLength(registers, 4);

        ulong bits = wordOrder == RegisterWordOrder.HighWordFirst
            ? ((ulong)registers[0] << 48) |
              ((ulong)registers[1] << 32) |
              ((ulong)registers[2] << 16) |
              registers[3]
            : ((ulong)registers[3] << 48) |
              ((ulong)registers[2] << 32) |
              ((ulong)registers[1] << 16) |
              registers[0];

        return BitConverter.UInt64BitsToDouble(bits);
    }

    /// <summary>把 <see cref="double"/>（LREAL）按 IEEE-754 位模式拆成四个寄存器。</summary>
    public static ushort[] FromDouble(
        double value,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst)
    {
        ValidateWordOrder(wordOrder);

        var bits = BitConverter.DoubleToUInt64Bits(value);
        var word0 = (ushort)(bits >> 48);
        var word1 = (ushort)(bits >> 32);
        var word2 = (ushort)(bits >> 16);
        var word3 = (ushort)bits;

        return wordOrder == RegisterWordOrder.HighWordFirst
            ? new[] { word0, word1, word2, word3 }
            : new[] { word3, word2, word1, word0 };
    }

    /// <summary>把两个 16 位字组合成一个 DWORD（<see cref="uint"/>）。</summary>
    public static uint ToDWord(ushort highWord, ushort lowWord)
        => ((uint)highWord << 16) | lowWord;

    /// <summary>把一个 DWORD（<see cref="uint"/>）拆成高字与低字。</summary>
    public static (ushort HighWord, ushort LowWord) SplitDWord(uint value)
        => ((ushort)(value >> 16), (ushort)(value & 0xFFFF));

    private static void ValidateWordOrder(RegisterWordOrder wordOrder)
    {
        if (wordOrder != RegisterWordOrder.HighWordFirst &&
            wordOrder != RegisterWordOrder.LowWordFirst)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wordOrder), wordOrder, "未定义的字序；只支持 HighWordFirst 与 LowWordFirst。");
        }
    }

    private static void RequireExactLength(ReadOnlySpan<ushort> registers, int requiredCount)
    {
        if (registers.Length != requiredCount)
        {
            throw new ArgumentException(
                $"该类型需要 {requiredCount} 个寄存器，实际传入 {registers.Length} 个。",
                nameof(registers));
        }
    }
}
