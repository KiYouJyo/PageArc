# PageArc v0.9.3 reader follow-up

This follow-up closes the three visual/interaction gaps found during installed-app acceptance.

## Figma source of truth

- `53:109` — selection-note popup, simplified to a single autosaving note field with no Save or Close control.
- `60:6` — expanded View mode state with vertical/horizontal/wrapped scrolling, spread choices, zoom, auto sizing, fit width and fit height.
- `16:227` — base full-height reading settings pane.

## Runtime contract

- Selection notes debounce-save while typing and flush immediately when selection is cleared or the popup is dismissed. A stable annotation id is reused so autosave updates one note instead of creating duplicates.
- Reader toolbar, left pane, right settings pane and surrounding reading area are transparent over the same Mica backdrop as the title bar. The document page remains independently themed.
- The View mode selector retries initialization when the settings pane loads and whenever Aa opens, preventing a timing-dependent missing selector.

## Validation

- Ordinary CI run `32010301996`: 116/116 tests, Debug x64, Release x64 and whitespace passed.
- Signed follow-up acceptance run `32010583870`: contract validation, Release tests/build, signing, installation, package-version check and packaged launch all passed.
- Acceptance artifact: `PageArc-v0.9.3-x64-reader-followup-signed-acceptance` (`9281673646`).
- Artifact digest: `sha256:e5f31abcc3955385956d7e612119a4548a2b0378a7d0ab585130586c5d95c0b7`.
- Acceptance MSIX SHA-256: `7650ac1e76cd7273f939e74dd6825d5679ee21866c9061fcee964273b0149776`.
- The acceptance package uses package version `0.9.3.2` only so it upgrades the earlier `RefinedInteractionAcceptance` install. Repository product/package versions remain `0.9.3` / `0.9.3.0`.
- Final cleanup CI run `32010929766`: 116/116 tests, Debug x64, Release x64 and whitespace passed.
