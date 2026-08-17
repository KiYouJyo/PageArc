# PageArc Figma Functional Audit

This audit is the v0.9 functional-completion gate. It deliberately does **not** reopen the deferred post-v0.6.1 visual-polish pass. New permanent UI remains constrained by the PAGEARC Figma file.

## Canonical Chinese screen inventory

| Figma node | Surface | Code owner | Functional status |
|---|---|---|---|
| `16:3` | Library | `LibraryPage` | Complete |
| `16:156` | Reader | `ReaderPage` | Complete |
| `16:227` | Reader settings | `ReaderPage` | Complete |
| `16:346` | Library collapsed sidebar | `MainWindow` / `LibraryPage` | Complete |
| `16:499` | Library empty state | `LibraryPage` | Complete state variant |
| `16:569` | Library search results | `LibraryPage` | Complete state variant |
| `16:666` | Book context menu | `LibraryPage` | Complete |
| `16:842` | Book details | `LibraryPage` | Complete |
| `16:1025` | Reader bookmarks | `ReaderPage` | Complete |
| `16:1088` | Reader notes | `ReaderPage` | Complete |
| `16:1149` | Reader search | `ReaderPage` | Complete |
| `16:1217` | Import chooser | `LibraryPage.Import` | Complete |
| `16:1386` | Import progress | `LibraryPage.Import` | Complete state variant |
| `16:1580` | Import completion | `LibraryPage.Import` | Complete state variant |
| `16:1750` | Settings | `SettingsPage` | Complete |
| `16:1878` | About | `AboutPage` | Complete |
| `16:2206` | Categories | `CategoriesPage` | Complete |
| `16:2294` | Import folders | `ImportFoldersPage` | Complete |
| `25:2` | Format conversion | `ConversionPage` | Complete |

The 19-node inventory is also represented by `PageArcFigmaSurfaces.Canonical` and guarded by regression tests so future work cannot silently drop a planned surface.

## Functional conclusions

- Reader controls from Figma are wired: theme, font family/size, line spacing, page width, continuous scrolling, progress visibility and reset. Search, bookmarks, highlights and notes share the flow locator contract.
- Import chooser/progress/completion, watched folders, categories, book details/context actions and Windows activation all route into the same persisted library model.
- Settings controls persist their values. Reading data backup exports bookmarks, highlights/notes and progress. Cache clearing is limited to generated cache data and does not intentionally remove the library, settings or reading-data records.
- Format conversion has a real provider-backed queue and reports provider availability truthfully. The old `Conversion_EnginePending` resource key is legacy/unreferenced copy; no runtime surface uses it.
- About reports the running assembly version, checks GitHub Releases on demand, and names the bundled third-party components/license boundary instead of displaying placeholder license copy.

## Localization audit

Static UI remains resource-backed in Simplified Chinese, Japanese and English. Runtime-created text must resolve through `App.Localization.CurrentLanguage` (directly or through a small helper such as `RuntimeText`) rather than reading the raw preference value; this preserves correct behavior when the user selects “Follow system”. Existing runtime menus/dialogs in Library, Import Folders, Settings, Reader and About follow this effective-language rule.

## Deferred work

Visual alignment, spacing, icon polish, surface opacity, typography tuning and other UX/display issues that do not block a planned control from functioning remain intentionally deferred until after the v0.9 functional-completion gate, as requested. They should be handled in a dedicated polish pass against the same Figma source of truth.
