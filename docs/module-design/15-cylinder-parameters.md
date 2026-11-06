# 气缸参数设计（接口设计）

> 状态：**接口设计**（2026-11-01）。与 [13-axis-parameters.md](13-axis-parameters.md) 共用
> `ElementParameter*` 值模型；气缸只保留自己的参数键与目录。

## 1. 范围

气缸参数是**设备配置**，不是运动曲线：

- 名称、工位、启用状态；
- 伸出/缩回电磁阀输出点；
- 伸出/缩回到位限位输入点；
- 动作超时；
- 备注。

气缸没有控制器参数读写能力；当前所有参数都是 `Restart`，保存配置后重新装配/重启生效。

## 2. 共用模型

气缸与轴共用：

- `ElementParameterDescriptor`
- `ElementParameterSet`（`ElementId` + `Values` + `Version`）
- `ElementParameterSnapshot`
- `ElementParameterValidationIssue/Result`
- `ElementParameterChangeResult`
- `ParameterScope` / `ParameterApplyMode` / `ParameterUpdateMode`
- `IElementParameterStore`

轴/气缸各自保留：

- `AxisParameterKeys` / `AxisParameterCatalog`
- `CylinderParameterKeys` / `CylinderParameterCatalog`
- `IAxisParameterService` / `ICylinderParameterService`
- `IAxisParameterController`（只有轴有控制器参数能力）

## 3. 参数目录

`CylinderParameterCatalog.Create()` 返回：

| 分组 | 参数键 | 类型 | 单位 | Scope | 权限 | ApplyMode |
|---|---|---|---|---|---|---|
| 身份 | `Name` | Text | — | Device | `MachineConfig` | Restart |
| 身份 | `StationId` | Number | — | Device | `MachineConfig` | Restart |
| 身份 | `IsEnabled` | Bool | — | Device | `MachineConfig` | Restart |
| IO 点 | `ExtendOutput` | Number | — | Device | `MachineConfig` | Restart |
| IO 点 | `RetractOutput` | Number | — | Device | `MachineConfig` | Restart |
| IO 点 | `ExtendedInput` | Number | — | Device | `MachineConfig` | Restart |
| IO 点 | `RetractedInput` | Number | — | Device | `MachineConfig` | Restart |
| 动作 | `ActionTimeoutSeconds` | Number | s | Device | `MachineConfig` | Restart |
| 身份 | `Description` | Text | — | Device | `MachineConfig` | Restart |

说明：

- `RetractOutput` 不设置表示单控（弹簧复位）；双控时必须与 `ExtendOutput` 不同；
- `ExtendedInput` / `RetractedInput` 不能使用同一个输入点；
- 点号范围按 `ushort` 上限给 UI 约束，实际控制器能力由实现校验。

## 4. 服务接口

```csharp
public interface ICylinderParameterService
{
    Task<Result<ElementParameterSnapshot>> GetAsync(string elementId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ElementParameterSnapshot>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<ElementParameterValidationResult>> ValidateAsync(
        string elementId, ElementParameterSet proposed, CancellationToken ct = default);
    Task<Result<ElementParameterChangeResult>> UpdateAsync(
        string elementId, ElementParameterSet proposed,
        ParameterUpdateMode mode, CancellationToken ct = default);
}
```

更新模式：

- `SaveOnly`：支持；保存配置，重启/重新装配后生效；
- `ApplyOnly` / `SaveAndApply`：当前返回
  `ElementParameterErrorCodes.ControllerUnsupported`，因为气缸没有运行时参数端口。

## 5. 校验

描述符负责单参数形状（类型/范围/Precision）；跨字段规则在
`CylinderParameterValidator` 中实现，不与轴共享：

- `ExtendOutput` / `RetractOutput` 不能相同（双控时）；
- `ExtendedInput` / `RetractedInput` 不能相同；
- `ActionTimeoutSeconds > 0`；
- 单控时 `RetractOutput` 必须缺省或为空。

轴有自己的 `AxisParameterValidator`（速度/加速度/软限位/缩放），两者规则不共用。

## 6. 权限与审计

| 动作 | 权限 |
|---|---|
| 查看参数 | `MachineView` |
| 修改参数 | `MachineConfig` |

审计事件与轴参数一致：`ELEMENT_PARAMETER_UPDATED` / `ELEMENT_PARAMETER_REJECTED`；
记录元件、参数键、旧值、新值、结果。

## 7. 测试

- 目录包含 IO/超时/身份参数，键唯一；
- 所有参数为 Device / `MachineConfig` / Restart；
- `RetractOutput` 描述单控语义；
- 超时下限为正；
- `CylinderParameterValidator`：IO 冲突、超时、单控语义；
- 后续服务实现补：权限、`SaveOnly`、`ControllerUnsupported`。

## 8. 待落地

- `ICylinderParameterService` 实现；
- `IElementParameterStore` 的 JSON/SQLite 实现（轴/气缸共用）；
- Desktop 参数设计页（轴/气缸共用描述符渲染）；**模板已交付（2026-11-02）**：
  `ParameterSettingsPage` + `ParameterEditorControl`；当前用内存示例源，真实气缸参数服务接入后替换 `IParameterPageSource`；
- 气缸运行时参数应用（当前无 `ICylinderParameterController`）。
