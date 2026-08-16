# PageArc Roadmap

## v0.1.0 — Foundation + EPUB reading path

- [x] Repository / CI / tests / documentation baseline
- [x] Figma-aligned WinUI 3 shell
- [x] zh-CN / ja-JP / en-US resources and in-app language switching
- [x] GitHub Release update check
- [x] Local library/settings persistence
- [x] EPUB 2 / EPUB 3 reading path
- [x] Signed x64 MSIX acceptance pipeline

## v0.2.0 — Unified flow engine + FB2

- [x] Unified format registry and flow document contract
- [x] EPUB + FB2 adapters
- [x] WebView2 reflow host and section-relative progress
- [x] Full-text search, bookmarks, annotation store and Figma-aligned reader panes
- [x] Real conversion queue and first conversion-provider abstraction

## v0.3.0 — MOBI + AZW3 / KF8

- [x] Pinned local foliate-js MOBI/KF8 runtime with license provenance
- [x] Built-in MOBI / AZW3 adapter and lazy resource loading
- [x] Native MOBI signature / PalmDOC encryption probe
- [x] Kindle metadata / cover / language / TOC projection
- [x] Compatibility fallback and DRM stop semantics

## v0.4.0 — LIT + complete conversion matrix

- [x] Dedicated LIT adapter / normalization path behind the flow contract
- [x] Explicit DRM-free mutual conversion capability matrix among EPUB / FB2 / MOBI / AZW3 / LIT
- [x] Conversion runtime packaging / licensing boundary decision
- [x] LIT normalization, DRM and migration regression tests
- [x] Final x64 signed acceptance pass: test, build, sign, install and launch
- [x] GitHub Release `v0.4.0` with signed acceptance package

## v0.5.0 — Library completion

- [ ] Figma-aligned library search, sort and filter behavior
- [ ] Rich metadata and real cover extraction across supported formats
- [ ] Batch file/folder import with progress, added/skipped/error summary and cancellation
- [ ] Stable duplicate detection and incremental folder rescans
- [ ] Book details panel and full book context menu
- [ ] Collections/categories completion and favorites consistency
- [ ] Missing-file detection and safe library removal semantics
- [ ] Persistent library view/sort preferences
- [ ] Large-library virtualization and import/query performance tests
- [ ] Storage migration and compatibility coverage

## v0.6.0 — Windows deep integration

- [ ] Native file associations for EPUB / FB2 / MOBI / AZW / AZW3 / LIT
- [ ] File activation and command-line open routed into the existing PageArc instance
- [ ] Single-instance redirection and activation queue safety
- [ ] Explorer “show file location” and native shell open workflows
- [ ] Windows jump list / recent-book integration
- [ ] Protocol deep links for library/book/reader navigation where appropriate
- [ ] Packaged activation manifest and signed-install acceptance coverage
- [ ] Windows integration regression tests and fallback behavior for unpackaged development builds
