# Changelog

## [1.0] - 2026-08-22

### Changed
- Unified the custom title bar and NavigationView pane on one opaque shell-chrome surface so the hamburger rail no longer renders with a different tint.
- Standardized Settings selectors, actions, sliders and label-free switches on a shared right edge across wide and compact layouts.
- Promoted the application and MSIX package versions to `1.0` / `1.0.0.0`.

## [0.9.5] - 2026-08-18

### Added
- Reading-data restore with schema-v2 book identities, Merge/Replace semantics, v1 compatibility and remapping by exact PageArc ID, content fingerprint or unique title/author/format identity.
- A pinned calibre 9.13.0 runtime for official x64 packages, exposed as the preferred `PageArcBundledConversionProvider` so the complete 20-pair EPUB / FB2 / MOBI / AZW3 / LIT conversion matrix works without a separate converter installation.
- Persistent shell-session storage for Home/Reader tab order, identity and selected tab, with corruption-safe fallback and missing-source filtering during restore.
- Reflow reader compatibility for strict CJK line breaking, ruby text, vertical writing-mode, responsive MathML/SVG and horizontally scrollable wide tables.
- Same-document footnote popovers with light-dismiss behavior and an explicit jump-to-note action.
- An in-reader image viewer with wheel/button zoom, pointer pan, fit-to-window, 100% mode and safe Windows-picker Save.

### Changed
- Reading backup format advances from schema v1 to v2 while retaining import compatibility with existing v1 backups.
- LIT normalization and conversion now prefer the runtime shipped with official x64 builds; an externally installed calibre copy remains a development/compatibility fallback.
- Settings keeps the existing PAGEARC Data & storage card and adds Restore beside Backup rather than introducing a new settings surface.
- Reader enhancements are injected into the existing document surface, keeping the v0.9.3 permanent Reader chrome unchanged.
- The application/package version advances to `0.9.5` / `0.9.5.0`.

### Validation
- 121/121 unit tests passed together with Debug x64, Release x64 and whitespace validation before the signed-acceptance pass.
- The signed-acceptance workflow additionally prepares the pinned conversion runtime, executes all 20 directed cross-format conversions, verifies runtime inclusion in the MSIX, signs/verifies the package, and installs/launch-smoke-tests the packaged app.

## [0.9.3] - 2026-08-17

### Added
- Multi-tab title-bar shell with localized Home/library tabs and independent live Reader tabs for multiple open books.
- Persisted Reader view-mode controls for vertical, horizontal and wrapped reading; single/odd/even spreads; zoom in/out; automatic sizing; fit-width and fit-height.
- Mouse-wheel and reading-surface click page turning for horizontal/wrapped reading, while preserving links, inputs and active text selections.

### Changed
- Reader top chrome is reduced to the sidebar toggle, current book title and `Aa`; the obsolete overflow control and visible previous/next page buttons are removed.
- Reader toolbar, left pane, right settings pane and surrounding reading area now reveal the same Mica backdrop as the custom title bar, while the document page continues to follow the selected reading theme.
- The 260 px left Reader sidebar and 260 px right `Aa` pane now open/close with a smooth cubic ease-out width/fade transition.
- Selection annotation UI is temporarily note-only: typing debounce-saves one stable note, dismissal flushes pending text, clearing the note removes that annotation, and note-bearing text uses one muted low-saturation red mark.

### Validation
- 116/116 tests passed together with Debug x64, Release x64 and whitespace validation.
- Signed v0.9.3 follow-up acceptance passed package signing, trust verification, MSIX install/version validation and packaged launch smoke testing.

## [0.9.2] - 2026-08-17

### Fixed
- Restored the Reader UI against the canonical PAGEARC Figma reader and settings surfaces.
- The default reading surface now follows the effective application Light/Dark theme; explicit Light/Sepia/Dark selections remain persistent overrides and Restore defaults returns to follow-app behavior.

### Changed
- Reader command-bar, contents-pane, reading-page and progress geometry were converged to the measured Figma baseline.
- Reading settings were restored with theme, font family, font size, line spacing, page width, continuous scrolling, reading progress and restore-default controls.
- Dedicated neutral Reader surface tokens were added for Light/Dark mode.

### Validation
- Signed v0.9.2 acceptance passed Release tests/build, publisher signing, MSIX installation and packaged launch.

## [0.9.1] - 2026-08-17

### Fixed
- The Library view control is now an actual Grid/List toggle instead of a non-functional visual control, and the selected view persists across launches.
- Removed the always-visible favorite button from Library book covers; favorite remains available from the existing book context menu.

### Changed
- Performed the first screenshot/Figma-driven UI/UX convergence pass against the canonical PAGEARC desktop screens.
- Library and Categories now use the Figma four-column geometry with a 258 px minimum item width, 26 px column gap, 24 px row gap, and fill stretching when the navigation pane is collapsed.
- The NavigationView applies the Figma 240 px expanded and 64 px compact pane widths at runtime while preserving the existing adaptive shell behavior.
- Grid/List modes reuse the same PageArc card tokens, cover pipeline, progress information, opening behavior, and right-click actions.

