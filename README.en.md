# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

**PageArc** is a WinUI 3 ebook reader for Windows focused on reflowable formats. The UI follows the PAGEARC Figma source of truth while keeping reading local-first and leaving original ebook files untouched.

## v0.4.0

v0.4.0 closes the first format-engine milestone:

- built-in EPUB 2 / EPUB 3 and FB2 adapters;
- built-in local MOBI and KF8/AZW3 parsing based on a pinned packaged runtime with no CDN dependency;
- a dedicated `LitFlowAdapter` that joins the common `FlowReaderEngine` and uses a read-only EPUB normalization cache when a compatible local conversion provider is available;
- unified TOC navigation, continuous/paginated reading, section-relative progress, full-text search, bookmarks, annotation data, and Figma-aligned Search / Bookmarks / Notes panes;
- native PalmDOC encryption probing before MOBI/AZW3 parsing; confirmed DRM stops immediately and is never bypassed;
- an explicit 20-pair ordered conversion capability matrix for EPUB / FB2 / MOBI / AZW3 / LIT, gated by providers actually available on the machine;
- optional local interoperability with calibre `ebook-convert` when calibre is already installed. calibre is not bundled with PageArc.

**Source safety:** reading caches, Kindle parser workspaces and conversions always use copies or new output files. PageArc never modifies the original ebook and does not attempt DRM removal.

## Next

- **v0.5:** library completion — batch import, richer metadata and covers, search/sort/filtering, collections, details, and large-library performance.
- **v0.6:** deeper Windows integration — file associations, activation/single-instance behavior, Explorer integration, jump lists, and native open workflows.

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

See [docs/ROADMAP.md](docs/ROADMAP.md), [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/ENGINE_ARCHITECTURE.md](docs/ENGINE_ARCHITECTURE.md), [docs/FORMAT_SUPPORT.md](docs/FORMAT_SUPPORT.md), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), [PRIVACY.md](PRIVACY.md), and [CONTRIBUTING.md](CONTRIBUTING.md).

## License

PageArc itself is MIT-licensed. Vendored parser components retain their own licenses and provenance; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
