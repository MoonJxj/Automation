namespace EquipmentMonitor.Common;

/// <summary>
/// 统一返回结果，用于表达<b>预期内</b>的失败。
/// </summary>
/// <remarks>
/// <para>
/// 约定（见 docs/module-design/00-shared.md §4）：
/// <list type="bullet">
///   <item><b>预期内的失败</b>用 <see cref="Fail"/> 表达：设备离线、读取超时、校验不通过、权限不足。</item>
///   <item><b>预期外的异常</b>才 <c>throw</c>：空引用、索引越界、配置缺失等编程错误。</item>
/// </list>
/// </para>
/// <para>
/// 为什么不用异常表达业务失败：上位机中「设备离线」「读取超时」是每分钟都会发生的正常情况，
/// 异常栈展开的开销不必要，且容易被 <c>catch</c> 意外吞掉。显式返回结果强制调用方处理失败分支。
/// </para>
/// <para>
/// <b>注意</b>：<see cref="Result"/> 不是 <see cref="Exception"/> 的替代品。
/// 不可恢复的程序错误仍必须抛出，交由全局兜底记录完整堆栈。
/// </para>
/// </remarks>
public class Result
{
    /// <summary>操作是否成功。</summary>
    public bool IsSuccess { get; }

    /// <summary>操作是否失败。等同于 <c>!IsSuccess</c>。</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>失败原因（面向用户或日志，应可读）。成功时为空字符串。</summary>
    public string Error { get; }

    /// <summary>可选的错误码，用于程序化判断失败类型。成功时为 <c>null</c>。</summary>
    public string? ErrorCode { get; }

    /// <summary>导致失败的内部异常（若有）。仅用于日志记录，<b>不应直接展示给用户</b>。</summary>
    public Exception? Exception { get; }

    /// <summary>结果产生的时间。</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>构造结果。派生类与工厂方法使用。</summary>
    protected Result(bool isSuccess, string error, string? errorCode, Exception? exception)
    {
        if (isSuccess && error.Length != 0)
        {
            throw new ArgumentException("成功的结果不能携带错误信息。", nameof(error));
        }

        if (!isSuccess && string.IsNullOrWhiteSpace(error))
        {
            throw new ArgumentException("失败的结果必须提供错误信息。", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
        Exception = exception;
        Timestamp = DateTimeOffset.Now;
    }

    /// <summary>创建成功结果。</summary>
    public static Result Ok() => new(true, string.Empty, null, null);

    /// <summary>创建成功结果并携带值。</summary>
    public static Result<T> Ok<T>(T value) => new(value, true, string.Empty, null, null);

    /// <summary>创建失败结果。</summary>
    /// <param name="error">失败原因，必填且应可读。</param>
    /// <param name="errorCode">可选的错误码，便于程序化判断。</param>
    /// <param name="inner">内部异常，仅用于日志。</param>
    public static Result Fail(string error, string? errorCode = null, Exception? inner = null)
        => new(false, error, errorCode, inner);

    /// <summary>创建失败结果（泛型版本）。</summary>
    public static Result<T> Fail<T>(string error, string? errorCode = null, Exception? inner = null)
        => new(default, false, error, errorCode, inner);

    /// <inheritdoc />
    public override string ToString()
        => IsSuccess ? "OK" : $"FAILED({ErrorCode ?? "-"}): {Error}";
}

/// <summary>
/// 携带值的统一返回结果。
/// </summary>
/// <typeparam name="T">成功时返回的值类型。</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    /// <summary>成功时返回的值。</summary>
    /// <exception cref="InvalidOperationException">结果为失败时访问。</exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"无法从失败的结果中取值。错误：{Error}。请先检查 IsSuccess，或改用 {nameof(TryGetValue)}。");

    internal Result(T? value, bool isSuccess, string error, string? errorCode, Exception? exception)
        : base(isSuccess, error, errorCode, exception)
    {
        _value = value;
    }

    /// <summary>尝试取值，避免抛出异常。失败或值为 <c>null</c> 时返回 <c>false</c>。</summary>
    public bool TryGetValue(out T value)
    {
        if (IsSuccess && _value is not null)
        {
            value = _value;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>成功时把值映射为新类型，失败时原样传递错误。</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return IsSuccess ? Result.Ok(mapper(Value)) : Result.Fail<TOut>(Error, ErrorCode, Exception);
    }

    /// <summary>成功时链式调用下一个可能失败的操作，失败时原样传递错误。</summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return IsSuccess ? binder(Value) : Result.Fail<TOut>(Error, ErrorCode, Exception);
    }

    /// <summary>成功时执行副作用，便于链式书写。返回自身以便继续链式调用。</summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsSuccess)
        {
            action(Value);
        }

        return this;
    }

    /// <summary>失败时执行副作用（通常是记录日志）。返回自身以便继续链式调用。</summary>
    public Result<T> OnFailure(Action<string> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (IsFailure)
        {
            action(Error);
        }

        return this;
    }
}
