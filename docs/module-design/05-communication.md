# Communication 模块设计

> 项目：`EquipmentMonitor.Communication` ｜ 依赖：`Core`、`Shared`
> NModbus 3.0.83（MIT）、MQTTnet 5.2.0.1603（MIT）、System.IO.Ports 8.0.0（MIT）

## 1. 职责边界

**做什么**：把多种工业协议统一到一套生命周期契约下；连接管理；自动重连；数据路由；通道健康度。

**不做什么**：

- ❌ **不解析业务语义**（不知道「寄存器 40001 是气缸伸出信号」——那是 Devices 的职责）
- ❌ 不引用 `Devices`（依赖方向单向）
- ❌ 不复制权限判断逻辑（只调用 Security 的 `RequirePermission`）
- ❌ 不 marshal 到 UI 线程（那是界面层的事）

**职责分界线**：本模块只负责**把字节搬进搬出**。这条线必须守住，
否则每接一种新设备都要改通讯层。

## 2. 架构

### 2.1 抽象层次：单层（协议级）

`ICommunicationChannel` 是**所有通道共有的生命周期契约**，协议特有操作放在扩展接口上。

```
                    ┌──────────────────────────────────┐
                    │  ICommunicationChannel           │
                    │  Connect / Disconnect / State    │
                    │  DataReader / GetHealth          │
                    └──────────────┬───────────────────┘
                                   │
              ┌────────────────────┼────────────────────┐
              ▼                    ▼                    ▼
      ┌───────────────┐   ┌───────────────┐   ┌───────────────┐
      │ IModbusChannel│   │  IMqttChannel │   │ （无扩展接口）  │
      │ 读寄存器/线圈  │   │  发布/订阅     │   │  Serial / Sock│
      └───────┬───────┘   └───────┬───────┘   └───────┬───────┘
              │                   │                   │
              ▼                   ▼                   ▼
      ┌──────────────────────────────────────────────────────┐
      │              ChannelBase（抽象基类）                   │
      │  状态机 · 读写串行化(SemaphoreSlim) · 自动重连          │
      │  健康度统计 · 有界数据流 · 事件在锁外触发                │
      └──────────────────────────────────────────────────────┘
              ▲                    ▲                   ▲
    ┌─────────┴────────┐  ┌────────┴───────┐  ┌────────┴────────┐
    │ModbusChannelBase │  │  MqttChannel   │  │ SerialPort /    │
    │  ├ ModbusTcp     │  │                │  │ Socket /        │
    │  └ ModbusSerial  │  │                │  │ EthernetIp /    │
    │    (RTU / ASCII) │  │                │  │ OpcUa           │
    └──────────────────┘  └────────────────┘  └─────────────────┘
```

**为什么不拆成「传输层 + 协议层」两层**：

1. **MQTT 没有可分离的传输层** —— 连接由 Broker 维护，客户端不持有字节流。
   强行分层会得到一个空的传输接口。
2. **分层的唯一收益（传输复用）在本项目拿不到** ——
   它能让 Modbus RTU 与自定义串口共用串口传输，但 NModbus 已自带串口实现。
3. **「读寄存器」与「发布消息」本就不是同一种操作形状**，
   分层后仍要为每种协议定义不同方法集，只是多了一层壳。

而 `ModbusChannelBase` 的抽取与上述结论并不矛盾：
Modbus 的三种变体是**同一协议的不同传输**，抽象是自然的；
Modbus 与 MQTT 是**不同协议**，强行抽象只会得到充满类型判断的基类。

### 2.2 数据流

```
  设备 / Broker
       │
       ▼
┌─────────────────┐   拉模型（Modbus / OPC UA 读）
│  通道实现        │──────────────────────────► ReadAsync 返回值 → Devices
└────────┬────────┘
         │  推模型（MQTT 订阅 / 串口主动上报）
         │
         ▼
┌─────────────────────────────────────┐
│ Channel<T>  有界 · 满时丢弃最旧       │
│ DataReader                          │
└────────┬────────────────────────────┘
         │
         ▼
   Devices 的消费任务 或 其它订阅者
```

### 2.3 背压策略：有界缓冲 + 丢弃最旧

三种选择的权衡：

| 策略 | 后果 | 结论 |
|---|---|---|
| **阻塞** | 反压到设备侧。MQTT 会被 Broker 断开；网络场景下发送方缓冲溢出 | ❌ 比丢数据更糟 |
| **无界缓冲** | 连续运行数周后 OOM | ❌ |
| **丢弃最旧** | 保留最新数据，丢弃量通过 `ChannelHealth.DroppedFrames` 可观测 | ✅ |

