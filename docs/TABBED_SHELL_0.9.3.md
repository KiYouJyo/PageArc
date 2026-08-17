# PageArc v0.9.3 — Tabbed shell and reader contract

## Figma source of truth

The visible v0.9.3 shell is implemented against the approved PAGEARC Figma file.

- Startup / new-tab Home: node `44:2`
- Reader: node `16:156`
- Reader settings: node `16:227`
- Bookmarks sidebar: node `16:1025`
- Notes sidebar: node `16:1088`
- Search sidebar: node `16:1149`

## Shell model

The first 48 px row is the PageArc document tab strip. PageArc starts with a Home tab. The add-tab button creates another Home tab. Opening a book creates a Reader tab; opening the same book again activates the existing tab. Multiple distinct Reader tabs remain alive simultaneously so WebView position, sidebar state and reader controls are preserved while switching books.

Closing a Reader tab disposes its flow source after persisting the current locator. Closing the final tab creates a Home tab rather than leaving the window without a workspace.

The Home surface continues to own the PageArc NavigationView (Library, Categories, Conversion, Import folders, Settings and About). Reader tabs do not duplicate that application navigation inside a book.

## Reader chrome

The reader's own 48 px command row contains only:

- unified sidebar toggle;
- current book title;
- `Aa` reading settings;
- `•••` document/annotation actions.

There is no Back-to-Library button. The window tab strip is the navigation mechanism between Home and books.

The 260 px reader sidebar contains four modes in a single persistent pane: Contents, Search, Bookmarks and Notes. Switching a mode does not create or replace another sidebar. Opening Bookmarks is navigation-only; adding/removing the current bookmark is a separate action inside the Bookmarks mode.

## Progress and page jump

The bottom reader strip exposes the canonical saved progress as an interactive slider. Slider seeking is debounced so dragging does not repeatedly rebuild sections.

Reflowable formats do not contain stable printed page numbers, so PageArc exposes **logical reflow pages** for direct page input. `FlowPageMap` derives a stable per-section logical-page count from the source section sizes and maps each logical page back to the existing `section index + fraction` locator. The canonical persisted position remains the flow locator, not the displayed logical page number.

## Non-goals

v0.9.3 does not replace the existing FlowReaderEngine, format adapters, annotation storage, conversion providers or local-first library model. It restructures the shell and reader interaction layer around those existing engines.
