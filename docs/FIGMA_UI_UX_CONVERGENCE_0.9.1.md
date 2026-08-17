# PageArc v0.9.1 — Figma UI/UX convergence pass

This pass follows the PAGEARC Figma file as the visual source of truth and starts from the two issues found during v0.9 manual acceptance. It is intentionally a convergence pass rather than a redesign.

## Figma nodes inspected

- `16:2` — canonical screen inventory
- `16:3` — Library, expanded navigation pane
- `16:346` — Library, collapsed navigation pane
- `16:2206` — Categories
- `16:1750` — Settings
- `16:1878` — About PageArc
- `16:2294` — Import folders
- `25:2` — Format conversion

## Measured shell / library targets

| Element | Figma target | v0.9.1 implementation |
|---|---:|---:|
| title bar | 48 px | 48 px |
| expanded navigation pane | 240 px | 240 px |
| collapsed navigation pane | 64 px | 64 px |
| content padding | 24 px | 24 px |
| library header | 132 px | 132 px |
| search box | 320 px | 320 px |
| sort control | 140 × 32 px | 140 × 32 px |
| view control | 88 × 32 px | 88 × 32 px |
| grid column gap | 26 px | 26 px |
| grid row gap | 24 px | 24 px |
| expanded-pane card width | 258 px | 258 px minimum |
| collapsed-pane card width | ~302 px | fill-stretched from the same 258 px minimum |
| card height | 300 px | 300 px |
| category card height | 206 px | 206 px |

The grid now uses the same four-column geometry in both navigation states instead of keeping the old fixed 240 px card width and leaving an artificial strip of unused space.

## Interaction convergence

### Grid / list view

The Figma library header treats the view control as a first-class view control. v0.9 rendered it as a button but did not wire it. v0.9.1 makes it a real two-state control:

- Grid → List → Grid
- state persists through `AppSettings.LibraryView`
- both modes use the same library data, real covers, open action, reading progress and context menu
- switching view never changes the underlying book records

Figma does not currently include a dedicated list-state frame, so the list layout deliberately reuses the established PageArc card tokens and content hierarchy instead of inventing a visually unrelated screen.

### Favorites

The user-requested behavior intentionally overrides the star shown on some older Figma book-card samples:

- no favorite/star button is drawn over library cards
- favorite remains in the existing book context menu
- source book data and the Favorites filter remain unchanged

This removes an always-visible affordance that competes with real cover art while preserving the existing action.

## First-pass scope

The shell dimensions above are global and therefore improve Library, Categories, Conversion, Import Folders, Settings and About consistently. Library and Categories also receive responsive card-width convergence because their old fixed widths were directly inconsistent with the expanded/collapsed Figma states.

The rest of the canonical screens were re-read during this pass to prevent new styling from drifting away from the existing PAGEARC language. Fine typography, individual control offsets, reader-content rendering details, import-dialog micro-spacing and dark-theme tuning remain suitable for subsequent screenshot-driven passes after this v0.9.1 baseline is manually exercised.
