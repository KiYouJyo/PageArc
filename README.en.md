# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

![Version](https://img.shields.io/badge/version-1.3.1-005fb8)
![Windows](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11)
![WinUI 3](https://img.shields.io/badge/WinUI-3-005FB8)
![Languages](https://img.shields.io/badge/UI-中文%20%7C%20日本語%20%7C%20English-6A5ACD)
![Offline First](https://img.shields.io/badge/offline-first-2E7D32)
![License](https://img.shields.io/badge/license-MIT-blue)

**PageArc** is a WinUI 3 / Windows App SDK ebook reader for Windows focused on reflowable formats. The UI follows the PAGEARC Figma source of truth while keeping reading local-first and leaving original ebook files untouched.

## v1.0

v1.0 completes the production convergence of the reader, library, settings, updater and Windows distribution experience on top of v0.9.5:

- reading backups move to schema v2 and can now be restored in Merge or Replace mode; PageArc remaps progress, bookmarks and notes after a device/path change using exact IDs, content fingerprints and unique book identity;
- official x64 packages bundle a pinned local calibre 9.13.0 conversion runtime, making all 20 directed EPUB / FB2 / MOBI / AZW3 / LIT conversion pairs available without a separate calibre installation; external calibre remains a development/compatibility fallback;
- the reflow document layer adds strict Chinese/Japanese line breaking, ruby support, vertical writing-mode preservation, responsive MathML/SVG and horizontal overflow for wide tables; this does not claim a complete fixed-layout EPUB engine;
- Home/Reader tab order, identity and selected tab persist and valid Reader sessions are restored after restart;
- same-document note references open in a lightweight reading-surface footnote popover with an explicit jump action;
- document images open in an in-reader viewer with zoom, pan, fit, 100% and safe Save through the Windows picker;
- EPUB 2/3 and FB2 retain built-in parsing, MOBI/KF8/AZW3 retain the pinned local parser path, and LIT uses the dedicated flow adapter plus the bundled local conversion runtime;
- the completed library, Contents/Search/Bookmarks/Notes panes, persisted reader settings/view modes, Windows file associations, single-instance activation, `pagearc:` links and Jump List integration remain in place.

**Source safety:** reading caches, cover caches, parser workspaces and conversions use copies or new files. Removing a book from PageArc never deletes the original ebook. DRM removal is out of scope.

## Releases

Official builds and signed installation packages are published on [GitHub Releases](https://github.com/KiYouJyo/PageArc/releases). The in-app update checker uses GitHub Releases as its update source as well.

## Design source of truth

Visible UI changes must be checked against the PAGEARC Figma design before XAML is changed. PageArc prefers native WinUI 3 controls, Mica / Fluent behavior and Windows system icons while preserving the approved Figma hierarchy and density.

## Privacy

No account is required. Library metadata, settings, progress, bookmarks, notes and tab-session state stay on the device. Normal reading, parsing and ebook conversion run locally. Official installed builds do not need network access to convert books; network access is limited to user-invoked update checks. The packaging workflow downloads the pinned third-party runtime before the installer is produced.

## Build

```powershell
dotnet restore PageArc.slnx
dotnet build PageArc.slnx -c Debug -p:Platform=x64
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Debug -p:Platform=x64
```

See the [application homepage](https://kiyoujyo.github.io/PageArc/), [public privacy policy](https://kiyoujyo.github.io/PageArc/privacy/), [support page](https://kiyoujyo.github.io/PageArc/support/), and [Microsoft Store publishing checklist](docs/STORE_PUBLISHING.md). Technical notes: [docs/ROADMAP.md](docs/ROADMAP.md), [docs/V095_FEATURES.md](docs/V095_FEATURES.md), [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/ENGINE_ARCHITECTURE.md](docs/ENGINE_ARCHITECTURE.md), [docs/WINDOWS_INTEGRATION.md](docs/WINDOWS_INTEGRATION.md), [docs/FORMAT_SUPPORT.md](docs/FORMAT_SUPPORT.md), [docs/TABBED_SHELL_0.9.3.md](docs/TABBED_SHELL_0.9.3.md), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), [PRIVACY.md](PRIVACY.md), [SECURITY.md](SECURITY.md), [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md), [CONTRIBUTING.md](CONTRIBUTING.md), and [CHANGELOG.md](CHANGELOG.md).

## License

PageArc itself is MIT-licensed. Bundled third-party components retain their own licenses and provenance; the calibre runtime in official x64 packages remains GPLv3-licensed. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
