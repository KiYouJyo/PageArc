# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

![Version](https://img.shields.io/badge/version-0.6.0-005fb8)
![Windows](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11)
![WinUI 3](https://img.shields.io/badge/WinUI-3-005FB8)
![Languages](https://img.shields.io/badge/UI-中文%20%7C%20日本語%20%7C%20English-6A5ACD)
![Offline First](https://img.shields.io/badge/offline-first-2E7D32)
![License](https://img.shields.io/badge/license-MIT-blue)

**PageArc** 是一个专注于流式电子书格式的 Windows 阅读器，采用 WinUI 3 构建。设计严格参考项目 Figma，并优先保持本地、轻量、原文件不修改的阅读体验。

## v0.6.0

PageArc 目前已经完成格式引擎、书库和 Windows 集成三个基础阶段：

- EPUB 2 / EPUB 3、FB2 使用内置解析器；MOBI / KF8 / AZW3 使用固定版本的本地解析运行时；LIT 通过独立 Flow Adapter 与本地转换 Provider 接入统一阅读器；
- 阅读器统一支持目录、连续滚动 / 分页、章节内进度、全文搜索、书签、标注与笔记；
- 书库支持真实封面与元数据、批量导入、重复检测、监视文件夹、分类/收藏、详情侧栏、缺失文件处理以及大书库迁移；
- Windows 打包版本注册 EPUB / FB2 / MOBI / AZW / AZW3 / LIT 文件关联，可从资源管理器直接打开电子书；
- Windows App SDK 单实例机制会把新的文件/协议激活重定向到现有 PageArc 窗口；
- `pagearc:` 深链与 Windows Jump List 可重新打开书库中的最近书籍；
- 格式转换层显式建模 EPUB / FB2 / MOBI / AZW3 / LIT 之间的 20 个有向互转组合，并根据本机实际 Provider 报告可用能力。

> **原文件安全**：PageArc 的阅读缓存、封面缓存、解析工作区和转换输出均使用副本或新文件，书库移除也不会删除原电子书。DRM 去除不属于项目范围。

## 设计基准

PageArc 的 UI SSOT 为 Figma `PAGEARC` 文件。新增或改变可见 UI 前必须先读取相应 Figma 节点；实现优先使用 WinUI 3 原生控件、Mica / Fluent 交互和系统图标。

## 隐私与联网

PageArc 不要求账户，书库、缓存、阅读进度、书签、标注与设置均保存在本机。正常阅读和内置解析不需要联网。网络仅用于用户主动执行的更新检查；若用户选择安装并配置外部转换 Provider，其调用仍在本机完成。

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
- [Windows 集成](docs/WINDOWS_INTEGRATION.md)
- [格式支持](docs/FORMAT_SUPPORT.md)
- [LIT 兼容性说明](docs/LIT_COMPATIBILITY.md)
- [数据与存储](docs/DATA_STORAGE.md)
- [第三方组件](THIRD_PARTY_NOTICES.md)
- [隐私政策](PRIVACY.md)
- [贡献指南](CONTRIBUTING.md)
- [更新日志](CHANGELOG.md)

## License

PageArc 主项目采用 MIT License。捆绑的第三方解析组件保留各自的许可证与来源说明，详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
