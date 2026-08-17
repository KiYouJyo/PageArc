# PageArc v0.9.2 — Reader Figma restoration

Source of truth: PAGEARC Figma file `f0NAlCj9L7yAxPcZRgwxQy`.

## Inspected frames

- `16:156` — Reader
- `16:227` — Reader / reading settings
- `16:2` — canonical screen inventory and related reader states

## Measured 1440 × 900 baseline

| Surface | Figma target |
| --- | --- |
| Reader command bar | 48 px high |
| Back-to-library control | 102 × 32 px, x=12 |
| Reader tool group | 332 × 32 px, 6 px gaps |
| Contents/Search | 76 × 32 px each |
| Aa / More | 40 × 32 px each |
| Bookmark | 76 × 32 px |
| Reader side pane | 260 px wide |
| Chapter row | 228 × 38 px, 4 px vertical gap |
| Reading area | 1180 × 804 px |
| Default page | 760 × 704 px, y=28 |
| Previous/Next controls | 40 × 40 px circular controls |
| Bottom progress strip | 760 × 38 px |
| Reading-settings flyout | 336 × 620 px |
| Theme cards | 88 × 58 px, 10 px gaps |

## v0.9.2 behavior

The default reader theme follows the effective application theme. A light app therefore opens a light reading surface and a dark app opens a dark reading surface. The Figma Light, Sepia, and Dark cards remain explicit reader overrides. Choosing one disables follow-app behavior until **Restore defaults** is used; Restore defaults resumes follow-app behavior.

The XAML reader chrome uses the measured Figma geometry while retaining the existing PageArc flow engine, WebView2 document host, sidebar state machinery, search, bookmarks, notes, annotations, progress persistence, and format adapters.

## Manual acceptance focus

Compare an installed v0.9.2 build at a 1440 × 900 logical window against Figma node `16:156`, then open the Aa flyout and compare against `16:227`. Repeat in both light and dark application themes. The dark appearance is a theme adaptation of the light Figma frame: layout remains identical while surfaces/text use PageArc/WinUI dark theme resources.
