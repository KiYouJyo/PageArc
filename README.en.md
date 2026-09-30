<p align="center">
  <img src="docs/assets/app-icon.png" width="128" height="128" alt="PageArc">
</p>
<h1 align="center">PageArc</h1>
<p align="center">A local-first Windows reader for reflowable ebooks.</p>
<p align="center">
  <a href="https://github.com/KiYouJyo/PageArc/releases/latest"><img src="https://img.shields.io/github/v/release/KiYouJyo/PageArc?display_name=tag&amp;sort=semver" alt="GitHub Release"></a>
  <a href="https://github.com/KiYouJyo/PageArc/actions/workflows/ci.yml"><img src="https://github.com/KiYouJyo/PageArc/actions/workflows/ci.yml/badge.svg?branch=main" alt="CI"></a>
  <a href="https://github.com/KiYouJyo/PageArc"><img src="https://img.shields.io/badge/Windows-WinUI%203-0078D4?logo=windows" alt="Windows"></a>
  <a href="https://github.com/KiYouJyo/PageArc"><img src="https://img.shields.io/badge/Languages-中文%20%7C%20日本語%20%7C%20English-6F42C1" alt="Languages"></a>
  <a href="https://github.com/KiYouJyo/PageArc"><img src="https://img.shields.io/badge/Design-Local--first-2EA043" alt="Local First"></a>
  <a href="https://github.com/KiYouJyo/PageArc/blob/main/LICENSE"><img src="https://img.shields.io/badge/License-MIT-D4A72C" alt="MIT License"></a>
  <a href="https://kiyoujyo.github.io/PageArc/"><img src="https://img.shields.io/badge/Website-PageArc-0078D4" alt="Website"></a>
</p>
<p align="center">
  <a href="https://get.microsoft.com/installer/download/9NTHMP210H9B?referrer=appbadge"><img src="https://get.microsoft.com/images/en-us%20dark.svg" width="240" alt="Get PageArc from Microsoft Store"></a>
</p>
<p align="center"><a href="README.md">简体中文</a> | <a href="README.ja.md">日本語</a> | <a href="README.en.md">English</a></p>

## v1.4

v1.4 removes the heavy ebook-conversion runtime from the base reader:

- calibre 9.13.0 is no longer embedded in the PageArc MSIX;
- the separate `KiYouJyo/PageArc.ConversionRuntime` repository publishes pinned runtime `9.13.0-pagearc.1`, downloaded only when conversion or a compatibility-dependent MOBI / AZW3 / LIT open is first requested;
- PageArc pins the release, archive size and SHA-256, installs the runtime per-user under `PageArc/Runtimes`, and reuses an existing system calibre installation when available.

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

Install from [Microsoft Store](https://apps.microsoft.com/detail/9NTHMP210H9B) for Store-managed installation and updates. Signed sideload packages remain available through [GitHub Releases](https://github.com/KiYouJyo/PageArc/releases). The channels use separate package identities and update sources.

## Design source of truth

Visible UI changes must be checked against the PAGEARC Figma design before XAML is changed. PageArc prefers native WinUI 3 controls, Mica / Fluent behavior and Windows system icons while preserving the approved Figma hierarchy and density.

## Privacy

No account is required. Library metadata, settings, progress, bookmarks, notes and tab-session state stay on the device. Normal reading, parsing and ebook conversion run locally. The base installer does not contain calibre. When a conversion-dependent feature is first requested, PageArc asks before downloading the pinned PageArc.ConversionRuntime release; subsequent conversion runs locally. Update checks and user-initiated WebDAV synchronization also use the network when requested.

## Build

```powershell
dotnet restore PageArc.slnx
dotnet build PageArc.slnx -c Debug -p:Platform=x64
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Debug -p:Platform=x64
```

See the [application homepage](https://kiyoujyo.github.io/PageArc/), [public privacy policy](https://kiyoujyo.github.io/PageArc/privacy/), [support page](https://kiyoujyo.github.io/PageArc/support/), and [Microsoft Store publishing checklist](docs/STORE_PUBLISHING.md). Technical notes: [docs/ROADMAP.md](docs/ROADMAP.md), [docs/V095_FEATURES.md](docs/V095_FEATURES.md), [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/ENGINE_ARCHITECTURE.md](docs/ENGINE_ARCHITECTURE.md), [docs/WINDOWS_INTEGRATION.md](docs/WINDOWS_INTEGRATION.md), [docs/FORMAT_SUPPORT.md](docs/FORMAT_SUPPORT.md), [docs/TABBED_SHELL_0.9.3.md](docs/TABBED_SHELL_0.9.3.md), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), [PRIVACY.md](PRIVACY.md), [SECURITY.md](SECURITY.md), [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md), [CONTRIBUTING.md](CONTRIBUTING.md), and [CHANGELOG.md](CHANGELOG.md).

## License

PageArc itself is MIT-licensed. The base package no longer bundles calibre; the optional PageArc.ConversionRuntime remains GPLv3-licensed where applicable and publishes the matching calibre source beside each runtime release. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
