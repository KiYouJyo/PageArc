# Format support matrix

PageArc separates **reading support** from **conversion-runtime availability**. Starting with v1.4, the base package no longer contains calibre; the pinned runtime is downloaded on demand from `KiYouJyo/PageArc.ConversionRuntime` only when needed.

| Format | Import/catalog | Reading path | Conversion source | Conversion target |
|---|---|---|---|---|
| EPUB | Built in | Built-in EPUB 2 / EPUB 3 adapter | On-demand runtime | On-demand runtime |
| FB2 | Built in | Built-in FB2 adapter | On-demand runtime | On-demand runtime |
| MOBI | Built in | Built-in local Kindle parser; on-demand-runtime normalization fallback | On-demand runtime | On-demand runtime |
| AZW3 / KF8 | Built in | Built-in local Kindle parser; on-demand-runtime normalization fallback | On-demand runtime | On-demand runtime |
| LIT | Built in | Dedicated LIT flow adapter using on-demand managed normalization | On-demand runtime | On-demand runtime |

## Conversion capability

Five formats produce 20 ordered cross-format pairs. PageArc models all 20 pairs through `EbookConversionService.GetRequiredCapabilityMatrix()`.

PageArc v1.4 first prefers an installed/configured calibre provider. If none exists, `PageArcManagedConversionProvider` installs pinned package `9.13.0-pagearc.1` from `PageArc.ConversionRuntime` after user confirmation. The runtime remains outside the MSIX and is reused across PageArc updates.

The signed v1.4 acceptance workflow downloads and validates the detached runtime, executes all 20 directed conversions, then separately verifies that the PageArc MSIX contains no calibre payload.

## DRM

DRM removal is outside PageArc's scope. Confirmed Kindle encryption and provider-reported DRM are terminal open/conversion conditions rather than signals to try another provider.

## Source-file policy

Original ebook files are never rewritten. Parsing workspaces, normalized EPUB copies and conversion outputs are separate files under PageArc's cache or the user-selected output location.

## On-demand runtime licensing

The bundled conversion runtime remains licensed by its upstream project. See `ThirdParty/calibre/PIN.md` and `THIRD_PARTY_NOTICES.md`. The corresponding calibre source archive is distributed beside official signed v1.0 acceptance/release assets.