**为什么丢弃最旧是对的**：工业监控关心**最新值** ——
气缸的当前状态比 3 秒前的值重要。补发一批历史数据对现场没有价值，
反而会在重连瞬间冲垮数据处理。

容量由 `ChannelConfig.DataBufferCapacity` 控制（默认 1000）。
**丢弃不是静默的**：计数可通过健康度查询，UI 应展示。

### 2.4 线程模型

| 问题 | 结论 |
|---|---|
| 每个通道有独立线程吗？ | **没有。** 连接与读写是 async，由调用方任务驱动 |
| 例外 | 断线后的**重连循环**是后台任务，持 `CancellationToken` |
| 并发读写允许吗？ | **不允许。** `ChannelBase` 用 `SemaphoreSlim(1,1)` 强制串行化 |
| 与 UI 如何交互？ | 通道**不负责** marshal；事件在后台线程触发，UI 用 `UiDispatcher.Post` 切回 |

**串行化不是可选优化**：Modbus RTU 是半双工，并发请求的响应会互相错配 ——
表现是「阀门 A 的值写到了阀门 B」，现场极难定位。

## 3. 重连策略

### 3.1 时序

```
  连接中断（IO 异常 / MQTT DisconnectedAsync）
        │
        ▼
  ┌───────────────────────────────────────┐
  │ TransitionTo(Reconnecting)            │  事件在锁外触发
  │ 事件携带 Attempt 与 NextRetryDelay     │
  └───────────────┬───────────────────────┘
                  ▼
        ┌─────────────────────┐
        │ NextDelay()         │◄── 纯函数：不含等待、不碰时钟
        │ 1s→2s→4s→…→60s     │
        │ ±20% jitter         │◄── 防止多台上位机同步重连
        └─────────┬───────────┘
                  ▼
        ┌─────────────────────┐
        │ await Task.Delay()  │◄── 可被 DisconnectAsync 取消
        └─────────┬───────────┘
                  ▼
        ┌─────────────────────┐
   失败 ──┤   ConnectCoreAsync  ├── 成功
        └─────────────────────┘     │
                  │                   ▼
                  │         ┌───────────────────┐
                  │         │ Reset()           │  退避序列归零
                  │         │ TransitionTo(Connected)
                  └────────►└───────────────────┘
                            等待更长时间后重试
```

### 3.2 参数与理由

| 参数 | 默认 | 理由 |
|---|---|---|
| `InitialDelayMs` | 1000 | 首次重试要快 —— 瞬时抖动应立刻恢复 |
| `BackoffFactor` | 2.0 | 指数增长，避免长时间离线时打满网络 |
| `MaxDelayMs` | 60000 | 上限保证设备恢复后 1 分钟内被发现，而不是「等 24 小时」 |
| `JitterRatio` | 0.2 | **不是可选优化**：多台上位机同时连同一 PLC 时，无抖动会让它们**同步重连**，形成周期性冲击，严重时让刚恢复的设备再次过载 |
| `MaxAttempts` | 0（无限） | 设备可能断电过夜，放弃意味着必须有人到现场重启上位机 |

### 3.3 三个关键设计点

**① 退避计算与等待分离。**

`ExponentialBackoff.NextDelay()` 只返回时长，不执行 `Task.Delay`。
好处是**可测性**：24 个退避测试 **38ms 跑完**。
若把等待放进被测代码，每个边界用例都会变成一次真实的 1s/2s/60s 等待，
整个测试类会慢到没人愿意跑，最终被跳过。

**② 不区分「首次连接失败」与「断线重连」。**

两者对使用者都是「没连上」，区分只会让行为复杂化。
**连接成功后 `Reset()` 退避序列**，因此两种场景自然都被正确覆盖：
首次失败从 1s 开始退避，连接成功后再断开会重新从 1s 开始
（而不是接着上次的 60s）。

**③ 时间与随机源注入。**

- 随机源注入 → 测试传固定种子，抖动断言可重复
- 时间源用 `TimeProvider`（.NET 8 BCL）→ 通道需要「当前时刻」打时间戳，
  而这不是调用方能预先传入的值，因此用 `TimeProvider` 而非 `DateTimeOffset` 参数

**④ 断线判定区分「连接丢失」与「操作失败」。**

