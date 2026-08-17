# PageArc v0.9.2 — Reader Figma convergence

Source of truth: PAGEARC Figma file `f0NAlCj9L7yAxPcZRgwxQy`, Chinese page `16:2`.

## Original measured baseline

The first v0.9.2 restoration used the existing reader frames as its geometry contract:

| Surface | Figma target |
| --- | --- |
| Reader command bar | 48 px high |
| Back-to-library control | 102 × 32 px, x=12 |
| Reader side pane | 260 px wide |
| Chapter row | 228 × 38 px, 4 px vertical gap |
| Reading area | 1180 × 804 px with sidebar open |
| Default page | 760 × 704 px, y=28 |
| Previous/Next controls | 40 × 40 px circular controls |
| Bottom progress strip | 760 × 38 px |
| Reading-settings flyout | 336 × 620 px |
| Theme cards | 88 × 58 px, 10 px gaps |

## Screenshot-driven follow-up

The first Windows acceptance pass exposed two defects plus a broader reader-navigation problem:

1. Library list rows could be measured to their content width instead of the available list viewport.
2. The reader's top-level Bookmark entry both opened the bookmark pane and mutated bookmark data.
3. Contents, Search, Bookmarks, and Notes behaved like unrelated sidebars, each reopened from a separate command-bar entry.

The first two defects are fixed in the v0.9.2 branch. The third is redesigned in Figma before WinUI implementation, following the project rule that new UI is designed in Figma first.

## Unified reader-sidebar revision

Updated source-of-truth states:

- `16:156` — main reader
- `16:227` — reading settings
- `16:1025` — bookmarks
- `16:1088` — notes
- `16:1149` — search
- `38:68` — new collapsed-sidebar reader state

The revised interaction contract is:

- One common sidebar show/hide button in the reader command bar.
- Contents / Search / Bookmarks / Notes are persistent categories inside the same 260 px sidebar rather than independent top-right entries.
- The former Contents, Search, and Bookmark command-bar entries are removed; `Aa` and `•••` remain reader-wide actions.
- Opening the Bookmarks category is navigation only. Adding a bookmark is a separate, explicit `添加当前页书签` action inside the Bookmarks category.
- The closed-sidebar state expands the reading area while retaining the same common sidebar toggle.
- The bottom progress control is represented as a seek slider with a draggable thumb.
- The progress strip exposes a page-number field (`页 128 / 296` in the design example) for direct page jump while keeping percentage feedback.

For reflowable books, the displayed page count is virtual pagination derived from the current reader layout. Changing font size, line spacing, page width, or viewport size may therefore change that page count.

## Theme contract

The default reader theme follows the effective application theme. A light app opens a light reading surface and a dark app opens a dark reading surface. The Figma Light, Sepia, and Dark cards remain explicit reader overrides. Choosing one disables follow-app behavior until **Restore defaults** is used; Restore defaults resumes follow-app behavior.

## Implementation gate

The two screenshot-reported defects are runtime fixes in the current v0.9.2 branch. The unified sidebar and seek/page-jump controls are deliberately **not yet implemented in WinUI**: their revised Figma states must be visually accepted first. Once accepted, the next code pass should implement these exact states rather than inventing a separate runtime layout.
