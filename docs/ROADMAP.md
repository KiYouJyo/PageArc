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
- [ ] Wire the Figma-approved Reader UI to the unified flow engine
- [ ] Wire the Figma-approved Conversion UI to real queue execution
- [ ] Full-text search
- [ ] Durable bookmarks / highlights / notes
- [ ] Cover extraction and richer metadata
- [ ] EPUB + FB2 compatibility fixture expansion
- [ ] Better paginated mode and touch / keyboard navigation

## v0.3.0 — MOBI + AZW3 / KF8

- [ ] MOBI / AZW3 parser integration behind the flow adapter contract
- [ ] Lazy section / resource loading for Kindle content
- [ ] DRM detection and clear unsupported messaging
- [ ] Kindle metadata / cover / TOC
- [ ] Cross-format conversion paths for EPUB / FB2 / MOBI / AZW3
- [ ] MOBI6 / KF8 compatibility fixtures

## v0.4.0 — LIT + complete conversion matrix

- [ ] LIT adapter / normalization path behind the flow contract
- [ ] Complete DRM-free mutual conversion among EPUB / FB2 / MOBI / AZW3 / LIT
- [ ] Conversion runtime packaging / licensing decision
- [ ] Compatibility hardening and migration tests
- [ ] Final five-format reader and conversion acceptance pass
