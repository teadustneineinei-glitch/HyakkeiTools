# 百景工具箱 — 架构设计与选型调研

> 创建：2026-08-13 ｜ 状态：技术栈已确认（C# 熟悉），P0 骨架已完成（2026-08-14）
>
> 用户设计原则：**1) 先构建框架 2) 轻量 3) 快速唤起快速隐藏 4) 美观优雅**

## 1. 需求回顾

- **目标**：一个客制化的个人功能中心（Windows 11），整合连点器、翻译、自制小工具等，避免每个功能单独装一个软件。
- **关键能力需求**：
  - 连点器 → 全局键鼠钩子（监听热键）+ 模拟输入（SendInput）
  - 翻译 → HTTP 调用翻译 API + 全局热键 + 剪贴板/划词取词
  - 自制小工具 → 需要模块化/插件化架构，方便随时加新功能
  - 常驻托盘、全局热键唤起
- **约束**：仅个人使用、仅 Windows 平台，不需要跨平台。

## 2. 候选架构调研（2026-08 网络检索结论）

### 2.1 框架横向对比

| 方案 | 优点 | 缺点 | 适合度 |
|---|---|---|---|
| **C# / .NET + WPF** | Windows 原生；全局钩子/模拟输入生态最成熟（globalmousekeyhook、WindowsInput 等现成库）；托盘/热键/Win32 互操作简单；单文件发布 | 仅 Windows（对本项目无所谓）；UI 默认样式老旧（可用 Fluent 主题/WPF UI 库解决） | ★★★★★ |
| **Tauri 2 (Rust + WebView)** | 体积极小（约为 Electron 的 4%）、内存占用低（30–40MB）；前端写 UI 灵活 | 连点器所需的底层钩子要写 Rust（InputBot/rdev 生态不如 C# 成熟）；前后端两套语言，个人项目复杂度高 | ★★★☆ |
| **Electron** | 生态最大、上手快 | 常驻内存 200–300MB，作为常驻托盘工具太重；底层钩子仍需原生模块 | ★★ |
| **WinUI 3** | 微软最新原生框架、Fluent 设计 | 工具链仍有坑，生态和资料少于 WPF；对个人项目收益不明显 | ★★★ |
| **Flutter / MAUI / Qt** | 各有所长 | 均以跨平台为卖点，本项目用不上；底层 Win32 交互都要额外绕 | ★★ |

### 2.2 同类产品架构参考（uTools / Quicker / ZTools）

业界成熟的"工具箱"类产品共同的架构模式：

- **核心 + 插件**：主程序只负责共享基础设施（窗口壳、热键、搜索框、设置、插件生命周期），每个功能是独立插件/模块。
- **明确定义的插件接口**：插件通过统一接口（如 `ITool`）与核心交互，核心不依赖任何具体插件。
- **"用完即走"**：全局热键唤起 → 使用功能 → 隐藏回托盘。

## 3. 推荐方案

### 3.1 技术栈：**C# / .NET（8 LTS 或 10 LTS）+ WPF，模块化单体架构**

选择理由：

