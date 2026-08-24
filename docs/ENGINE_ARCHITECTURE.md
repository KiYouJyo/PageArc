# PageArc Flow Engine Architecture

PageArc 0.2–0.4 converges all supported reflowable ebook formats on one reader contract instead of adding format checks to `ReaderPage`.

## Core contract

`FlowReaderEngine` selects an `IFlowBookAdapter` and returns an `IFlowBookSource`. Every source exposes the same `FlowDocument` metadata, ordered reflow sections, table of contents and section-on-demand loader. Reading position is represented by `FlowContentLocator` (`section + fraction + optional fragment/text quote`) so bookmarks, highlights, notes and search results can survive renderer changes.

Current format paths:

- **EPUB** — built-in EPUB 2 / EPUB 3 parser and cache.
- **FB2** — built-in FictionBook XML parser producing semantic flow sections.
- **MOBI / KF8 / AZW3** — built-in pinned foliate-js Kindle parser executed in an isolated local WebView2 runtime; sections/resources are loaded lazily and projected into `FlowDocument`.
- **LIT** — dedicated `LitFlowAdapter` using a provider-backed, read-only LIT→EPUB normalization cache. The normalized EPUB then enters the same flow engine.

`FlowReaderEngine` supports ordered same-format adapters. Ordinary compatibility failures may fall through to the next matching adapter, but `DrmProtectedEbookException` always stops immediately so provider fallback can never become an accidental DRM-bypass path.

## Rendering

The WinUI reader shell remains the Figma-approved PageArc reader. Format parsing is isolated from presentation. The visible WebView2 receives only PageArc-controlled section HTML. EPUB active content is stripped, external requests are blocked, and Kindle blob resources are materialized into self-contained data before they reach the visible reader.

The Kindle parser uses a separate 1×1 transparent, non-interactive WebView2. Its parser code is pinned and packaged locally; it does not load parser code from a CDN. This runtime is infrastructure only and does not create a second visible reader UI.

Continuous reading and paginated reading share the same section-relative progress model. Reader state persists both the section index and within-section fraction, so changing font size, line spacing or page width preserves position more accurately than chapter-only progress.

## Search and reading data

`FlowSearchService` searches section plain text independently of the source ebook format. Search results carry stable section/fraction locators and can be highlighted in the visible reader.

`ReadingDataService` stores bookmarks and annotations separately from the ebook file. Search, Bookmarks and Notes reuse the Figma-approved 260px reader side pane rather than introducing format-specific panels.

## Conversion pipeline

Conversion is provider-based. `EbookConversionService` understands the five PageArc target formats: EPUB, FB2, MOBI, AZW3 and LIT. Providers declare the format pairs they support and never modify the source file.

Five formats produce 20 ordered cross-format pairs. `GetRequiredCapabilityMatrix()` checks every pair against providers that are actually available at runtime, separating two questions:

1. Does PageArc model this format pair?
2. Is a provider capable of performing it installed on this machine?

Official x64 packages bundle the pinned calibre `ebook-convert` runtime behind `PageArcBundledConversionProvider`; source/development builds retain the external-calibre fallback. PageArc remains MIT-licensed and calls the GPLv3 runtime across a process/file boundary. DRM-protected input is reported as unsupported; PageArc never attempts removal.

The same provider abstraction is used by the LIT flow adapter. A LIT source is converted into PageArc's local normalized cache, stamped with source size/mtime, and then opened through the EPUB adapter. Original LIT files remain read-only.

## Licensing boundaries

- PageArc main executable: MIT.
- Vendored foliate-js MOBI parser subset: MIT, exact commit/blob pinned in `ThirdParty/foliate-js/PIN.md`.
- Vendored fflate runtime: MIT.
- calibre: bundled GPLv3 runtime in official x64 packages, with an optional external fallback for development builds.
- ConvertLIT: not copied/linked into the PageArc executable; see `docs/LIT_COMPATIBILITY.md`.

## Version boundaries

- **0.2:** unified flow contracts, EPUB migration, FB2 reading, WebView2 reader, search/bookmark/notes foundations, real provider-based conversion queue.
- **0.3:** built-in MOBI/KF8/AZW3 parsing, lazy Kindle resources, native encryption probe, metadata/cover/TOC projection and compatibility fallback.
- **0.4:** dedicated LIT normalization adapter, explicit complete five-format conversion capability matrix, licensing/package boundary decision, and five-format compatibility hardening.

Any user-visible additions or layout changes must be implemented against the PAGEARC Figma file before XAML is changed.
