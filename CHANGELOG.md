# Changelog

## [0.3.0] - 2026-08-16

### Added
- Built-in DRM-free MOBI and KF8/AZW3 reading behind the same `FlowReaderEngine` used by EPUB and FB2.
- Pinned local Kindle parsing runtime based on the MIT-licensed foliate-js MOBI parser and fflate zlib runtime; no runtime CDN is used.
- Isolated invisible parser WebView2 that performs lazy Kindle section/resource parsing while the visible Reader WebView remains dedicated to PageArc's safe reflow rendering surface.
- Kindle metadata, author, language, cover and table-of-contents projection into the common `FlowDocument` model.
- Native Palm database / MOBI signature and PalmDOC encryption probing before parser execution.
- Ordered same-format adapter fallback: the built-in Kindle parser is preferred, while the optional calibre normalization path can handle compatible edge cases if installed.
- Dedicated third-party license notices and exact upstream pin/blob provenance for the vendored parser runtime.

### Changed
- Confirmed DRM/encryption now raises a dedicated open failure and stops provider fallback; PageArc never attempts DRM removal.
- Kindle blob resources and styles are materialized into self-contained, sanitized section HTML before reaching the visible reader.
- The packaged application version is now 0.3.0.

### Notes
- EPUB, FB2, MOBI and AZW3 now have built-in reading adapters.
- The calibre bridge remains optional for format conversion and compatibility fallback; it is not bundled with PageArc.
- LIT continues to use the optional normalization fallback in this release and is the main built-in format target for v0.4.0.

## [0.2.0] - 2026-08-16

### Added
- Unified flow document engine with format-neutral sections, TOC entries and stable section-relative reading locations.
- EPUB adapter migrated onto the unified engine without losing EPUB 2 / EPUB 3 compatibility.
- Native FB2 adapter with metadata, table of contents, semantic reflow content and embedded image support.
- WebView2 reflow host supporting continuous reading and paginated section navigation while preserving the Figma-approved PageArc reader shell.
- Full-text search across flow sections with in-document result highlighting.
- Persistent bookmarks and annotation storage plus Figma-aligned Search, Bookmarks and Notes side panes.
- Real conversion queue backed by pluggable conversion providers.
- calibre `ebook-convert` provider for DRM-free conversion across EPUB, FB2, MOBI, AZW3 and LIT when calibre is installed or configured.
- Normalized flow fallback that can open MOBI, AZW3 and LIT by converting a cached copy to EPUB when the calibre provider is available.
- Dedicated normalized-book cache; original ebook files are never modified.

### Changed
- Reading progress now stores both the flow section and the within-section fraction so font/line-spacing/page-width changes preserve the reading location more accurately.
- EPUB rendering now strips active script content and blocks external WebView resource requests and pop-up navigation.
- The About page reads the application version at runtime instead of embedding a stale version number in each localization resource.

### Notes
- EPUB and FB2 have built-in reading adapters in v0.2.0.
- MOBI, AZW3 and LIT have a working optional normalization path when calibre is installed; v0.3/v0.4 will progressively replace reading-time normalization with built-in format adapters.
- DRM removal is intentionally out of scope.

## [0.1.0] - 2026-08-16

### Added
- First public PageArc release built with WinUI 3 / Windows App SDK.
- Figma-aligned Library, Categories, Reader, Format Conversion, Import Folders, Settings and About experiences.
- Adaptive NavigationView with native Windows Fluent icons and activation-aware cyan / neutral-gray navigation surfaces.
- Simplified Chinese, Japanese and English UI resources with Follow system and in-place language switching.
- Local library, categories, favorites, reading progress and settings persistence.
- EPUB 2 / EPUB 3 metadata, OPF, spine, nav and NCX parsing with safe extraction/cache handling.
- Native WinUI EPUB text reading with TOC, chapter navigation, progress, font-size, line-spacing and reading-theme controls.
- User-invoked GitHub Release update checking.
- CI, regression tests, signed x64 MSIX validation, privacy policy, contribution guide, architecture and roadmap documents.

### Notes
- EPUB is the supported reading format in v0.1.0.
- FB2 / MOBI / AZW3 / LIT can be cataloged but do not yet have stable reading adapters.
- The Format Conversion page is present, but conversion engines are planned for later versions.
