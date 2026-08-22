# 百景工具箱 HyakkeiTools

Windows 个人功能中心：双击 Ctrl 唤起一个极简命令面板，把连点器、翻译、截屏 OCR 等零散小工具收进一个入口。

- **唤起即用**：双击 Ctrl 弹出面板，打字即搜——模块名、链接、算式、待翻译文字都能直接回车
- **模块化**：每个功能是独立模块，管理中心一键开关；新增模块只需实现 `ITool`
- **一个动作键 F6**：进哪个模块就归哪个模块（连点启停 / 划词翻译 / 截屏翻译），退出即释放
- **轻量**：常驻内存约 18MB，无 Electron，仅 WPF + 两个依赖
- **清新国风「天青」配色**，跟随系统深浅色

## 模块

| 模块 | 功能 |
|---|---|
| 连点器 | 鼠标左/右键、键盘任意键 × 连点 / 长按，频率预设 |
| 翻译 | 输入即翻、划词翻译、截屏 OCR 翻译；中 / 英 / 法；谷歌 → 腾讯回退链，可配百度 Key |

## 使用

下载 [Releases](../../releases) 或自行构建后运行 `HyakkeiTools.exe`，程序静默进托盘。所有操作方式见 [docs/usage.md](docs/usage.md)。

需要 .NET 10 运行时（Windows 10 19041+ / Windows 11）。截屏翻译的识别精度取决于系统已安装的 OCR 语言包（设置 → 语言 → 勾选「光学字符识别」）。

## 构建

```bash
dotnet build HyakkeiTools.slnx -c Release
scripts\publish.ps1      # 单文件发布到 dist\
```

## 文档

- [docs/usage.md](docs/usage.md) — 使用手册
- [docs/architecture.md](docs/architecture.md) — 架构与实施记录
- [docs/design/DESIGN.md](docs/design/DESIGN.md) — 界面设计规范
