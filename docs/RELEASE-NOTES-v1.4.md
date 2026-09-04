# PageArc v1.4

PageArc v1.4 turns the application into a lightweight base reader by moving the heavy calibre conversion runtime into the separate public repository `KiYouJyo/PageArc.ConversionRuntime`.

## Lightweight base package

- calibre 9.13.0 is no longer embedded in the PageArc MSIX.
- The old bundled-runtime provider and local runtime preparation script have been removed from the main repository.
- GitHub and Microsoft Store packaging explicitly reject a package that accidentally contains calibre / `ebook-convert.exe`.
- The signed-release acceptance workflow records the actual light MSIX size.

## PageArc.ConversionRuntime

The initial optional runtime release is:

- package: `9.13.0-pagearc.1`
- calibre: `9.13.0`
- archive: `PageArc.ConversionRuntime-win-x64.zip`
- archive size: `282,915,121` bytes
- SHA-256: `1d223227254d6dfacc8f5645caf3cba26434e129cf5bb65decb0a121a61b5322`

The runtime repository publishes the binary archive, machine-readable manifest, checksums, matching calibre source archive and licensing notices.

## On-demand installation

- PageArc prefers an already installed/configured calibre copy when one exists.
- Otherwise, the first conversion operation asks the user before downloading the managed runtime.
- Opening MOBI / AZW3 / LIT through the compatibility conversion path also asks before the first runtime download.
- Download progress is surfaced in the conversion page and reader.
- PageArc validates the pinned release metadata, exact archive size and SHA-256 before extracting.
- Archive extraction rejects path traversal.
- `ebook-convert --version` is executed before the staged runtime becomes active.
- The runtime is installed per-user under `%LOCALAPPDATA%\PageArc\Runtimes\Conversion`, outside the MSIX package.

## Validation

The v1.4 signed-acceptance workflow validates all 20 directed EPUB / FB2 / MOBI / AZW3 / LIT conversion pairs using the detached runtime, then builds the PageArc MSIX and verifies that the runtime is absent from the package.

## Version

- PageArc: `1.4`
- Assembly / sideload MSIX: `1.4.0.0`
- Microsoft Store identity: `2026.904.140.0`
