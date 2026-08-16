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

- [x] Figma-aligned library search, sort and filter behavior
- [x] Rich metadata and real cover extraction across the five-format reading paths, with Kindle/LIT metadata persisted when their parser/normalization path runs
- [x] Figma three-stage batch file/folder import with progress, added/skipped/error summary and cancellation
- [x] Stable content-fingerprint duplicate detection and repeat-safe monitored-folder rescans
- [x] Figma book details side panel and full book context menu
- [x] Collections/categories assignment, counts and favorites consistency across library surfaces
- [x] Missing-file detection and safe library removal semantics that never delete source ebooks
- [x] Persistent library sort/filter/view preferences
- [x] ItemsRepeater virtualization plus 2,000-record persistence/migration coverage
- [x] Storage migration and compatibility coverage for legacy/missing records
- [x] Persistent monitored-folder management with recursive supported-format scanning

## v0.6.0 — Windows deep integration

- [x] Native file associations for EPUB / FB2 / MOBI / AZW / AZW3 / LIT in the packaged manifest
- [x] File / launch-argument activation routed through the existing PageArc library and reader path
- [x] Windows App SDK single-instance registration, redirection, queued startup activation and serialized activation handling
- [x] Explorer “show file location” plus native Open/Open with workflows through registered ebook associations
- [x] Windows Jump List recent-book integration using stable `pagearc://book/<id>` arguments
- [x] `pagearc:` protocol deep links for stable book IDs and explicit local ebook paths
- [x] Packaged file-association/protocol manifest plus signed-install registration validation
- [x] Signed runtime acceptance covering packaged launch, protocol activation, import routing and same-PID single-instance redirection
- [x] Pure activation/parser/manifest regression tests with graceful unpackaged-development fallbacks
