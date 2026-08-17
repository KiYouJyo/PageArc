# PageArc v0.9.3 — Tabbed shell and reader contract

## Figma source of truth

The visible v0.9.3 shell is implemented against the approved PAGEARC Figma file and the post-acceptance refinement pass requested on 2026-08-17.

- Startup / new-tab Home: node `44:2`
- Reader: node `16:156`
- Reader settings: node `16:227`
- Bookmarks sidebar: node `16:1025`
- Notes sidebar: node `16:1088`
- Search sidebar: node `16:1149`
- Text-selection annotation state added during refinement: node `53:109`

The Figma file was refined first, then the WinUI implementation was updated to follow those screens.

## Shell model

The first 48 px row is the PageArc document tab strip. PageArc starts with a Home tab. The add-tab button creates another Home tab. Opening a book creates a Reader tab; opening the same book again activates the existing tab. Multiple distinct Reader tabs remain alive simultaneously so WebView position, sidebar state and reader controls are preserved while switching books.

Tabs deliberately do **not** use the browser-like connected `TabViewItem` silhouette. Home and Reader tabs are detached long rounded rectangles, matching the Figma title-bar states: Home uses a 220 × 36 baseline, Reader tabs use a 300 × 36 baseline, adjacent tabs have an 8 px gap, and selected/unselected states are communicated by subtle Fluent fills and strokes.

Closing a Reader tab disposes its flow source after persisting the current locator. Closing the final tab creates a Home tab rather than leaving the window without a workspace.

The Home surface continues to own the PageArc NavigationView (Library, Categories, Conversion, Import folders, Settings and About). Reader tabs do not duplicate that application navigation inside a book.

## Reader chrome

The reader's own 48 px command row contains only the unified sidebar toggle, current book title, a generic `•••` command affordance, and `Aa` reading settings. `Aa` is intentionally the far-right reader tool.

There is no Back-to-Library button. The window tab strip is the navigation mechanism between Home and books.

The 260 px reader sidebar contains four modes in a single persistent pane: Contents, Search, Bookmarks and Notes. The mode strip is 236 × 36 at the Figma baseline, with larger hit targets, 4 px spacing and slightly expanded label character spacing. Switching a mode does not create or replace another sidebar. Opening Bookmarks is navigation-only; adding/removing the current bookmark is a separate action inside the Bookmarks mode.

## Reading settings pane

Reading settings no longer use a floating flyout. Activating `Aa` opens a dedicated 260 px full-height pane on the **right** side of the reader, mirroring the structural role of the left reader sidebar. The reading area contracts while the pane is open so settings never cover the page.

The pane retains the existing theme, font family, font scale, line spacing, page width, continuous scrolling, progress visibility and restore-defaults controls.

## Selection-driven annotations

Highlight and note creation are selection-driven instead of being permanent items in the top reader command area. When the user selects text inside the reading WebView, PageArc reports the selection rectangle to WinUI and opens a compact Zotero-style contextual editor beside the selected text.

The contextual editor exposes highlight color, an optional multiline note field and Save. Saving continues to use the existing `ReadingDataService` annotation model and durable flow locator. The Notes sidebar remains the place for browsing and navigating existing annotations.

## Progress and page jump

The bottom reader strip exposes the canonical saved progress as an interactive slider. Its layout uses explicit non-overlapping columns for chapter name, slider, `Page` label, page input, page total and percentage. Long chapter names are single-line ellipsized instead of painting over the slider or page controls. The direct page input uses a 64 px baseline width.

Slider seeking is debounced so dragging does not repeatedly rebuild sections.

Reflowable formats do not contain stable printed page numbers, so PageArc exposes **logical reflow pages** for direct page input. `FlowPageMap` derives a stable per-section logical-page count from the source section sizes and maps each logical page back to the existing `section index + fraction` locator. The canonical persisted position remains the flow locator, not the displayed logical page number.

## Regression and signed acceptance

The refinement pass adds source-level regression checks for detached Figma-shaped tabs, the full-height right settings pane, the non-overlapping progress grid, the enlarged four-mode strip and selection-triggered annotation popup.

Final validation for this pass:

- branch-head normal CI: green — 110/110 tests, Debug x64, Release x64 and whitespace checks;
- signed acceptance run `32002795948`: green — Release tests/build, publisher signing and trust verification, MSIX installation/version validation, and real packaged launch;
- signed artifact: `PageArc-v0.9.3-x64-refined-signed-acceptance` (`9279026175`), artifact digest `sha256:d2d719b012efa663faa6c6d2562e34aae00d7b5e907b679dbd288fda3dc5f081`;
- packaged MSIX SHA-256: `314697abb1229361d63c2bf05b1c7fc0c5a2151c654a16932fedb31cfcd8e77d`.

The one-time acceptance workflow was removed after the successful build.

## Non-goals

v0.9.3 does not replace the existing FlowReaderEngine, format adapters, annotation storage, conversion providers or local-first library model. It restructures the shell and reader interaction layer around those existing engines.