`OnConnectionLost(exception)` 返回 `true` 才触发重连。
Modbus 从站返回「非法数据地址」是**操作失败**，连接还在 ——
把它误判为断线会让通道不断重连，而真正的问题（地址配错了）永远修不好。

## 4. 协议实现与选型理由

| 协议 | 状态 | 库 | 许可证 |
|---|---|---|---|
| Modbus TCP | ✅ 完整实现 | NModbus 3.0.83 | MIT（包内 LICENSE.txt 实读） |
| Modbus RTU / ASCII | ✅ 完整实现 | NModbus + 自实现串口适配 | MIT |
| MQTT | ✅ 完整实现 | MQTTnet 5.2.0.1603 | MIT（包内 LICENSE 实读） |
| 裸串口 | 🚧 框架 + TODO | System.IO.Ports | MIT |
| 裸 Socket | 🚧 框架 + TODO | BCL | — |
| EtherNet/IP | 🚧 框架 + TODO | EEIP.NetStandard（待引入） | MIT |
| OPC UA | 🚧 框架 + TODO | LibUA（待引入） | Apache-2.0 |

### 4.1 EtherNet/IP 选型：为什么是 EEIP.NetStandard 而不是 libplctag

**① 适用性**：本项目的 EtherNet/IP 用于 **PC ↔ MES 通讯**，不是连接 AB PLC。
libplctag 的核心价值是 AB/Rockwell 的符号化 Tag 访问 —— 这个场景用不到。
它反而带来负担：依赖 `win-x64` 原生二进制、不支持 CIP Security/TLS、
单个 Tag 实例非线程安全。

**② 许可证**：`libplctag` 1.5.2 的 nuspec 实读为 **MPL-2.0**，
不满足本项目「MIT 或 Apache 2.0」的约束。`EEIP.NetStandard` 为 **MIT**。

**两条理由指向同一结论。**

⚠️ 注意 `EEIP.NET` 这个包名在 NuGet 上**不存在**，真实包名是 `EEIP.NetStandard`。

### 4.2 NModbus 的串口支持：为什么不用 NModbus.Serial

`NModbus.Serial` 3.0.83 的 NuGet 元数据**没有声明任何许可证** ——
nuspec 无 `license` 元素，包内无许可文件。

虽然它与 MIT 许可的 `NModbus` 同仓库、同版本号、同作者，
但「推断出来的许可证」不等于「已确认的许可证」。

而 `IStreamResource` 接口**只有 6 个成员**，用已确认 MIT 的 `System.IO.Ports`
自己实现一遍成本极低（`SerialPortStreamResource.cs`，约 70 行），
且完全在自己的控制之下。因此选择自实现，不引入许可状态不明的依赖。

### 4.3 MQTT 实现要点

- **`Payload` 必须复制**：MQTTnet 5.x 的 `Payload` 是 `ReadOnlySequence<byte>`，
  底层指向连接内部的**池化缓冲区**，回调返回后可能被复用。
  直接持有会导致数据帧读到别的消息内容 —— 这类 bug 只在高负载时偶发，极难复现。
- **消息回调必须快速返回**：它运行在 MQTTnet 的网络接收线程上，
  任何阻塞都会反压到内部处理，最终掉线。基类的 `PublishFrame` 是非阻塞的，正合此场景。
- **连接被拒时不启动重连**：凭据错误、ClientId 冲突重试无意义，直接返回失败。

## 5. 与 Devices 模块的接口契约

```
Devices / App
    │
    │  1. 拿通道
    ├──► CommunicationManager.GetChannel(channelId) → ICommunicationChannel?
    │
    │  2. 拉数据（Modbus / OPC UA 读）
    ├──► ((IModbusChannel)ch).ReadHoldingRegistersAsync(start, count)
    │        → Result<ushort[]>
    │
    │  3. 消费推送数据（MQTT / 串口上报）
    ├──► ch.DataReader  (ChannelReader<ChannelDataFrame>)
    │        有界，满时丢弃最旧；丢弃量见 ChannelHealth.DroppedFrames
    │
    │  4. 健康度
    └──► CommunicationManager.GetHealthReport() → Result<IReadOnlyList<ChannelHealth>>
```

**约定**：

- **地址一律零基**。PLC 手册的「40001」换算成 `0` 由配置层完成，
  通道只接受零基地址。猜错一个偏移量意味着读写到错误的寄存器，在现场是危险事故。
- **协议能力用模式匹配获取**：`if (channel is IModbusChannel modbus) { ... }`。
  不要假设所有通道都能读寄存器。
