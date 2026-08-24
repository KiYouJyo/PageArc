# PageArc 1.1 package-size audit

Measured from the x64 Microsoft Store MSIX payload on 2026-08-24. Sizes are compressed sizes inside the package and are rounded to two decimals.

## Baseline and implemented reductions

| Build | MSIX size | Change |
|---|---:|---:|
| 1.0 baseline | 338.02 MB | — |
| 1.1 final candidate | 302.42 MB | -35.60 MB (-10.5%) |

The 1.0 x64 package contained Windows runtime payloads for x64, ARM64, and ARM64EC. PageArc 1.1 selects `win-x64` for x64 builds and `win-arm64` for ARM64 builds, so each package contains only its target architecture. The three unreferenced branding master PNGs are also retained in the repository but excluded from the packaged payload (2.72 MB before package compression/metadata recalculation). The generated Store upload archive is 297.93 MB; the table compares like-for-like MSIX bundles.

## 1.1 payload composition before branding-master exclusion

| Component | Packed size | Notes |
|---|---:|---|
| calibre 9.13.0 runtime | 270.66 MB | Required for the bundled 20-direction conversion matrix and metadata fallback |
| DirectML + ONNX Runtime | 16.09 MB | Pulled in by the Windows App SDK runtime; PageArc does not currently use ML |
| Microsoft Windows SDK .NET projection | 6.29 MB | Windows API projection |
| WinUI and related projections | about 3.0 MB | Application UI/runtime projections |
| Branding masters | 2.72 MB | Source-only PNGs; excluded in 1.1 |
| Runtime logo, splash, tiles, PageArc binaries, PRI and other files | remainder | Runtime and manifest assets |

## Further candidates, in priority order

1. **Build a tested minimal calibre conversion image.** The full calibre directory is 270.66 MB packed. Large candidates include Qt WebEngine (80.10 MB), frozen Python modules (28.03 MB), localization archives (16.63 MB), ICU data, developer resources, recipes, text-to-speech dictionaries, viewer/editor assets, and unused GUI launchers. This has the largest potential benefit, but files must be removed only through an allowlisted runtime builder followed by all 20 conversion-pair tests, cover extraction, LIT normalization, multilingual metadata, and packaged execution tests.
2. **Review the Windows App SDK ML payload.** DirectML and ONNX Runtime account for 16.09 MB packed although PageArc does not call ML APIs. Removing these files by a post-build delete relies on internal SDK layout and is not treated as a safe 1.1 change. Revisit when the SDK exposes a supported component opt-out or after a contained startup/update/package test matrix.
3. **Rationalize generated visual assets.** Keep only manifest-required scale/target-size variants plus the in-app 1024 px logo. This is a small saving and should be verified against Start, taskbar, file association, splash, light/dark, and Store ingestion rendering.

Do not enable .NET trimming for this WinUI application solely to reduce package size without a packaged startup and reflection/resource test pass. Do not remove calibre files based only on filename or apparent GUI usage: `ebook-convert.exe` loads Python, Qt, codec, font, ICU, and plugin assets dynamically.
