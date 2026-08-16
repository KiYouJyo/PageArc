# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

![Version](https://img.shields.io/badge/version-0.4.0-005fb8)
![Windows](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11)
![WinUI 3](https://img.shields.io/badge/WinUI-3-005FB8)
![Languages](https://img.shields.io/badge/UI-中文%20%7C%20日本語%20%7C%20English-6A5ACD)
![Offline First](https://img.shields.io/badge/offline-first-2E7D32)
![License](https://img.shields.io/badge/license-MIT-blue)

**PageArc** 是一个专注于流式电子书格式的 Windows 阅读器，采用 WinUI 3 构建。设计严格参考项目 Figma，并优先保持本地、轻量、原文件不修改的阅读体验。

## v0.4.0

v0.4.0 完成第一阶段格式引擎收束：

- EPUB 2 / EPUB 3 与 FB2 使用内置解析器；
- MOBI 与 KF8 / AZW3 使用固定版本、本地打包的解析运行时，不依赖 CDN；
- LIT 通过独立 `LitFlowAdapter` 接入统一 `FlowReaderEngine`，在本机存在兼容转换 Provider 时使用只读 EPUB 归一化缓存；
- 阅读器统一支持目录、连续滚动 / 分页、章节内进度、全文搜索、书签、标注数据以及 Figma 对齐的搜索 / 书签 / 笔记侧栏；
- MOBI / AZW3 在解析前检查 PalmDOC 加密标志，确认 DRM 后立即停止，不尝试绕过；
- 转换层显式建模 EPUB / FB2 / MOBI / AZW3 / LIT 之间的 20 个有向互转组合，并根据本机实际 Provider 报告可用能力；
- 若本机安装 calibre，可将 `ebook-convert` 作为可选的本地转换与兼容性 Provider。PageArc 不捆绑 calibre。

> **原文件安全**：PageArc 的阅读缓存、Kindle 解析工作区和转换输出均使用副本或新文件，绝不修改原电子书。DRM 去除不属于项目范围。

## 下一阶段

- **v0.5**：完善书库、批量导入、元数据与封面、搜索/排序/筛选、合集、详情与大书库性能。
- **v0.6**：Windows 深度集成，包括文件关联、激活/单实例、资源管理器联动、跳转列表与系统级打开体验。

## 设计基准

PageArc 的 UI SSOT 为 Figma `PAGEARC` 文件。新增或改变可见 UI 前必须先读取相应 Figma 节点；实现优先使用 WinUI 3 原生控件、Mica / Fluent 交互和系统图标。

## 隐私与联网

PageArc 不要求账户，书库、缓存、阅读进度、书签、标注与设置均保存在本机。正常阅读和内置解析不需要联网。

网络仅用于用户主动执行的更新检查；若用户选择安装并配置外部转换 Provider，其调用仍在本机完成。

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
- [流式引擎架构](docs/ENGINE_ARCHITECTURE.md)
- [格式支持](docs/FORMAT_SUPPORT.md)
- [LIT 兼容性说明](docs/LIT_COMPATIBILITY.md)
- [数据与存储](docs/DATA_STORAGE.md)
- [第三方组件](THIRD_PARTY_NOTICES.md)
- [隐私政策](PRIVACY.md)
- [贡献指南](CONTRIBUTING.md)
- [更新日志](CHANGELOG.md)

## License

PageArc 主项目采用 MIT License。捆绑的第三方解析组件保留各自的许可证与来源说明，详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
