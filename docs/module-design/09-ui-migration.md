# AntdUI 界面替换清单（阶段十执行）

> **本文档是阶段十的作业指导，不是设计说明。**
> 所有结论都来自实测（`AntdUiProbeTests` / `AntdUiAliasStrategy*Tests` /
> `AntdUiDefaultButtonProbeTests`），不是推测。与实测不符时以实测为准。

---

## 1. 为什么需要这份清单

`AntdUI` 已引入但未使用，App 的 8 个窗体全部是标准 WinForms。
替换的难点**不在于控件数量**，而在于三件在构造冒烟测试里完全看不出来的事：

| 问题 | 现状 | 后果 |
|---|---|---|
| 控件名与 WinForms 大面积重名 | `AntdUI.Button` vs `System.Windows.Forms.Button` | 编译期 CS0104，必须逐文件处理 |
| 事件契约被遮蔽 | `PerformClick` 不触发 `Click` | 任何「模拟点击」的测试写法失效 |
| 回车提交失效 | `AcceptButton` 语义不再成立 | **现场感受：输完密码按回车没反应** |

第三条最危险：它编译通过、构造不抛异常、现有全部测试保持绿色，
只有人坐在机器前敲一次回车才会发现。

---

## 2. 别名策略（已定）

**全局别名 + 项目专属前缀**，在 `EquipmentMonitor.App.csproj` 声明：

```xml
<ItemGroup>
  <Using Include="AntdUI.Button" Alias="AntdButton" />
  <Using Include="AntdUI.Input"  Alias="AntdInput" />
  <Using Include="AntdUI.Select" Alias="AntdSelect" />
  <Using Include="AntdUI.Window" Alias="AntdWindow" />
  <!-- 其余按实际用到的控件逐个添加 -->
</ItemGroup>
```

**理由**（三种策略都写了最小 Demo 实测，见 `AntdUiAliasStrategy1Tests` /
`2Tests` / `3Tests`）：

- 策略 1（每窗体 using 别名）：8 个窗体就要写 8 遍，漏一个是一次 CS0104；
  且别名拼写会在各文件间漂移。
- 策略 3（全限定名）：过渡期两种控件同处一个窗体时，
  `Button` 与 `AntdUI.Button` 要逐字符看才能分清。
- **策略 2 的代价是「读单个文件看不出别名从哪来」，
  用一个说明来源的前缀（`AntdXxx` 而非 `Button2`）即可抵消。**

⚠️ **不要写 `<Using Include="AntdUI" />`**（整个命名空间导入）——
那会把 AntdUI 的全部类型拉进全局，重名冲突以 CS0104 散落到各文件，
而错误信息不会告诉你「你导入了一个不该导入的命名空间」。

---

## 3. 替换顺序

由易到难，**每完成一个立即实跑确认**：

1. `LoginForm`（控件最少，且回车提交最关键，先拿它验证机制）
2. `ChangePasswordForm`
3. `MainForm`（导航）
4. `DeviceConfigForm`
5. `DeviceDebugForm`
6. `MesPushPage`
7. `UserManagementForm`
8. `LogViewerForm`

---

## 4. 每个窗体替换后必做

| 检查项 | 具体做法 |
|---|---|
| **布局** | 实跑一次，肉眼确认。AntdUI 的默认边距/字体与 WinForms 不同 |
| **回车提交** | 焦点放在密码框，敲回车，确认真的提交了。**不能只看代码** |
| **Tab 顺序** | 逐个 Tab，确认焦点顺序与原窗体一致 |
| **快捷键** | 若有 `&` 助记符或 `Ctrl+X`，逐个按一遍 |
| **取消路径** | 改密窗的「取消」、主窗体的关闭，确认仍然可用 |
| **全量测试** | `dotnet test`，确认 975 条不回退 |

---

## 5. 回车提交：用方案 1

### 实测结论（两个预设都被翻转）

1. **`AntdUI.Button` 确实实现了 `IButtonControl`** —— 所以
   `AcceptButton = antdButton` **能编译**。但它的 `PerformClick` 不触发 `Click`
   —— **编译通过完全不能说明回车能用**。

   > **⚠️ 阶段十第一轮更正（实测，2026-09-30）**
   >
   > 本节原先写的成因是「它的 `Click` 被 `new` 遮蔽，`PerformClick` 走的是
   > 基类那一份」。**这个成因是错的**，反射实测（AntdUI 2.4.11）：
   >
   > - `AntdUI.Button.Click` 的 `DeclaringType` 是 `System.Windows.Forms.Control`
   >   —— 它**没有**被遮蔽，全链路只有这一个 `Click` 事件；
   > - 显式调用 `Control.OnClick` 会让 `Click` **正常触发**（实测 1 次）；
   > - `Control` **根本没有** `PerformClick` 方法，所以也说不上「走基类那一份」。
   >
   > 真实成因是 `AntdUI.Button.PerformClick()` 是它自己实现的方法
   > （实现 `IButtonControl`，`DeclaringType` 为 `AntdUI.Button`），
   > 该方法在控件挂到窗体上之后有额外的内部前置条件，条件不满足就静默返回。
   > 具体是哪个条件需要读它的源码才能确定 —— **但结论不受影响**：
   > `PerformClick` 不可靠，不要用它。之所以要更正成因，是因为
   > 「事件被遮蔽」会让人以为「绑 `Click` 也不可靠」，从而在下个窗体里
   > 避开一个其实完全正常的用法。