### Notes
- This is a convergence baseline rather than a redesign. Fine typography, per-control offsets, reader rendering details, dialog micro-spacing, and dark-theme tuning remain suitable for subsequent screenshot-driven passes.

## [0.9.0] - 2026-08-17

### Added
- Canonical 19-screen PAGEARC Figma functional contract and repository audit mapping every planned surface to its implementation owner.
- Cross-feature regression coverage spanning PageArc deep-link activation, the complete five-format conversion matrix, reading annotations/bookmarks and reading-data backup.
- Concrete About-page third-party license information for bundled foliate-js/fflate and the optional external calibre boundary.
- Central helper for runtime-created Simplified Chinese / Japanese / English copy using the effective UI language.
- Signed x64 functional acceptance covering tests, Debug/Release builds, package signing/install, file/protocol registration, launch, import routing and single-instance activation.

### Changed
- About continues to report the running assembly version and check GitHub Releases on demand, with placeholder license copy replaced by actual dependency information.
- The packaged application version is now 0.9.0.
- Cosmetic/experience polish that does not block planned feature functionality remains intentionally deferred to a later Figma-driven pass.

## [0.8.0] - 2026-08-17

### Added
- Complete Figma Settings control wiring for Windows accent source, page width, default library sort, recent books, watched folders and duplicate detection.
- Versioned JSON reading-data backup containing bookmarks, highlights/notes and per-book progress/section location.
- Safe generated-cache maintenance that preserves library, settings and reading-data records while invalidating cached cover paths.
- Category persistence regression coverage and isolated persistence paths for tests.

### Changed
- Settings values persist through the shared settings service and duplicate detection is applied immediately to the active library service.
- The packaged application version is now 0.8.0.

## [0.7.0] - 2026-08-17

### Added
- Complete Figma reader-settings contract: font family, page width, reading-progress visibility and restore defaults in addition to the existing theme/size/spacing/continuous controls.
- Selected-text highlight and note capture through the existing reader tool surface.
- Persistent saved-highlight rendering and annotation navigation using the common flow locator contract.
- Reader setting and annotation persistence regression coverage.

### Changed
- Reader settings now apply immediately without reopening the ebook.
- The packaged application version is now 0.7.0.

## [0.6.1] - 2026-08-17

### Fixed
- Multi-book library/category layout regressions, cover discovery/fallback presentation, Follow-system runtime localization and category navigation.
- Book-details/import surfaces that allowed underlying content to bleed through.
- MOBI/AZW3/LIT calibre normalization now writes and validates a unique temporary EPUB before atomically publishing the normalized cache entry.
- Raw conversion/provider tracebacks are logged while the reader shows compact localized failures.

### Notes
- v0.6.1 became the functional baseline for v0.7-v0.9; remaining visual/experience polish was deliberately deferred.

## [0.6.0] - 2026-08-16

### Added
- Windows App SDK single-instance lifecycle coordinator using a stable PageArc instance key and redirected activation handling.
- File activation for EPUB / FB2 / MOBI / AZW / AZW3 / LIT routed into the existing library/import and reader pipeline.
- `pagearc:` protocol activation for stable book IDs and explicit local ebook paths.
- Windows Jump List recent-book integration backed by `pagearc://book/<id>` deep links.
- Packaged manifest template declaring all six ebook file associations plus the `pagearc` protocol.
- Pure activation parsing tests for quoted Windows paths, all associated extensions, protocol round-tripping and manifest declarations.
- Signed Windows-integration acceptance covering package signing/install, registered shell associations, protocol launch, import routing and same-PID single-instance redirection.

### Changed
- Application startup now registers Windows lifecycle routing before loading PageArc state, queues early redirected activations safely and serializes activation handling.
- Secondary instances fail closed: they exit after attempting redirection rather than creating a second PageArc window if redirect delivery fails.
- Successful book opens update both PageArc recents and the Windows Jump List.
- The packaged application version is now 0.6.0.

### Notes
- v0.6 adds no new visible in-app surface; Windows shell activations reuse the existing Figma-approved library and reader UI.
- Unpackaged development builds retain graceful command-line/local-launch fallback when packaged Windows lifecycle features are unavailable.

## [0.5.0] - 2026-08-16

### Added
- Figma-derived 410 px book-details side panel with reading progress, file information, favorites and bookmark/highlight/note counts.
- Figma-derived book context menu with open, continue, favorite, category, details, file-location and safe library-removal actions.
- Figma three-stage import experience: chooser/drop zone, per-file progress/cancellation, and added/skipped/error completion summary.
- Persistent watched-folder management with recursive supported-format scans, repeat-safe rescans, availability state and per-folder counts.
- SHA-256 content fingerprints for duplicate detection across different source paths.
- Structured single/batch import results and progress contracts.
- Rich EPUB/FB2 metadata extraction including language, publisher, description and embedded cover cache.
- Kindle cover data-URL caching and persistence when MOBI/AZW3 parsing runs; normalized LIT/Kindle fallback metadata and covers when those paths run.
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
