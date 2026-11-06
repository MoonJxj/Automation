# Shared 模块设计

> 项目：`EquipmentMonitor.Shared` ｜ 许可证无关（零依赖）

## 1. 职责边界

**做什么**：存放所有模块共用的、与业务无关的基础设施——统一返回结果、通用扩展方法、全局常量。

**不做什么**：

- ❌ 不含任何业务概念（不出现"设备""通道""用户""元件"等字样）
- ❌ 不引用任何其他项目（叶子模块）
- ❌ 不引用任何第三方 NuGet 包

**判断标准**：一段代码如果只有某一个模块在用，它就不属于 `Shared`。
`Shared` 是**被动收编**的——先写在业务模块里，等到第二个模块也需要时再上移。
反过来，预先往 `Shared` 里堆"以后可能有用"的工具类，是它变成垃圾桶的开始。

## 2. 依赖关系

```
EquipmentMonitor.Shared ──► （无）
```

无任何项目依赖与包依赖。

## 3. 目录结构

```
EquipmentMonitor.Shared/
├── Result.cs                    # 统一返回结果
├── Extensions/
│   ├── ResultExtensions.cs      # Result 的组合与转换
│   ├── StringExtensions.cs      # 字符串处理
│   ├── NumericExtensions.cs     # 数值转换与字节序处理
│   └── CollectionExtensions.cs
└── Constants/
    ├── AppConstants.cs          # 应用级常量（名称、版本、路径）
    └── RegexPatterns.cs         # 常用正则（IP、串口名、Tag 名）
```

## 4. 核心类型：Result

系统中**预期内的失败**统一用 `Result` 表达，不使用异常。这是贯穿所有模块的约定。

```csharp
namespace EquipmentMonitor.Shared;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }
    public string? ErrorCode { get; }
    public Exception? Exception { get; }

    protected Result(bool isSuccess, string error, string? errorCode, Exception? exception);

    public static Result Ok();
    public static Result Fail(string error, string? errorCode = null, Exception? inner = null);
    public static Result<T> Ok<T>(T value);
    public static Result<T> Fail<T>(string error, string? errorCode = null, Exception? inner = null);
}

public sealed class Result<T> : Result
{
    public T Value { get; }        // IsFailure 时访问将抛出 InvalidOperationException
    public bool TryGetValue(out T value);

    public Result<TOut> Map<TOut>(Func<T, TOut> mapper);
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder);
    public Result<T> OnSuccess(Action<T> action);
    public Result<T> OnFailure(Action<string> action);
}
```

**使用约定**：

| 场景 | 用法 | 示例 |
|---|---|---|
| 预期内的失败 | `Result.Fail(...)` | 设备离线、Tag 不存在、校验不通过、权限不足 |
| 预期外的异常 | `throw` | 空引用、索引越界、配置缺失（属编程错误） |

**为什么不用异常表达业务失败**：
上位机中"设备离线""读取超时"是**每分钟都会发生**的正常情况，
用异常处理会带来不必要的性能开销（异常栈展开昂贵），且容易被 `catch` 意外吞掉。
显式返回 `Result` 强制调用方处理失败分支。

**边界**：`Result` 不是 `Exception` 的替代品。
对于**不可恢复的程序错误**，仍然必须抛出异常，让全局兜底记录完整堆栈。

### 4.1 与通讯层的配合

```csharp
// Communication/Channels/ModbusTcpChannel.cs
public async Task<Result<ushort[]>> ReadHoldingRegistersAsync(ushort address, ushort count, CancellationToken ct)
{
    try
    {
        var registers = await _master.ReadHoldingRegistersAsync(_slaveId, address, count);
        return Result.Ok(registers);
    }
    catch (SlaveException ex)         // 从站返回错误码 —— 预期内
    {
        return Result.Fail<ushort[]>($"从站异常：{ex.Message}", "MODBUS_SLAVE_ERROR", ex);
    }
    catch (IOException ex)            // 网络/串口中断 —— 预期内，交由重连处理
    {
        return Result.Fail<ushort[]>($"通道 IO 异常：{ex.Message}", "CHANNEL_IO_ERROR", ex);
    }
}
```

## 5. 扩展方法

`Extensions/` 下的方法遵循以下约定：

- 定义在 `static class` 中，命名空间为 `EquipmentMonitor.Shared.Extensions`
  （**不使用** `System` 命名空间——那会污染全局并引发歧义解析）
- 只放**高频且语义明确**的通用操作；能用 BCL 一行写清的不收录
- `NumericExtensions` 需包含工业场景高频的**字节序转换**（大端/小端、字交换、双字交换），
  这是协议解析中最容易出错的地方，集中在 `Shared` 中便于单元测试覆盖

## 6. 常量

**禁止散落的魔法值**。以下类别必须集中定义：

- 默认超时、重试次数、轮询周期
- 文件路径模板、日志目录名
- 配置节的 Key 名（`"Serilog"`、`"Database"` 等）
- 常用正则（IP、端口、串口名、PLC Tag 名）

**注意**：配置项的**默认值**如果可被 `appsettings.json` 覆盖，应定义在对应模块的配置模型类中，
而非 `Shared/Constants`。`Constants` 只放**真正不会变**的值。

## 7. 扩展点

| 需求 | 做法 |
|---|---|
| 新增通用扩展方法 | 加到 `Extensions/` 下对应类；跨领域时新建类文件 |
| 新增 `Result` 组合方法 | 加到 `Extensions/ResultExtensions.cs`，避免污染 `Result` 本体 |
| 新增常量 | 按类别归入 `Constants/`，不要新建零散常量类 |

**约束**：本模块**不需要 DI 注册**——全部是静态类型。若某项功能需要 DI，说明它不属于 `Shared`。

## 8. 待细化事项

留给后续按模块细化时确认：

- [ ] `Result` 是否需要携带 `Severity`（区分"警告"与"错误"）以及是否与日志级别联动
- [ ] `ErrorCode` 的命名规范与是否建立集中式错误码表
- [ ] 字节序转换 API 的具体形态（是否引入 `Span<byte>` 重载以避免分配）
- [ ] 是否需要 `Result` 的结构化序列化能力（用于远程认证返回错误）

## 9. 相关文档

- [总体架构](../architecture.md)
- [编码规范](../conventions.md)
- [Core 模块](01-core.md)