2. **「隐藏的原生按钮当代理」不可行**：`ButtonBase.PerformClick` 要求
   `CanSelect`，而它依赖控件及父链的 `Visible`。隐藏的代理按钮
   永远不触发；即使可见，只要窗体从未显示过也同样不触发。

### 采用方案：`KeyPreview` + `KeyDown`

```csharp
KeyPreview = true;

KeyDown += (_, e) =>
{
    if (e.KeyCode == Keys.Enter)
    {
        e.SuppressKeyPress = true;   // 否则回车会先在焦点控件上走一遍，可能提交两次
        OnSubmit();                  // 与按钮 Click 调用同一个方法
    }
};
```

**理由**：只用到 `Form.KeyPreview` 与 `Control.KeyDown` 两个原生成员，
不依赖任何控件的契约；可以直接驱动、无需消息循环（因此可测）；
且能表达「多行文本框里回车应当换行」这类判断。

**代价**：每个需要回车提交的窗体各写一遍。
等替换到第二个窗体、确认形状一致后，再抽成
`form.EnableEnterSubmit(action)` 扩展方法 —— 现在抽是为一个还没出现的重复做设计。

### ⚠️ 不要用 `PerformClick` 驱动 AntdUI 按钮

实测发现它的行为**随挂载方式改变**：挂在窗体上不触发，
独立存在时触发。一个行为会随挂载改变的成员，不该用来驱动测试。
（`AntdUiDefaultButtonProbeTests` 里有一条「活断言」盯着这个不一致，
若 AntdUI 修好了它会变红，提醒回来重新评估。）

---

## 6. 收尾

替换完成后：

- **删除**三个策略探测文件（`AntdUiAliasStrategy1Tests` / `2Tests` / `3Tests`）
  与 csproj 里的 `AliasProbe*` 别名 —— 它们只为选型存在，选定了就该消失，
  留着会让人以为那是既定约定。
- **保留** `AntdUiProbeTests`（API 可用性护栏）与
  `AntdUiDefaultButtonProbeTests`（契约差异护栏）——
  AntdUI 升级换了 API 时，它们会先红。
- 更新本文件：把「阶段十执行」改成完成记录，并记下实际遇到的偏差。

---

## 7. 轮次记录（实测偏差）

> 每轮替换后在这里追加。**只记「下一轮会踩到的坑」**，
> 不重复本文件前面已经写过的内容。

### 第一轮：LoginForm（2026-09-30）

对 AntdUI 2.4.11 逐项实测，发现 **4 条默认值/API 与 WinForms 不同**，
其中前两条会静默产生「看不见的控件」和「Tab 停在文字上」，
构造冒烟测试与既有 986 条测试**一条都不会红**：

| # | 差异 | 实测值 | 后果 | 处理 |
|---|---|---|---|---|
| 1 | `AntdUI` 控件的 `Size` | 构造后恒为 **`0x0`**，且**不会自己长高**；`Input`/`Select` 的 `PSize` 恒为 `9x9`，`AutoSize = true` 也只得到 `9x9` | 只给 `Width` 的输入框高度为 0 —— **界面上一片空白** | 凡输入类控件**必须显式给 `Height`** |
| 2 | `AntdUI.Label.TabStop` | 默认 **`true`**（WinForms `Label` 是 `false`） | 每个文字标签都进入 Tab 顺序，按 Tab 会停在「用户名」「密码」上 | 每个 `AntdLabel` 显式 `TabStop = false` |
| 3 | `AntdUI.Select.SelectedItem` | **不存在**；取选中项用 `SelectedValue`（`object`） | 编译期就报错，不会静默 | 用 `SelectedValue as string` |
| 4 | `AntdUI.Select.SelectedIndexChanged` | 处理器是 `IntEventHandler`（`object, IntEventArgs`），**不是** `EventHandler` | — | 方法组转换支持逆变（形参取基类），`void H(object?, EventArgs)` **可直接订阅**，无需 lambda 适配 |
| 5 | `AntdUI.Select` 的 `DropDownStyle` | 不存在（本就是「只能选不能输入」语义） | — | 原 `ComboBoxStyle.DropDownList` 一行直接删掉 |

**另外两条为后续轮次探明的结论：**

- **回车不会被 AntdUI 控件在消息层吞掉。** 实测 `ProcessCmdKey(Enter)`
  在 `Input` / `Select` / `Button` 上**全部返回 `false`**（`Select` 只对方向键
  返回 `true`，用于展开下拉）。因此「焦点在密码框时敲回车」会正常到达窗体的
  `KeyPreview` + `KeyDown` —— 方案 1 成立。**这条是回车提交能用的实测依据。**
- **`Control.Click` 没有被遮蔽**（见 §5 的更正）：`AntdUI.Button.Click`
  就是 `System.Windows.Forms.Control.Click`，显式调 `OnClick` 能正常触发。
  后续窗体**可以**正常用 `button.Click += ...`，不必绕。

**上一轮的输入（本轮已做）**：`EquipmentMonitor.App.csproj` 加了 5 个别名
（`AntdButton` / `AntdInput` / `AntdSelect` / `AntdLabel` / `AntdCheckbox`）。
**下一轮（ChangePasswordForm）预计需要追加**：按该窗体实际用到的控件增补，
大概率还要 `AntdPanel`。⚠️ 类名是 `AntdUI.Checkbox`（小写 b）。

**新增护栏**：`LoginFormTests`（5 条）。其中「回车」与「Tab 顺序」两组
已用**反证**验过会红（把 `KeyPreview` 关掉、或去掉一个 `TabStop = false`，
对应用例立刻失败），因此它们是护栏而不是装饰。
