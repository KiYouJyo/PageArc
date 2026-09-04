# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

![Version](https://img.shields.io/badge/version-1.3.1-005fb8)
![Windows](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11)
![WinUI 3](https://img.shields.io/badge/WinUI-3-005FB8)
![Languages](https://img.shields.io/badge/UI-中文%20%7C%20日本語%20%7C%20English-6A5ACD)
![Offline First](https://img.shields.io/badge/offline-first-2E7D32)
![License](https://img.shields.io/badge/license-MIT-blue)

**PageArc** 是一个专注于流式电子书格式的 Windows 阅读器，采用 WinUI 3 / Windows App SDK 构建。界面以 PAGEARC Figma 为设计基准，优先保持本地、原文件不修改的阅读体验。

## v1.0

v1.0 在 v0.9.5 功能基础上完成阅读器、书库、设置、更新和 Windows 分发体验的正式版收敛：

- 阅读数据备份升级到 schema v2，并提供“合并 / 覆盖”恢复；换机后可按 PageArc ID、内容指纹或唯一书籍身份重新匹配阅读进度、书签和笔记；
- 官方 x64 包内置固定版本的 calibre 9.13.0 本地转换运行时，PageArc 默认直接提供 EPUB / FB2 / MOBI / AZW3 / LIT 的 20 个有向互转组合，用户无需另装转换软件；外部 calibre 仅保留为开发/兼容后备；
- EPUB / CJK 正文层增加严格中日文换行、Ruby/振假名、纵排 writing-mode、MathML/SVG 自适应和宽表格横向滚动兼容；该能力针对 reflow 文档，不宣称完整 Fixed-layout EPUB 支持；
- Home / Reader 标签顺序、身份和当前选中标签持久化，重启后自动恢复仍然有效的阅读会话；
- 同文档脚注链接可直接弹出轻量阅读层，并保留跳转到原脚注的入口；
- 点击正文图片可进入阅读器内图片查看器，支持缩放、拖动、适合窗口、100% 与安全保存；
- EPUB 2 / EPUB 3、FB2 使用内置解析器；MOBI / KF8 / AZW3 使用固定版本本地解析运行时；LIT 通过独立 Flow Adapter 与随包转换运行时接入统一阅读器；
- 书库继续支持 Grid/List、真实封面与元数据、批量导入、重复检测、监视文件夹、分类/收藏、缺失文件处理；Reader 保留目录 / 搜索 / 书签 / 笔记、阅读主题及多种流式视图模式；
- Windows 打包版本继续注册 EPUB / FB2 / MOBI / AZW / AZW3 / LIT 文件关联，并支持单实例、`pagearc:` 深链和 Jump List。

> **原文件安全**：PageArc 的阅读缓存、封面缓存、解析工作区和转换输出均使用副本或新文件，书库移除也不会删除原电子书。DRM 去除不属于项目范围。

## 获取版本

正式版本与签名安装包发布在 [GitHub Releases](https://github.com/KiYouJyo/PageArc/releases)。应用内“检查更新”同样以 GitHub Releases 为更新源。

## 设计基准

PageArc 的 UI SSOT 为 Figma `PAGEARC` 文件。新增或改变可见 UI 前必须先读取相应 Figma 节点；实现优先使用 WinUI 3 原生控件、Mica / Fluent 交互和系统图标。

## 隐私与联网

PageArc 不要求账户，书库、缓存、阅读进度、书签、笔记、标签会话与设置均保存在本机。正常阅读、解析和转换均在本地进行。正式安装包不会为了转换电子书而联网；网络仅用于用户主动执行的更新检查。构建官方安装包时下载的第三方转换运行时不属于应用运行期联网行为。

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

- [应用主页（GitHub Pages）](https://kiyoujyo.github.io/PageArc/)
- [公开隐私政策](https://kiyoujyo.github.io/PageArc/privacy/)
- [支持与问题反馈](https://kiyoujyo.github.io/PageArc/support/)
- [Microsoft Store 发布边界与检查清单](docs/STORE_PUBLISHING.md)

- [路线图](docs/ROADMAP.md)
- [v0.9.5 功能记录](docs/V095_FEATURES.md)
- [架构](docs/ARCHITECTURE.md)
- [流式引擎架构](docs/ENGINE_ARCHITECTURE.md)
- [Windows 集成](docs/WINDOWS_INTEGRATION.md)
- [格式支持](docs/FORMAT_SUPPORT.md)
- [v0.9.3 标签页与 Reader 设计记录](docs/TABBED_SHELL_0.9.3.md)
- [LIT 兼容性说明](docs/LIT_COMPATIBILITY.md)
- [数据与存储](docs/DATA_STORAGE.md)
- [第三方组件](THIRD_PARTY_NOTICES.md)
- [隐私政策](PRIVACY.md)
- [安全披露](SECURITY.md)
- [行为准则](CODE_OF_CONDUCT.md)
- [贡献指南](CONTRIBUTING.md)
- [更新日志](CHANGELOG.md)

## License

PageArc 主项目采用 MIT License。捆绑的第三方组件保留各自许可证与来源说明；官方 x64 包内的 calibre 转换运行时继续受 GPLv3 约束，详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
