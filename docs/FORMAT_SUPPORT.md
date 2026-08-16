# Format support matrix

PageArc intentionally separates **reading support** from **conversion-provider availability**.

| Format | Import/catalog | Reading path | Conversion source | Conversion target |
|---|---|---|---|---|
| EPUB | Built in | Built-in EPUB 2 / EPUB 3 adapter | Provider-backed | Provider-backed |
| FB2 | Built in | Built-in FB2 adapter | Provider-backed | Provider-backed |
| MOBI | Built in | Built-in local Kindle parser; optional normalization fallback | Provider-backed | Provider-backed |
| AZW3 / KF8 | Built in | Built-in local Kindle parser; optional normalization fallback | Provider-backed | Provider-backed |
| LIT | Built in | Dedicated LIT flow adapter using local provider normalization | Provider-backed | Provider-backed |

## Conversion capability

Five formats produce 20 ordered cross-format pairs. PageArc models all 20 pairs through `EbookConversionService.GetRequiredCapabilityMatrix()`.

A pair is marked executable only when an installed provider says it can perform that conversion. The default provider is calibre `ebook-convert` when calibre is installed or explicitly configured. PageArc does not bundle calibre.

This distinction prevents the format selector from being treated as evidence that an external converter is present.

## DRM

DRM removal is outside PageArc's scope. Confirmed Kindle encryption and provider-reported DRM are terminal open/conversion conditions rather than signals to try another provider.

## Source-file policy

Original ebook files are never rewritten. Parsing workspaces, normalized EPUB copies and conversion outputs are separate files under PageArc's cache or the user-selected output location.
