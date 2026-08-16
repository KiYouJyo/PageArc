# PageArc Roadmap

## v0.1.0 — Foundation + EPUB reading path

- [x] Repository / CI / tests / documentation baseline
- [x] Figma-aligned WinUI 3 shell
- [x] Adaptive NavigationView and native Windows Fluent icons
- [x] Activation-aware cyan / neutral-gray navigation surfaces
- [x] zh-CN / ja-JP / en-US resources and in-app language switching
- [x] GitHub Release update check
- [x] Local library/settings persistence
- [x] Categories and favorite filtering
- [x] EPUB 2 / EPUB 3 metadata + OPF + spine + nav / NCX parsing
- [x] Native WinUI EPUB reading path
- [x] Basic reading theme, font size and line-height controls
- [x] Format Conversion UI / task-flow shell
- [x] Signed x64 MSIX acceptance pipeline

## v0.2.0 — Unified flow engine + FB2

- [x] Unified ebook format registry for EPUB / FB2 / MOBI / AZW3 / LIT
- [x] Format-neutral flow document / section / TOC contract
- [x] Stable content-locator abstraction
- [x] EPUB adapter onto the unified reader contract
- [x] FB2 reflow adapter with metadata / TOC / semantic HTML sections
- [x] Conversion-provider abstraction and first calibre `ebook-convert` bridge
- [x] Wire the Figma-approved Reader UI to the unified flow engine
- [x] Add WebView2 reflow host with continuous and paginated section navigation
- [x] Persist section-relative reading position across renderer/layout changes
- [x] Wire the Figma-approved Conversion UI to real queue execution
- [x] Harden EPUB WebView content against active ebook scripts and external requests
- [x] Full-text search service and Figma-aligned search pane
- [x] Durable bookmark and annotation data store
- [x] Figma-aligned bookmark and notes panes
- [ ] Text-selection highlight / note creation interaction
- [ ] Cover extraction and richer metadata
- [ ] EPUB + FB2 compatibility fixture expansion
- [ ] Touch / keyboard navigation refinement

## v0.3.0 — MOBI + AZW3 / KF8

- [x] Pin and license-review `foliate-js` MOBI/KF8 parser at commit `78914aef4466eb960965702401634c2cb348e9b1` (MIT)
- [x] Vendor exact parser/zlib runtime locally with blob identity verification and third-party notices
- [x] Built-in MOBI / AZW3 parser integration behind the flow adapter contract
- [x] Lazy Kindle section/resource loading through an isolated parser WebView2
- [x] Native MOBI signature and PalmDOC encryption probe
- [x] Stop adapter fallback for confirmed DRM while retaining compatibility fallback for ordinary parser failures
- [x] Kindle metadata / cover / language / TOC projection into `FlowDocument`
- [x] Self-contained resource materialization before the visible Reader WebView
- [x] Cross-format provider matrix for EPUB / FB2 / MOBI / AZW3
- [x] MOBI/AZW3 flow-contract, lifecycle, pinning and DRM regression tests
- [ ] Expand real-world MOBI6 / KF8 corpus acceptance coverage

## v0.4.0 — LIT + complete conversion matrix

- [ ] Built-in LIT adapter / normalization path behind the flow contract
- [ ] Complete DRM-free mutual conversion among EPUB / FB2 / MOBI / AZW3 / LIT
- [ ] Conversion runtime packaging / licensing decision
- [ ] Compatibility hardening and migration tests
- [ ] Final five-format reader and conversion acceptance pass
