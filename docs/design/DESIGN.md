# 百景工具箱 · 界面设计规范 (DESIGN)

> 创建：2026-08-14 ｜ 适用：岛（命令面板）、模块岛视图、管理中心
> 参考：[Fluent 2 间距令牌](https://fluent2.microsoft.design/design-tokens)（4px 基准阶梯）、[Fluent 2 布局](https://fluent2.microsoft.design/layout)、[Apple HIG](https://developer.apple.com/design/human-interface-guidelines/)（8pt 网格、44pt 触控目标、分段控件、Clarity/Deference 原则）

## 0. 总原则

1. **极简文案**：界面不写说明/引导文字；占位、按钮、标签一律用最短词（「检索」「开始」）。操作方式写文档，不写界面。
2. **单强调色**：天青（浅 `#6E99A1` / 深 `#8FB6BD`）只用于四处——选中态、运行状态、主按钮、光标。其余全部灰阶。一屏内强调色出现 ≤3 处。
3. **4px 网格**：所有间距、尺寸取 4 的倍数；同类间距全局一致。
4. **每行一个关注点**：模块面板按"行"组织，一行只放一组相关控件；行内左侧放选择/输入，右侧放动作/状态。
5. **克制装饰**：无图标底块、无阴影叠加、无焦点框（`FocusVisualStyle` 置空或避免程序化聚焦容器）；层级靠间距和字色，不靠框线。

## 1. 色彩（画刷键 `Island.*`，定义于 IslandColors.Light/Dark.xaml）

| 键 | 用途 | 浅色 | 深色 |
|---|---|---|---|
| Island.Bg | 面板底 | #F0FAFCFC | #F21E2425 |
| Island.TextPrimary | 正文/控件文字 | #363B3A | #E6EBE9 |
| Island.TextSecondary | 次要文字（描述、未选段） | #9AA19C | #97A19E |
| Island.TextTertiary | 辅助（占位、提示、待机状态） | #ABB1AB | #6E7876 |
| Island.IconDefault | 未选中图标 | #8A8F8A | #939B99 |
| Island.Hairline | 发丝线/描边 | 7% 墨 | 8% 白 |
| Island.HoverWash | 悬停晕 | 4% 墨 | 6% 白 |
| Island.TrackWash | 分段控件轨道底 | 5% 墨 | 7% 白 |
| Island.SegmentOn | 选中段底 | 不透明白 | #2C3435 |
| Island.SegmentOnBorder | 选中段描边 | 8% 墨 | 12% 白 |
| Island.Accent | 强调 | #6E99A1 | #8FB6BD |
| Island.AccentWash | 强调晕（选中行/主按钮底） | 8% | 12% |
| Island.AccentBorder | 主按钮描边 | 30% | 30% |

## 2. 字号阶梯（Segoe UI / Microsoft YaHei）

| 级 | 字号 | 用途 |
|---|---|---|
| 输入 | 16 | 岛搜索框 |
| 标题 | 14 | 列表项名称 |
| 控件 | 13 | 按钮、分段、输入框 |
| 辅助 | 12 | 展开态头部、字段后缀 |
| 提示 | 11 | 描述行、键位提示、角标 |

字重：正文 Regular；仅数值/状态可 SemiBold。禁用 11 以下字号。

**行高**（2026-08-15 用户反馈"翻译看着难受"后定）：中文正文行高 ≈ 字号 × 1.6——译文 15/24、面板译文 14/22、原文 13/20。WPF `TextBox` 不能设行高，可选中的正文一律用 `SelectableText`（Translator 模块内，RichTextBox 封装）；段落阅读宽度 480–600。多段内容用 11 号灰色小标（「译文」「原文」）分区，不靠框线。

## 3. 间距与尺寸

- 间距阶梯：**4 / 8 / 12 / 16 / 20**；行间距 12，组间距 16，面板内容边距 20（左右）。
- 控件高度：岛列表行 **40**（只放图标 + 名称 + 序号，**不写描述**——用户定稿"越简单越好"；描述只出现在管理中心模块页）；按钮/输入框 **32**；分段控件轨道 **30**（含 2px 内边距）。
- 圆角（2026-08-14 用户定稿，整体偏小）：面板随系统（DWM Round）；行/按钮/输入框 **6**；分段轨道 **6**、段 **5**；小角标/预设 chip **4**。
- 展开面板内容高度上限 300。

## 4. 控件规范

### 分段选择器（Segmented，2–4 段互斥选择）
- 轨道：`TrackWash` 底、圆角 9、内边距 2。
- 段：内边距 12,5、圆角 7、字号 13；未选 `TextSecondary`，悬停 `HoverWash`；**选中 = `SegmentOn` 底 + `SegmentOnBorder` 描边 + `TextPrimary`**（不用强调色，把强调色留给状态与主按钮）。
- 样式键：`Island.Segment`（RadioButton）；轨道用 Border 手排。

### 输入框（数值/短文本）
- 发丝描边、圆角 9、高 32、文字 13 居中；单位后缀放框内右侧（11 号 `TextTertiary`）。
- 不可用时整体降为 40% 不透明度。

### 按钮
- 次级 `Island.Button`：透明底 + 发丝描边，悬停 `HoverWash`。
- 主按钮 `Island.PrimaryButton`：`AccentWash` 底 + `AccentBorder` 描边 + `Accent` 文字。一个面板 ≤1 个。

### 预设角标（Chip）
- 常用值的一键预设（如频率「1/秒」）：11 号字、发丝描边、圆角 4、内边距 8,3，样式键 `Island.Chip`；跟随其关联字段一起禁用/降透明度。

### 状态点
- 8px 圆；待机 `TextTertiary`、运行 `Accent`。放在其描述的动作按钮左侧 12px。

### 键位提示
- 纯文字 11 号 `TextTertiary`（如 `F6`、`Esc`），不加边框（搜索行的 Esc 角标除外——已定型不回改）。

## 5. 模块岛视图模板

```
行1  [主选择组：分段控件]            [次选择组：分段控件]
行2  [参数输入]        （弹性空隙）  [状态点] [主按钮] [键位提示]
```

- 新模块先套此模板；放不下再申请第三行，高度勿超上限。
- 所有配色引用 `Island.*` 动态资源；禁止硬编码颜色。
- 视图内禁止程序化 `Focus()` 容器（会画出焦点框）；需要捕获按键时挂窗口级 `PreviewKeyDown`。

## 6. Do / Don't

- ✅ 打开即可用：默认值合理，零配置可开始。
- ✅ 键盘可完成全部高频操作。
- ❌ 界面出现整句说明文字。
- ❌ 同屏两个主按钮 / 两种强调色。
- ❌ 控件贴边、间距不成 4 的倍数。
