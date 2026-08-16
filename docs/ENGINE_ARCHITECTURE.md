# PageArc Flow Engine Architecture

PageArc 0.2–0.4 converges all supported reflowable ebook formats on one reader contract instead of adding format checks to `ReaderPage`.

## Core contract

`FlowReaderEngine` selects an `IFlowBookAdapter` and returns an `IFlowBookSource`. Every source exposes the same `FlowDocument` metadata, ordered reflow sections, table of contents and section-on-demand loader. Reading position is represented by `FlowContentLocator` (`section + fraction + optional fragment/text quote`) so bookmarks, highlights, notes and search results can survive renderer changes.

The first adapters are EPUB and FB2. EPUB wraps the proven 0.1 parser/cache path. FB2 parses FictionBook XML and generates safe semantic HTML one top-level section at a time. MOBI/KF8 and LIT are added without changing the reader contract.

## Rendering direction

The WinUI reader shell remains the Figma-approved PageArc reader. Format parsing is isolated from presentation. The 0.2 reader host can consume `FlowSectionContent`; the later WebView-based reflow renderer will preserve the same command bar, contents pane, page width and progress geometry from the Figma reader node.

For MOBI/KF8, the preferred 0.3 implementation path is a pinned, vendored browser parser/renderer adapter rather than a second native reader UI. Any vendored reader code must be pinned, license-attributed and sandboxed so ebook scripts cannot execute.

## Conversion pipeline

Conversion is provider-based. `EbookConversionService` understands the five PageArc target formats: EPUB, FB2, MOBI, AZW3 and LIT. Providers declare the format pairs they support and never modify the source file.

The initial provider is a bridge to calibre `ebook-convert` when it is installed/configured. It is deliberately not bundled in 0.2: PageArc is MIT-licensed while calibre is GPLv3, so redistribution/package strategy must be handled explicitly rather than silently copying calibre libraries into the app. The provider accepts only DRM-free input and reports DRM/encryption failures without attempting bypass.

## Version boundaries

- **0.2:** unified flow contracts, stable content locators, EPUB migration, FB2 reading, first conversion provider, reader/search/annotation foundations.
- **0.3:** MOBI + KF8/AZW3 adapters, lazy resource loading, Kindle metadata/cover/TOC, conversion matrix expansion and compatibility fixtures.
- **0.4:** LIT adapter/normalization path, complete five-format conversion matrix, conversion packaging decision, compatibility hardening and migration tests.

Any user-visible additions or layout changes must be implemented against the PAGEARC Figma file before XAML is changed.
