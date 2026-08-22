# 百景工具箱 (HyakkeiTools)

## 项目目标
Windows 个人功能中心。设计原则：**极简模块可开关 → 轻量 → 双击 Ctrl 唤起灵动岛式小窗 → 美观优雅**。

## 铁律
1. **每次执行任务前必须先读本文件。**
2. 所有项目文件只写入 `E:\3_Project\HyakkeiTools`。
3. 本文件保持简洁只记要点；设计文档、调研等单独放 `docs/`。

## 形态（v0.2 已确认；命令面板 + 「天青」配色已定稿）
- **岛** = 日常入口：双击 Ctrl 在屏幕上 22% 居中弹出命令面板（亚克力，640 宽高度自适应）。搜索框 + 模块结果列表，↑↓ 选择、回车/**Ctrl+数字 1-9**/点击进入（裸数字永远是输入，因万能输入可以数字开头），Esc 逐级返回；单击 Alt 在「模块面板 ↔ 列表」之间来回切换（列表里按 Alt 回到最近离开的面板，并重新挂载其热键），失焦即隐。
- **配色 = 清新国风「天青」**（用户从设计画布选定）：极简——无图标底块、发丝分隔线、选中为淡青晕 + 小色点；画刷集中在 `IslandColors.Light/Dark.xaml`（`Island.*` 键），随主题自动切换，改配色只动这两个文件。设计稿存 `docs/design/island-colors/`（画布含存档方案）。
- **管理中心** = 原主窗口：模块开关 / 详情 / 设置，仅托盘右键进入，无热键。
- 新增工具 = 模块项目实现 `ITool`（`CreateIslandView` 岛极简视图 + `CreateView` 管理中心页）→ `App.xaml.cs` 注册一行；模块服务经 `ToolContext` 取用。
- **模块会话（隐身挂载）**：模块可实现 `IToolSession`——进入岛面板=激活并接管通用模块热键（`ModuleHotkey`，默认 F6）；岛隐藏不解除；Esc 返回列表/切模块/退出=解除并把 F6 交还主页。唤起岛时若有会话直接回到该模块面板。
- **F6 = 动作键，唤起只归双击 Ctrl**（2026-08-15 用户纠正后定稿）：无模块会话时 F6 归岛——岛隐藏：有选中文字→取词唤岛填入搜索框，无选中→**无反应**；岛列表态：复制当前行的值（算式结果/链接）。进面板后 F6 移交模块（连点器启停 / 翻译取词或截屏）。F6 常驻注册（主页/模块二者其一）。
- **万能输入**：搜索框内容非模块名时列表出现智能行——链接→「打开链接」（回车打开）、算式→「= 结果」（F6 复制不关岛，回车复制并收起）、其余→实现 `IToolQuickInput` 的模块快捷行（翻译）。Ctrl+数字/回车/点击通用。
- 设计原则（用户提醒）：**做之前先想清职责边界**——一个键一种角色，别让热键兼职。

## 技术栈
- .NET 10 + WPF；依赖仅 WPF-UI 4.3（连字符包名）+ H.NotifyIcon.Wpf 2.4。
- 构建：`dotnet build HyakkeiTools.slnx -c Release`；开发运行：`src\Hyakkei.App\bin\Release\net10.0-windows10.0.19041.0\HyakkeiTools.exe`（App/Translator 为 windows10 TFM，解锁 WinRT OCR；Core/AutoClicker 仍为 net10.0-windows）。
- **打包**：`scripts\publish.ps1` → `dist\HyakkeiTools.exe`（单文件 31MB，framework-dependent；`-SelfContained` 可捆绑运行时）。用户日常运行 dist 版，配置/日志在 dist 同目录，脚本重发布时保留。
- 版本控制：已 `git init`（2026-08-15），每次阶段完成提交；`dotnet format whitespace` 统一格式。

