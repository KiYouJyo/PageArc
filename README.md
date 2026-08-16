# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

![Version](https://img.shields.io/badge/version-0.1.0--preview-005fb8)
![Windows](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11)
![WinUI 3](https://img.shields.io/badge/WinUI-3-005FB8)
![Languages](https://img.shields.io/badge/UI-中文%20%7C%20日本語%20%7C%20English-6A5ACD)
![Offline First](https://img.shields.io/badge/offline-first-2E7D32)
![License](https://img.shields.io/badge/license-MIT-blue)

**PageArc** 是一个专注于流式电子书格式的 Windows 阅读器，采用 WinUI 3 构建。设计严格参考项目 Figma，并优先保持本地、轻量、原文件不修改的阅读体验。

## v0.1.0 当前范围

当前开发分支建立了 v0.1.0 的可运行基础：

- WinUI 3 / Windows App SDK 应用壳与 Figma 对齐的书库、阅读、设置、关于页面；
- 可收放 NavigationView，使用 Windows 原生 `SymbolIcon`，不再使用文本字符模拟图标；
- 简体中文、日本語、English 三语资源与应用内语言切换；
- 本地书库索引和阅读偏好持久化；
- EPUB 元数据 / OPF / spine / EPUB 3 nav 基础解析；
- WebView2 本地 EPUB 正文渲染、章节前后跳转、主题、字号、行距；
- About 页 GitHub Release 更新检查；
- CI、测试、隐私、贡献、路线图等仓库基础文件。

> **格式状态**：产品目标覆盖 EPUB / FB2 / MOBI / AZW3 / LIT；v0.1.0 当前阅读核心优先打通 EPUB。其余格式适配器将在后续里程碑接入。在完整实现前，README 不宣称它们已可稳定阅读。

## 设计基准

PageArc 的 UI SSOT 为 Figma `PAGEARC` 文件。实现时优先使用 WinUI 3 原生控件、Mica/Fluent 交互和 `SymbolIcon`，在控件行为与像素级布局冲突时，优先保留 Windows 原生交互语义，同时匹配 Figma 的尺寸、密度与层级。

## 隐私与联网

PageArc 不要求账户，书库、缓存、阅读进度与设置均保存在本机。原始电子书文件不会被修改。

默认阅读流程不需要联网。当前 v0.1.0 只有用户主动点击“检查更新”时会访问 GitHub Release API。

## 开发环境

- Windows 10 19041+ / Windows 11
- .NET 10
- Windows App SDK 2.3.1
- Visual Studio 2026（含 Windows App SDK / Windows 应用开发组件）

```powershell
dotnet restore PageArc.slnx
dotnet build PageArc.slnx -c Debug -p:Platform=x64
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Debug -p:Platform=x64
```

## 文档

- [路线图](docs/ROADMAP.md)
- [架构](docs/ARCHITECTURE.md)
- [数据与存储](docs/DATA_STORAGE.md)
- [隐私政策](PRIVACY.md)
- [贡献指南](CONTRIBUTING.md)
- [更新日志](CHANGELOG.md)

## License

MIT. 详见 [LICENSE](LICENSE)。
