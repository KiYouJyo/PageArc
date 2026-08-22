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
- [x] Explorer “show file location” plus native Open/Open workflows through registered ebook associations
- [x] Windows Jump List recent-book integration using stable `pagearc://book/<id>` arguments
- [x] `pagearc:` protocol deep links for stable book IDs and explicit local ebook paths
- [x] Packaged file-association/protocol manifest plus signed-install registration validation
- [x] Signed runtime acceptance covering packaged launch, protocol activation, import routing and same-PID single-instance redirection
- [x] Pure activation/parser/manifest regression tests with graceful unpackaged-development fallbacks

## v0.6.1 — Hotfix baseline

- [x] Repair the major v0.6 library layout, cover extraction, localization, category navigation and non-EPUB normalization regressions found during signed manual acceptance
- [x] Signed x64 install/launch acceptance and ordinary CI green
- [x] Accept the repaired build as the functional baseline for v0.7; remaining visual/experience polish is intentionally deferred while v0.7-v0.9 functional completion proceeds

## v0.7.0 — Reader interaction completion

- [x] Complete the Figma reading-settings contract: theme, font family, font size, line spacing, page width, continuous scrolling, reading-progress visibility and restore defaults
- [x] Persist and immediately apply every reader setting without reopening the book
- [x] Complete search navigation/highlighting and preserve correct section-relative progress after jumping to a match
- [x] Complete bookmark and annotation navigation against the shared flow locator contract
- [x] Make saved highlights visible again when their section is rendered, without modifying the source ebook
- [x] Add a lightweight annotation capture path for selected text using the existing reader tool surface and native Fluent controls
- [x] Add regression coverage for reader-setting persistence, annotation state and existing search/highlight behavior

## v0.8.0 — Settings, data and library control completion

- [x] Complete every control already present in the Figma Settings screen: Windows accent source, default reader font/size/spacing/page width, default library sort, recent-books visibility, watched folders and duplicate detection
- [x] Implement the Figma “Backup reading data” action for bookmarks, highlights, notes and reading progress
- [x] Make cache clearing remove generated parser/cover/normalization data only and keep library/settings/reading records intact
- [x] Keep category search/new-category/open-category behavior persistent and consistent with the Figma Categories surface
- [x] Add migration/default-value coverage for newly persisted settings and backup schema

## v0.9.0 — Planned-surface integration completion

- [x] Finish the About/Update surface with live version reporting, GitHub Release checking and concrete third-party license information
- [x] Remove stale placeholder/pending copy from live already-implemented conversion and format surfaces; any retained legacy resource key is unreferenced
- [x] Audit all 19 canonical Figma screens so every visible action is wired or intentionally read-only
- [x] Audit zh-CN / ja-JP / en-US runtime-created text for feature-complete localization
- [x] Run end-to-end regression coverage across library → import → categories → reader → annotations → backup → conversion → Windows activation contracts
- [x] Produce a signed x64 acceptance build after v0.7-v0.9 functional gates are green (run `31988316528`, artifact `PageArc-v0.9.0-x64-signed-acceptance`)

## v0.9.5 — Reader, data and local-runtime completion

- [x] Upgrade reading-data backup to schema v2 and add Merge / Replace restore with exact-ID, content-fingerprint and unique book-identity remapping while retaining v1 import compatibility
- [x] Make the official x64 package self-contained for DRM-free conversion by bundling a pinned calibre 9.13.0 runtime behind `PageArcBundledConversionProvider`, with external calibre retained only as a development/compatibility fallback
- [x] Add a reflow EPUB/CJK compatibility layer for strict Chinese/Japanese line breaking, ruby, vertical writing-mode, responsive MathML/SVG and wide-table overflow without modifying source ebooks
- [x] Persist and restore Home/Reader tab order, identity and selected tab while safely skipping missing source books
- [x] Add lightweight same-document footnote popovers with an explicit jump-to-note action
- [x] Add an in-reader image viewer with zoom, pan, fit-to-window, 100% and safe native Save
- [x] Add regression coverage for backup remapping/replace semantics, session persistence, bundled conversion capability and the reader enhancement contracts

> UI/experience polish that is not required to make a planned control functional remains deferred until after the v0.9 functional-completion pass. New visible surfaces must continue to derive from the PAGEARC Figma file rather than introducing an unrelated design language.
