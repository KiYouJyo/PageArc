# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

![Version](https://img.shields.io/badge/version-0.9.3-005fb8)
![Windows](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11)
![WinUI 3](https://img.shields.io/badge/WinUI-3-005FB8)
![Languages](https://img.shields.io/badge/UI-中文%20%7C%20日本語%20%7C%20English-6A5ACD)
![Offline First](https://img.shields.io/badge/offline-first-2E7D32)
![License](https://img.shields.io/badge/license-MIT-blue)

**PageArc** is a WinUI 3 / Windows App SDK ebook reader for Windows focused on reflowable formats. The UI follows the PAGEARC Figma source of truth while keeping reading local-first and leaving original ebook files untouched.

## v0.9.3

v0.9.3 completes the current desktop reading-experience convergence on top of the format engine, library, and Windows integration foundations:

- built-in EPUB 2 / EPUB 3 and FB2 parsing; pinned local MOBI / KF8 / AZW3 parsing; dedicated LIT flow integration through a local conversion-provider boundary;
- a multi-tab title-bar shell where startup and `+` create Home/library tabs and multiple books can remain open in independent Reader tabs;
- a completed library workflow with Grid/List switching, real covers and metadata, batch import, duplicate detection, watched folders, categories/favorites, details, missing-file handling and large-library migration coverage;
- one Reader sidebar for Contents / Search / Bookmarks / Notes plus an `Aa` pane with vertical, horizontal and wrapped reading, single/odd/even spread modes, zoom controls, automatic sizing, fit-width and fit-height;
- Reader chrome, side panes and surrounding reading area reveal the same Mica backdrop as the custom title bar while the document page itself follows the selected reading theme;
- text selection currently uses a note-first flow: typing autosaves, dismissal flushes pending text, and note-bearing text uses a muted low-saturation red mark;
- reading progress, search, bookmarks, notes, reading settings and view modes persist across sessions;
- packaged Windows file associations for EPUB / FB2 / MOBI / AZW / AZW3 / LIT, plus single-instance activation, `pagearc:` deep links and Jump List recent-book entries;
- an explicit 20-pair ordered conversion capability matrix for EPUB / FB2 / MOBI / AZW3 / LIT, gated by providers actually available on the machine.

**Source safety:** reading caches, cover caches, parser workspaces and conversions use copies or new files. Removing a book from PageArc never deletes the original ebook. DRM removal is out of scope.

## Releases

Official builds and signed installation packages are published on [GitHub Releases](https://github.com/KiYouJyo/PageArc/releases). The in-app update checker uses GitHub Releases as its update source as well.

## Design source of truth

Visible UI changes must be checked against the PAGEARC Figma design before XAML is changed. PageArc prefers native WinUI 3 controls, Mica / Fluent behavior and Windows system icons while preserving the approved Figma hierarchy and density.

## Privacy

No account is required. Library metadata, settings, progress, bookmarks and notes stay on the device. Normal reading and built-in parsing are offline. Network access is limited to user-invoked update checks; optional external conversion providers are invoked locally.

## Build

```powershell
dotnet restore PageArc.slnx
dotnet build PageArc.slnx -c Debug -p:Platform=x64
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Debug -p:Platform=x64
```

See [docs/ROADMAP.md](docs/ROADMAP.md), [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/ENGINE_ARCHITECTURE.md](docs/ENGINE_ARCHITECTURE.md), [docs/WINDOWS_INTEGRATION.md](docs/WINDOWS_INTEGRATION.md), [docs/FORMAT_SUPPORT.md](docs/FORMAT_SUPPORT.md), [docs/TABBED_SHELL_0.9.3.md](docs/TABBED_SHELL_0.9.3.md), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), [PRIVACY.md](PRIVACY.md), [CONTRIBUTING.md](CONTRIBUTING.md), and [CHANGELOG.md](CHANGELOG.md).

## License

PageArc itself is MIT-licensed. Vendored parser components retain their own licenses and provenance; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).