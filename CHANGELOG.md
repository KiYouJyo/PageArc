# Changelog

## [0.5.0] - 2026-08-16

### Added
- Figma-derived 410 px book-details side panel with reading progress, file information, favorites and bookmark/highlight/note counts.
- Figma-derived library book context menu with open, continue, favorite, category, details, file-location and safe library-removal actions.
- Figma three-stage import experience: chooser/drop zone, per-file progress/cancellation, and added/skipped/error completion summary.
- Persistent watched-folder management with recursive supported-format scans, repeat-safe rescans, availability state and per-folder counts.
- SHA-256 content fingerprints for duplicate detection across different source paths.
- Structured single/batch import results and progress contracts.
- Rich EPUB/FB2 metadata extraction including language, publisher, description and embedded cover cache.
- Kindle cover data-URL caching and persistence when MOBI/AZW3 parsing runs; normalized LIT/Kindle fallback metadata and cover persistence when those paths run.
- Missing-source tracking without silently dropping library records.
- Persistent library filter/sort/view preferences.
- Large-library regression coverage with 2,000 legacy records.

### Changed
- Library opening is format-neutral and routes supported formats through the shared reader instead of applying an EPUB-only gate.
- Search now considers title, author, format, publisher and category.
- Library persistence writes atomically and migrates older records into the richer v0.5 book schema.
- Removing a book from PageArc never deletes the original ebook file.
- The packaged application version is now 0.5.0.

### Notes
- The PAGEARC Figma nodes for Library, book context menu, book details, import chooser/progress/completion, Categories and Import Folders were inspected before visible UI changes.
- Normal local library operation remains offline-first.

## [0.4.0] - 2026-08-16

### Added
- Dedicated LIT flow adapter behind the common `FlowReaderEngine` contract.
- Read-only LIT-to-EPUB normalization cache through an available local conversion provider, with source size/mtime invalidation.
- Explicit conversion capability model covering all 20 ordered cross-format pairs among EPUB / FB2 / MOBI / AZW3 / LIT.
- Capability reporting that reflects providers actually available on the machine.
- Regression coverage for LIT normalization, DRM stop behavior, conversion matrix completeness, and five-format flow compatibility.
- Signed x64 acceptance pipeline covering Release tests, Debug/Release builds, MSIX signing, install and launch smoke testing.

### Changed
- LIT is no longer treated as an incidental Kindle compatibility case; it has its own adapter boundary.
- Confirmed DRM signals from the LIT provider stop the open path and are never routed into bypass attempts.
- The packaged application version is now 0.4.0.

### Notes
- EPUB, FB2, MOBI and AZW3 have built-in reading adapters.
- LIT reading uses a dedicated PageArc adapter and a local conversion-provider boundary; calibre remains optional and is not bundled.
- Original ebook files are never modified.

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
- Dedicated normalized-book cache; original ebook files are never modified.

### Changed
- Reading progress now stores both the flow section and the within-section fraction so font/line-spacing/page-width changes preserve the reading location more accurately.
- EPUB rendering strips active script content and blocks external WebView resource requests and pop-up navigation.

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