- **`Result` 与异常的分工**：连接超时、设备无响应用 `Result.Fail`；
  配置错误抛异常（改配置就好，与运行时常态有本质区别）。
- **事件在后台线程触发**，Devices 若需更新 UI 必须自行 marshal。

## 6. 与 Security 模块的权限校验点

通讯模块**只调用 `RequirePermission`，不复制任何权限判断逻辑**。
校验点集中在 `CommunicationManager`，通道实现内部不校验 ——
校验逻辑有两个来源时就一定会不一致。

| 操作 | 权限点 | 对应等级 | 位置 |
|---|---|---|---|
| 查看通道健康度 | `MachineView` | Low | `GetHealthReport()` |
| 连接 / 断开全部通道 | `MachineOperate` | Medium | `ConnectAllAsync` / `DisconnectAllAsync` |
| 加载 / 重载通道配置 | `MachineConfig` | High | `LoadFromConfigurationAsync` / `ReloadAsync` |
| 读寄存器 / 写寄存器 | 由调用方（Devices）校验 | — | 通道本身不校验 |
| MQTT 发布 | 由调用方校验 | — | 通道本身不校验 |

**为什么读写不由通道校验**：通道可能被轮询任务高频调用，
每次都查权限表会带来无谓开销；更重要的是，
**权限属于「谁在用这条通道」，而不是「通道本身能做什么」**。
在调用方（Devices 的轮询服务 / UI 命令）校验语义更清晰。

失败时记录 `Warning` 日志，含 `Operation`、`RequiredPermission`、`Reason`。

## 7. 已知限制与 TODO

### 7.1 未实现的通道

四个骨架通道（SerialPort / Socket / EthernetIp / OpcUa）的**连接管理已实现**，
待补齐的是**数据帧的收发**。每个通道类的 XML 注释里有具体的实现清单。

**EtherNet/IP 与 OPC UA 不引入库、不写猜测代码的理由**：
本机无法访问 GitHub，无法对这些库的 API 做实证探测。
按项目约定「不盲写无法编译验证的第三方库 API」，保留结构 + 待办清单。
实现时先写最小 Demo 探测 API，再补通道。

### 7.2 其他 TODO

- [ ] **通道状态机尚无独立测试**。`ChannelBase` 的状态迁移表与串行化行为
      目前只有代码内的断言，缺少单元测试（需要一个假通道派生类）。
- [ ] **`ChannelBuilders.cs` 把 8 个构建器放在一个文件里**。
      每个只有 5 行且完全同构，拆开只增加导航成本；但若将来某个构建器
      需要额外依赖，应单独拆出。
- [ ] **MQTT 没有测试**。MQTTnet 自带测试用的内嵌服务端，
      可以写「连接 → 订阅 → 发布 → 收到」的往返测试，未实现。
- [ ] **Modbus 没有测试**。需要本地 Modbus 从站模拟器。
      注意：**不要为「网络连通性」写单元测试** —— 那是集成测试的范畴。
- [ ] **通道动态增删的并发安全**。`ReloadAsync` 用 `_lifecycleLock` 串行化，
      但 Devices 若正在用旧通道引用读数据，会在通道被释放后拿到失败结果。
      需要与 Devices 约定「重载前先停止轮询」的时序。
- [ ] **`DataBufferCapacity` 的现场调优**。默认 1000 帧，
      对高频 MQTT 场景可能偏小，需按实测调整。

### 7.3 设计上值得再想一次的点

- [ ] **是否需要一个「共享通道」的概念**。当前每个 `ChannelConfig` 一个通道实例。
      若现场有 3 个元件连同一个 Modbus 从站，它们会共用同一个通道 —— 这是对的。
      但若配置里不小心写了两条指向同一 IP:Port 的通道，会产生两个连接，
      而 Modbus TCP 一般不允许同一从站被两个主站连接。**建议在配置加载时做去重校验。**
- [ ] **写入操作的二次确认**。`AccessDecision.RequireConfirm` 已能正确求值，
      但通讯层的写方法尚未接入确认流程 —— 目前依赖调用方（UI）自行处理。

## 8. 相关文档

- [总体架构](../architecture.md)（§2.2 已按本模块的选型结论修正）
- [Core 模块](01-core.md)
- [Devices 模块](06-devices.md)
- [Security 模块](04-security.md)
- [编码规范 §5 线程与并发](../conventions.md#5-线程与并发)
