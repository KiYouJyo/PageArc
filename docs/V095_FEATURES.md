# PageArc v0.9.5 functional milestone

v0.9.5 is a reader/data/runtime completion milestone. Visible additions stay inside the existing PAGEARC Figma settings and reader surfaces instead of introducing a separate design language.

## Reading-data restore

- Backup schema advances to v2 and adds stable book identity metadata.
- Restore accepts v1 and v2 backups.
- Books resolve by exact PageArc ID first, then SHA-256 content fingerprint, then a unique title/author/format identity; legacy v1 backups retain a unique-title fallback.
- Merge mode preserves local bookmark/annotation rows except when the imported row has the same stable item ID.
- Replace mode replaces local bookmarks/annotations with successfully matched backup rows.
- Both modes restore progress for matched books; unmatched records are skipped and reported rather than assigned to the wrong book.

## Built-in conversion runtime

- Official x64 packages bundle the pinned calibre 9.13.0 Windows runtime.
- `PageArcBundledConversionProvider` is the first-choice provider for all 20 directed conversions among EPUB / FB2 / MOBI / AZW3 / LIT.
- The pre-existing external calibre provider remains a development/compatibility fallback.
- The generated runtime is never committed to the repository; `eng/prepare-calibre-runtime.ps1` downloads and extracts the exact pinned release before packaging.
- Signed acceptance also downloads the matching upstream source archive and keeps it beside the packaged artifacts.

## EPUB / CJK rendering compatibility

The reader injects a PageArc-owned compatibility layer after each document navigation:

- strict Chinese/Japanese line breaking and safer CJK wrapping;
- ruby / `rt` layout normalization;
- preservation of explicit vertical-writing documents and mixed text orientation;
- responsive MathML/SVG and horizontally scrollable wide tables;
- no source-ebook mutation.

This is a reflow compatibility pass. It does not claim a full fixed-layout EPUB engine.

## Session restore

- Home and Reader tab identity/order plus the selected tab persist in `%LOCALAPPDATA%\PageArc\shell-session.json`.
- Session JSON is versioned and corruption-safe.
- Reader tabs whose library record/source file no longer resolves are skipped.
- Session restore does not intentionally advance the persisted recent-reading timestamp.

## Footnotes

Same-document EPUB/flow noterefs are intercepted and rendered in a light-dismiss reading-surface popover. The popover offers an explicit jump-to-note action. Cross-document note links fall through to the existing link/navigation behavior.

## Image viewer

Clicking a non-trivial document image opens an in-reader lightbox with zoom in/out and wheel zoom, pointer drag/pan, fit-to-window and 100% modes, Escape/close handling, and Save via the native Windows file picker using only the current book cache or a WebView-produced image data URL.
