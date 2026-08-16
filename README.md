# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

![Version](https://img.shields.io/badge/version-0.1.0-005fb8)
![Windows](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11)
![WinUI 3](https://img.shields.io/badge/WinUI-3-005FB8)
![Languages](https://img.shields.io/badge/UI-中文%20%7C%20日本語%20%7C%20English-6A5ACD)
![Offline First](https://img.shields.io/badge/offline-first-2E7D32)
![License](https://img.shields.io/badge/license-MIT-blue)

**PageArc** 是一个专注于流式电子书格式的 Windows 阅读器，采用 WinUI 3 构建。设计严格参考项目 Figma，并优先保持本地、轻量、原文件不修改的阅读体验。

## v0.1.0

PageArc v0.1.0 是首个公开版本，当前包含：

- 与 PAGEARC Figma 设计对齐的 WinUI 3 / Windows App SDK 应用壳、书库、分类、阅读器、格式转换、导入文件夹、设置与关于页面；
- 自适应 NavigationView、小尺寸覆盖式导航、Windows 原生 Fluent 图标，以及随窗口激活状态变化的浅青 / 深青与中性灰导航配色；
- 简体中文、日本語、English 三语资源、跟随系统选项与应用内原地语言切换；
- 本地书库索引、分类、收藏筛选、阅读进度与阅读偏好持久化；
- EPUB 2 / EPUB 3 的 metadata、OPF、spine、nav / NCX 目录解析与安全缓存；
- 原生 WinUI EPUB 正文阅读、目录跳转、前后章节、阅读进度、字号、行距和阅读主题；
- About 页 GitHub Release 更新检查；
- CI、自动测试、签名 MSIX 验收、隐私政策、贡献指南、架构与路线图文档。

> **格式状态**：v0.1.0 的稳定阅读路径为 **EPUB**。书库可识别 FB2 / MOBI / AZW3 / LIT，但这些格式的阅读适配器仍在后续版本计划中。格式转换页已建立交互与任务界面，实际转换引擎尚未在 v0.1.0 提供。

## 设计基准

PageArc 的 UI SSOT 为 Figma `PAGEARC` 文件。实现时优先使用 WinUI 3 原生控件、Mica / Fluent 交互和系统图标，在控件行为与像素级布局冲突时优先保留 Windows 原生交互语义，同时匹配 Figma 的尺寸、密度与层级。

## 隐私与联网

PageArc 不要求账户，书库、缓存、阅读进度与设置均保存在本机。原始电子书文件不会被修改。

默认阅读流程不需要联网。v0.1.0 仅在用户主动点击“检查更新”时访问 GitHub Release API。

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
