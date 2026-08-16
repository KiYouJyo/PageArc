# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

**PageArc** is a WinUI 3 ebook reader for Windows focused on reflowable formats. The UI follows the PAGEARC Figma source of truth while keeping reading local-first and leaving original ebook files untouched.

## v0.6.0

PageArc now has a complete first-stage format engine, library, and Windows integration foundation:

- built-in EPUB 2 / EPUB 3 and FB2 parsing; local pinned MOBI / KF8 / AZW3 parsing; dedicated LIT flow integration through a local conversion-provider boundary;
- one reader contract for TOC navigation, continuous/paginated reading, section-relative progress, full-text search, bookmarks and annotations;
- a completed library workflow with real covers/metadata, batch import, duplicate detection, watched folders, categories/favorites, details, missing-file handling and large-library migration coverage;
- packaged Windows file associations for EPUB / FB2 / MOBI / AZW / AZW3 / LIT so ebooks can open from Explorer;
- Windows App SDK single-instance redirection for new file/protocol activations;
- `pagearc:` deep links and Windows Jump List recent-book entries;
- an explicit 20-pair ordered conversion capability matrix for EPUB / FB2 / MOBI / AZW3 / LIT, gated by providers actually available on the machine.

**Source safety:** reading caches, cover caches, parser workspaces and conversions use copies or new files. Removing a book from PageArc never deletes the original ebook. DRM removal is out of scope.

## Design source of truth

Visible UI changes must be checked against the PAGEARC Figma design before XAML is changed. PageArc prefers native WinUI 3 controls, Mica / Fluent behavior and Windows system icons while preserving the approved Figma hierarchy and density.

## Privacy

No account is required. Library metadata, settings, progress, bookmarks and annotations stay on the device. Normal reading and built-in parsing are offline. Network access is limited to user-invoked update checks; optional external conversion providers are invoked locally.

## Build

```powershell
dotnet restore PageArc.slnx
dotnet build PageArc.slnx -c Debug -p:Platform=x64
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Debug -p:Platform=x64
```

See [docs/ROADMAP.md](docs/ROADMAP.md), [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/ENGINE_ARCHITECTURE.md](docs/ENGINE_ARCHITECTURE.md), [docs/WINDOWS_INTEGRATION.md](docs/WINDOWS_INTEGRATION.md), [docs/FORMAT_SUPPORT.md](docs/FORMAT_SUPPORT.md), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), [PRIVACY.md](PRIVACY.md), and [CONTRIBUTING.md](CONTRIBUTING.md).

## License

PageArc itself is MIT-licensed. Vendored parser components retain their own licenses and provenance; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