## 关键经验
- **UI 文案极简**：界面里不写说明/引导文字（搜索框占位只写「检索」，岛列表只显示模块名不带描述），操作方式写文档不写界面。
- **XAML/C# 里的图标字形必须用 `&#xE7xx;` 实体或 `\uE7xx` 转义**，写 PUA 字面量会静默丢字（已连丢三次：键盘/放大镜/返回箭头/首页卡片）。
- **启动静默**：只进托盘不弹岛；调试重启后也不要自动唤岛打扰用户。
- .ps1 必须纯 ASCII（PS 5.1 按 ANSI 读无 BOM 文件，中文注释吞换行）。
- LL 钩子里 Ctrl 有 0xA2/0xA3（物理）与 0x11（合成）两种码，都要接。
- 双击 Ctrl 状态机必须忽略按住 Ctrl 的自动重复 KEYDOWN，否则 Ctrl+C 接 Ctrl+V 会误触发。
- Storyboard.Completed 必须在 Begin() 之前订阅。
- 后台进程抢前台焦点用 `WindowActivator.ForceForeground`（AttachThreadInput），直接 Activate() 会被前台锁拦截。
- 失焦隐藏别依赖 WPF Deactivated（抢来的前台可能不触发）：用 `ForegroundWatcher`（EVENT_SYSTEM_FOREGROUND）+ "曾拥有前台才隐藏"策略（防抢前台失败时被迟到事件误杀）。
- 隐藏时 `EmptyWorkingSet`：岛隐藏后常驻 ~18MB，岛可见 ~56MB。
- FluentWindow 默认 MinHeight=320，动态高度的小窗必须显式 `MinHeight="0"`。
- 纯代码构造 H.NotifyIcon 的 `TaskbarIcon` 必须调 `ForceCreate(enablesEfficiencyMode: false)`，否则托盘图标不出现。

## 当前状态
- [2026-08-15] **v0.6 整理 + 打包**：Win32 辅助收拢到 Core（`WindowExStyles`、`WindowActivator.IsForeground`）、语种侦测去重（`LanguageDetect`）、岛快捷动作拆为 `IslandWindow.QuickActions.cs`；`scripts/publish.ps1` 单文件发布；`docs/usage.md` 用户手册。
- [2026-08-15] **v0.5**：① 服务商回退链——用户在国外，Auto = 谷歌(gtx 免费接口，已实测) → 百度(有Key时) → 腾讯兜底，失败自动降级，`Tools.translator.Provider` 可强制；② **截屏翻译**——面板「截屏」→ 拉框 → Windows 内置 OCR（Windows.Media.Ocr）→ 自动翻译；③ **法语**——目标分段 自动/中/EN/FR，本地侦测 zh/fr/en（法语靠重音字符+虚词），谷歌源语言交给 `sl=auto`，百度法语代码 `fra`；④ **F6 一键两用**（用户定稿）：有选中文字→划词翻译弹岛，无选中→截屏翻译就地出卡片（译文贴选区旁 + 天青描边，可拖动；卡片内可切目标语言重译、「复制原文/复制译文」，原文即 OCR 结果=文字识别功能）；面板内 Ctrl+1-4 切目标语言（裸数字永远输入）。不加第二热键。
- 经验：Bash/perl 写入非 ASCII 会双重编码，**含中文/重音的改动用 Edit/Write 工具**，Bash 只做纯 ASCII 替换。
- OCR 按语言分模型：用户机器曾只装 zh-Hans OCR 包导致英文识别差；`OcrService` 已改为中→英/法引擎自动选择，但**英/法包需用户自行安装**（设置→语言→勾选光学字符识别，或 `Add-WindowsCapability Language.OCR~~~en-US~0.0.1.0`）。
- [2026-08-14] v0.4 翻译（输入即翻 + 划词 F6）；v0.3 连点器。微软 Edge 免费翻译通道已失效（404），勿再用。
- 待用户实测截屏翻译与谷歌链路；后续按需加新模块。

## 文档索引
- [docs/architecture.md](docs/architecture.md) — 架构设计、选型调研、P0/v0.2 实施记录
- [docs/design/DESIGN.md](docs/design/DESIGN.md) — **界面设计规范**（色彩/字号/间距/控件/模块面板模板，做任何 UI 前必读）
- [docs/usage.md](docs/usage.md) — 用户手册（所有操作方式的唯一归宿，界面不写说明）
