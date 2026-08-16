# LIT compatibility strategy

Microsoft Reader `.lit` is the last PageArc target format whose practical open-source tooling is materially different from EPUB / FB2 / MOBI / KF8.

## Why PageArc does not embed libmspack for LIT

The libmspack project lists Microsoft Reader LIT among its historical format families, but at pinned commit `55d501976171397ccd5d5a7a1ca7da065b1d9a06` its public LIT decompressor surface is still a placeholder and `litd.c` documents decompression as unfinished. It therefore cannot be used as a production LIT reader backend.

## Why ConvertLIT is not copied into the PageArc executable

ConvertLIT 1.8 source is preserved by Debian and provides Windows/POSIX extraction tooling for Microsoft Reader files. The preserved package is GPL-2.0-or-later. PageArc itself is MIT-licensed, so v0.4 deliberately avoids copying or linking ConvertLIT implementation code into the main application executable.

A future separately distributed helper is possible only if its GPL source/license obligations and update path are handled explicitly. It must remain a process/file boundary rather than becoming an undocumented embedded dependency.

## v0.4 runtime model

PageArc ships a dedicated `LitFlowAdapter`, but LIT payload normalization is provider-based:

1. Keep the original `.lit` file read-only.
2. Ask `EbookConversionService` for an installed provider that declares `LIT → EPUB`.
3. Convert into PageArc's local normalized-book cache.
4. Stamp the cache with the source file size and modification time so edits invalidate it.
5. Open the normalized EPUB through the same `FlowDocument` engine used by all other PageArc formats.
6. If the provider reports DRM/encryption, raise `DrmProtectedEbookException` and stop. PageArc never attempts DRM removal.

The default provider is local calibre `ebook-convert` when calibre is already installed or explicitly configured. calibre is not bundled with PageArc.

## Conversion matrix

The five target formats create 20 ordered cross-format conversion pairs:

- EPUB
- FB2
- MOBI
- AZW3
- LIT

`EbookConversionService.GetRequiredCapabilityMatrix()` now checks every pair against providers that are actually available at runtime. This means the UI/service layer can distinguish “PageArc knows this format pair” from “a provider capable of performing this conversion is currently installed.”

This is intentional: the project does not claim that an unavailable external GPL runtime is silently present.