1. **连点器是硬需求**，需要 `SetWindowsHookEx` 全局钩子 + `SendInput` 模拟输入，C# 在这方面的库最成熟、资料最多，Win32 P/Invoke 也最顺手。
2. 本机已有 Unity 开发环境，说明对 C# 熟悉，学习成本最低（若此判断有误请指出）。
3. 仅 Windows、个人使用 → Tauri/Electron 的跨平台与 Web 生态优势用不上，反而引入双语言复杂度。
4. WPF 成熟稳定，配合 [WPF UI](https://github.com/lepoco/wpfui)（Fluent 风格）或 HandyControl 可获得现代观感。
5. 单文件发布（self-contained 或 framework-dependent），无需安装器。

### 3.2 架构：核心宿主 + 工具模块（先编译期模块化，后可升级为动态插件）

```
HyakkeiTools/
├── CLAUDE.md                      # Claude 工作记录（保持简洁）
├── docs/                          # 设计文档、调研记录
├── src/
│   ├── Hyakkei.App/               # WPF 宿主：主窗口壳、导航、托盘、设置页
│   ├── Hyakkei.Core/              # 契约与基础设施（不依赖任何具体工具）
│   │   ├── ITool.cs               #   工具接口：Id/名称/图标/CreateView()/OnActivate/OnDeactivate
│   │   ├── GlobalHotkeyService    #   全局热键注册与分发
│   │   ├── InputSimulator 封装     #   SendInput / 钩子（供连点器等使用）
│   │   ├── ConfigService          #   JSON 配置读写（每工具独立配置节）
│   │   └── LogService             #   日志
│   └── Tools/
│       ├── Hyakkei.Tool.AutoClicker/   # 连点器模块
│       ├── Hyakkei.Tool.Translator/    # 翻译模块
│       └── (以后每个新功能一个模块项目)
└── HyakkeiTools.sln
```

设计要点：

- **核心零依赖原则**：`Hyakkei.Core` 只定义接口和基础设施；`Hyakkei.App` 通过 `ToolRegistry` 发现并挂载所有 `ITool` 实现；工具模块之间互不引用。
- **先静态后动态**：初期工具模块直接被 App 引用（编译期组装），简单可靠；未来若想做成 uTools 式动态插件，再引入 `AssemblyLoadContext` 动态加载 DLL，接口不用改。
- **常驻形态**：开机可自启 → 常驻托盘 → 全局热键唤出主面板（uTools 式"用完即走"）。
- **配置与数据**：统一存放于项目/程序目录下的 `config/`，JSON 格式，每个工具一个配置节，便于备份迁移。

### 3.3 分阶段实施计划

| 阶段 | 内容 | 产出 |
|---|---|---|
| P0 | 搭建解决方案骨架：App 壳 + Core + 托盘 + 全局热键 + 空的工具导航 | 可运行的空壳 |
| P1 | 第一个工具：**连点器**（验证钩子/模拟输入基础设施） | 可用的连点器 |
| P2 | 第二个工具：**翻译**（验证网络/热键/剪贴板基础设施） | 可用的翻译 |
| P3 | 按需追加自制小工具；视需要引入动态插件加载 | 持续迭代 |

## 3.4 P0 实施记录（2026-08-14 完成）

- **环境**：.NET 10 SDK（10.0.203），TargetFramework `net10.0-windows`。解决方案为 .NET 10 新版 `.slnx` 格式，构建命令须直接指定：`dotnet build HyakkeiTools.slnx -c Release`。
- **依赖仅两个**：[WPF-UI 4.3.0](https://github.com/lepoco/wpfui)（Fluent 界面，注意包名是 `WPF-UI` 连字符，`WPF.UI` 是无关老包）＋ [H.NotifyIcon.Wpf 2.4.1](https://github.com/HavenDV/H.NotifyIcon)（托盘）。无 MVVM 框架，代码后置事件直连。
- **窗口形态**：`FluentWindow` + Mica 云母背景 + 圆角 + 标题栏融合；侧栏为手写 ListBox 导航（圆角悬停 + 主题色指示条），右侧内容区分层背景。
- **快速唤起/隐藏**：窗口常驻不销毁，只做 Show/Hide；热键语义为"可见就藏、不可见就唤"（不要做"先置前再隐藏"——后台进程 Activate 会被前台锁拦截，导致藏不掉）。Esc / 关闭按钮 / 失焦（可选）都走隐藏；真正退出只在托盘菜单。
- **轻量实测**：隐藏到托盘时调用 `EmptyWorkingSet` 收缩工作集，常驻 **18MB**；唤起后按需取回页面（约 35MB 起）。可见时工作集 ~227MB 属 WPF+Mica 正常水平。
- **单实例**：Mutex + EventWaitHandle，重复启动会唤出已运行实例。
- **配置/日志**：程序目录 `config/settings.json`（含每工具配置节 `Tools.<toolId>`）、`logs/app-日期.log`，绿色便携。
- **坑**：Windows PowerShell 5.1 把无 BOM 的 UTF-8 脚本按 ANSI(GBK) 解析，中文注释会吞掉换行导致诡异错误 —— 本项目 `.ps1` 一律纯 ASCII（见 `scripts/gen-icon.ps1`）。
- **图标**：`scripts/gen-icon.ps1` 生成渐变圆角"百"字 logo（PNG + 多尺寸 ICO），改样式后重跑即可。

## 3.5 v0.2「岛化」实施记录（2026-08-14 完成）

用户新增设计决策：**模块极简可开关；双击 Ctrl 唤起；唤起的是灵动岛式小窗，不是整个工具箱**（参考 [DynamicWin](https://github.com/FlorianButz/DynamicWin)、[PILLAR](https://github.com/warpirate/pillar-dynamic-island-for-windows)、[Listary 双击 Ctrl](https://help.listary.com/hotkeys)）。

**形态**：
- **岛（日常入口）**：屏幕顶部中央深色圆角胶囊（480 宽，恒深色不随主题），无边框置顶、不占任务栏、不进 Alt+Tab（WS_EX_TOOLWINDOW）。平时完全隐藏，双击 Ctrl 唤起。
  - 紧凑态（高 80）：搜索框（自动聚焦，可过滤模块）+ 已启用模块图标行（带数字键提示）。
  - 展开态：点图标 / 按数字 1-9 / 回车 → 高度动画展开为模块极简面板（`CreateIslandView`，高度上限 300）。
  - Esc 逐级返回（面板→紧凑→隐藏），失焦自动隐藏，隐藏时收缩工作集。
- **管理中心（原主窗口降级）**：模块开关（`DisabledTools` 配置）、模块详情页、主题设置。无热键，托盘右键进入；仅首次运行自动弹出（`FirstRunDone`）。

**技术记录**：
- 双击 Ctrl：`RegisterHotKey` 不支持纯修饰键 → `KeyboardHookService`（WH_KEYBOARD_LL 低级钩子，Core 内，连点器后续复用）。判定：Ctrl 单独按-放（无其他键插入），间隔 400ms 内再按下即触发。
- **坑 1**：物理 Ctrl 到达 LL 钩子是 `VK_LCONTROL(0xA2)`，但 `keybd_event` 合成的通用 `VK_CONTROL(0x11)` 不会被翻译，两种码都要接。
- **坑 2**：WPF Storyboard 的 `Completed` 必须在 `Begin()` **之前**订阅，事后订阅永远不触发（曾导致隐藏回调卡死）。
- **坑 3**：后台进程 `Activate()` 被前台锁拦截，岛弹出后拿不到键盘焦点、按键漏到前台应用 → `WindowActivator.ForceForeground`（AttachThreadInput + SetForegroundWindow，启动器类软件通用做法），实测有效。
- 岛专用样式在 `IslandTheme.xaml`（`Island.Button` 等），模块岛视图用 `DynamicResource` 引用，无编译依赖。
- 实测：岛可见时整机工作集 ~56MB，隐藏后 ~18MB。

## 3.6 风格 A「命令面板」定稿（2026-08-14）

用户从四个候选风格（命令面板 / 顶部工具条 / 侧边飞出栏 / 径向轮盘）中选定 **A 命令面板**（Raycast / uTools 风）：

- 岛从"顶部恒深色胶囊"改为**屏幕上 22% 居中的命令面板**：640 宽、高度随内容自适应，跟随系统深浅色 + Acrylic 亚克力背景（`FluentWindow` + `WindowBackdropType="Acrylic"`），DWM 圆角与阴影。
- 紧凑态 = 大搜索框（16px 字号）+ **模块结果列表**（图标块 + 名称 + 描述 + 数字键角标），支持 ↑↓ 选择（循环）、回车进入选中项、数字 1-9 直达、鼠标点击进入；过滤时高度即时调整，进入/返回时播 170ms 高度动画。
- 展开态、Esc 逐级返回、失焦隐藏、内存收缩等逻辑不变。
- 模块岛视图配色契约改为：使用 WPF-UI 主题动态画刷（不再假设深色背景），`IslandTheme.xaml` 提供主题自适应的 `Island.Button` / `Island.IconButton`。
- **坑 4**：`FluentWindow` 默认 `MinHeight=320`，会顶住动态高度，必须显式 `MinHeight="0"`。
- **坑 5**：纯代码构造 H.NotifyIcon 的 `TaskbarIcon` 不会自动注册托盘图标，必须调 `ForceCreate(enablesEfficiencyMode: false)`（false 避免进程被降入 EcoQoS）。

## 3.7 配色定稿「天青」（2026-08-14）

两轮设计画布（`docs/design/island-colors/`，已发布为 Artifact）后用户选定**清新国风·极简**方向的 **E · 天青**：

- **视觉语言**：无图标底块（细线 glyph 直接放置）、数字/Esc 提示为纯文字无描边框、发丝分隔线；选中行 = 8% 天青晕 + 名称旁 5px 色点 + 图标/数字变强调色。
- **色板**（浅色 / 深色）：底 `#F0FAFCFC` / `#F21E2425`；正文 `#363B3A` / `#E6EBE9`；次要 `#9AA19C` / `#97A19E`；辅助 `#ABB1AB` / `#6E7876`；强调（天青）`#6E99A1` / `#8FB6BD`。
- **实现**：画刷集中在 `IslandColors.Light.xaml` / `IslandColors.Dark.xaml`（键名 `Island.*`），`App.ApplyIslandColors` 订阅 `ApplicationThemeManager.Changed` 随主题热切换；岛窗口与模块岛视图只引用 `Island.*` 键，改配色只动这两个文件。
- 备选方案（墨岛/雾白/靛蓝/青瓷/豆绿/黛蓝/桃夭）保留在设计画布存档页，随时可回滚或混搭。

## 3.8 P2 连点器实施记录（2026-08-14 完成）

- **功能**：目标（鼠标左/右键 | 键盘任意键，面板内捕获设定）× 模式（连点：间隔 ms，下限 10 | 长按：按下不放/再按松开）。设置持久化于 `Tools.auto-clicker` 配置节。
- **模块会话机制（新增，通用）**：`IToolSession` 接口 + `ToolContext`（Config / ModuleHotkeys 服务定位）。用户确认的语义="隐身挂载"：进入面板=激活并注册通用热键（`AppConfig.ModuleHotkey`，默认 F6）；**岛隐藏不解除**（游戏中可用）；唤起岛直接回到会话模块面板；Esc 返回列表/切换模块/程序退出=解除（注销热键 + 停止 + 释放长按中的键）。热键平时完全不注册，不占用系统热键。
- **Core 新增**：`InputSimulator`（SendInput：鼠标当前位置按/放/点 + 键盘扫描码事件，扩展键处理）；`GlobalHotkeyService` 改为返回 id 并支持 `Unregister`。
- **引擎**：连点=后台线程 `Thread.Sleep` 循环（个人用足够，间隔下限 10ms 防炸）；长按=启动时按下、停止时释放；`RunningChanged` 事件驱动 UI 状态点。
- 实测（目标临时配为无害的 ScrollLock）：挂载 → 岛隐藏 → F6 开始/停止 → 再唤起直达面板 → Esc 解除 → F6 无效，全部通过。
- 遗留观察：日志曾出现一次瞬时 DWM「桌面合成已禁用」COMException（WPF WindowChrome 对 WM_DWMCOMPOSITIONCHANGED 的已知抖动，全屏应用/驱动重置可触发），已被 DispatcherUnhandledException 兜底，无需处理。

## 3.9 失焦隐藏修复（2026-08-14）

用户报告：唤起后点击外部不隐藏，需先点一下岛再点外面。原因：`AttachThreadInput` 抢来的前台，WPF 有时不认定为"已激活"，`Deactivated` 永不触发。

修复：Core 新增 `ForegroundWatcher`（`SetWinEventHook` 监听 `EVENT_SYSTEM_FOREGROUND`，系统级、不依赖自身激活状态）。两次迭代出的最终策略：

1. 事件异步送达可能滞后/陈旧 → 判定时取"当前" `GetForegroundWindow()`，不用事件参数；
2. 抢前台可能被系统拒绝并回弹 → **"本次显示期间曾拥有过前台，之后前台易主才隐藏"**；从未拥有则保持可见（用户点击岛获得前台后规则自然生效）。`OnActivated` 与唤起后的直接检查都会标记"已拥有"。

另：连点器自动化测试用 ScrollLock 当无害目标，奇数次按压翻转了锁定状态——测试后已复位；此类测试目标以后选 0x88-0x8F 的未定义 VK 更干净。

## 3.10 P3 翻译模块实施记录（2026-08-14）

- **服务商调研**：微软 Edge 免费通道（edge.microsoft.com/translate/auth）2026 年国内已失效（实测 404）；Bing 网页接口（cn.bing.com/ttranslatev3）能取到 IG/token 但响应体为空（反爬），弃用；**腾讯交互翻译 TranSmart（transmart.qq.com/api/imt）实测可用**——免费无 Key、国内直连、JSON 干净，选为默认。百度翻译开放平台（AppId+Secret+MD5 签名）作为 Key 备选，配置后自动切换。
- **结构**：`ITranslator` 抽象（TranSmart / Baidu 两实现）+ `TranslatorTool`（ITool + IToolSession）+ 岛视图（输入 → 译文 → 目标分段/复制/热键行）。
- **方向判定**：本地 CJK 字符启发式判源语言，Auto 模式译向另一侧；可强制 中/EN；源=目标时原样返回。输入停顿 600ms 或回车触发，进行中的请求用 CTS 取消。
- **划词**：会话热键 F6 → `ClipboardCapture`（备份剪贴板文本 → SendInput Ctrl+C → 以 GetClipboardSequenceNumber 轮询等待变化 ≤450ms → 读取 → 恢复备份）→ `ToolContext.SummonIsland` 唤岛直达面板 → 填入翻译。无选中（剪贴板无变化）则只唤岛。注意：仅恢复文本剪贴板，图片等会丢（v1 取舍）。
- **岛高度自适应**：展开态内容尺寸变化（译文出现/变长）由 IslandWindow 的 LayoutUpdated 重测并动画调高（_heightAnimating 防抖，动画期间跳过重测）。

## 3.11 v0.5：谷歌回退链 + 截屏翻译（2026-08-15）

- **服务商回退链**：用户在国外 → Auto 顺序 = 谷歌（translate.googleapis.com `client=gtx` 免费接口，用户机器实测通）→ 百度（配了 Key 时）→ 腾讯 TranSmart 兜底；单个失败记日志并降级，全败才报错。`Tools.translator.Provider` 可强制 Google/Baidu/TranSmart。谷歌超时设 6s 保证回退迅速。
- **截屏翻译**：面板「截屏」按钮 → 岛 Hide + 180ms 等待 → `SnipOverlay`（覆盖虚拟屏幕的拉框遮罩，Esc 取消，选区按 `VisualTreeHelper.GetDpi` 换算物理像素）→ `Graphics.CopyFromScreen` 截图 → `OcrService`（Windows.Media.Ocr；按内容自动选引擎：中文引擎无汉字则改英文/法文引擎，需系统装对应 OCR 语言包）→ **`SnipResultWindow` 就地出卡片**（贴选区下方/上方，译文 + 识别原文 + 复制；`RegionOutlineWindow` 给选区描天青边、点击穿透）。用户反馈"文字进输入框没法对照位置"后由"唤岛填入"改为就地卡片，岛保持隐藏。卡片内可切目标语言重译（分段/数字键 1-4）、复制原文/译文；排版按 DESIGN.md 行高规范（`SelectableText`）。F6 一键两用：有选中→划词，无选中→截屏。
- **TFM 变更**：App 与 Translator 升到 `net10.0-windows10.0.19041.0`（WinRT 投影；System.Drawing.Common 该 TFM 下自带无需包）。**输出目录随之变为 `bin\Release\net10.0-windows10.0.19041.0\`**，旧目录配置已迁移。
- 已知限制：混合 DPI 多屏下选区换算用主窗口 DPI，副屏缩放不同时可能偏移（v1 取舍）；OCR 质量依赖截图清晰度与系统语言包。

## 3.12 万能输入 + 主页 F6（2026-08-15）

- 用户质疑搜索框无用（仅过滤模块名）→ 升级为命令面板式"万能输入"：`ApplyFilter` 在模块名匹配行之后追加快捷行——URL（正则：协议/www/域名形）→「打开链接」（`Process.Start` UseShellExecute，无协议补 https）；算式（仅含数字与 `+-*/%()`，`DataTable.Compute` 求值）→「= 结果」回车复制；否则遍历实现 `IToolQuickInput` 的模块取行标签（翻译 →「翻译」，选中后 Expand 再 `HandleQuickInput`）。`IslandChip` 扩展为 Tool / Action / QuickInput 三态，`Activate()` 统一分派。
- **主页 F6**：`ClipboardCapture` 下沉至 Core；岛在无会话时注册 F6（`RegisterHomeHotkey`），`Expand` 激活会话前注销、`EndActiveSession` 后重新注册，避免与模块重复注册失败。主页 F6 = 取词填入搜索框（多行压成单行）；无选中仅唤岛。此举使 F6 常驻注册（用户决定，覆盖早期"平时不占用"原则）。
- 光标与占位文字重叠：占位 `Margin="4,0,0,0"`。

## 4. 备选方案说明

若你更想用 **Web 技术写 UI**（HTML/CSS/JS）且愿意接受 Rust：**Tauri 2** 是备选。体积小、UI 灵活，连点器可用 Rust 的 `rdev`/`InputBot` 或独立 sidecar 进程实现，但整体复杂度高于 C# 方案。

## 5. 调研来源

- [Tauri vs Electron 2026 对比 (Rustify)](https://rustify.rs/articles/rust-tauri-vs-electron-2026)
- [2026 桌面框架全景对比 (youngju.dev)](https://www.youngju.dev/blog/culture/2026-05-14-desktop-app-frameworks-2026-tauri-electron-wails-compose-multiplatform-maui-flutter-comparison-deep-dive-2026.en)
- [Top 5 Electron alternatives 2026 (TeamDev)](https://teamdev.com/mobrowser/blog/top-5-electron-alternatives-in-2026/)
- [uTools 插件化桌面效率工具介绍 (知乎)](https://zhuanlan.zhihu.com/p/1920959781415413232)
- [开源 uTools 平替 ZTools (知乎)](https://zhuanlan.zhihu.com/p/2025216183540924571)
- [插件架构设计模式入门 (Dev Leader)](https://www.devleader.ca/2023/09/07/plugin-architecture-design-pattern-a-beginners-guide-to-modularity)
- [用插件解耦软件的最佳实践 (ArjanCodes)](https://www.arjancodes.com/blog/best-practices-for-decoupling-software-using-plugins/)
- [globalmousekeyhook (C# 全局键鼠钩子库)](https://github.com/Shujee/globalmousekeyhook)
- [WindowsInput (C# SendInput 封装)](https://github.com/MediatedCommunications/WindowsInput)
- [InputBot (Rust 全局热键/模拟输入库)](https://github.com/obv-mikhail/InputBot)
- [FluAutoClicker (Rust+Tauri 连点器参考实现)](https://github.com/Agzes/FluAutoClicker)
