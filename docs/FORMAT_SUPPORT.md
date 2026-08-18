# Format support matrix

PageArc intentionally separates **reading support** from **conversion-runtime availability** while official v0.9.5 x64 packages now carry a pinned local conversion runtime.

| Format | Import/catalog | Reading path | Conversion source | Conversion target |
|---|---|---|---|---|
| EPUB | Built in | Built-in EPUB 2 / EPUB 3 adapter | Bundled runtime | Bundled runtime |
| FB2 | Built in | Built-in FB2 adapter | Bundled runtime | Bundled runtime |
| MOBI | Built in | Built-in local Kindle parser; bundled-runtime normalization fallback | Bundled runtime | Bundled runtime |
| AZW3 / KF8 | Built in | Built-in local Kindle parser; bundled-runtime normalization fallback | Bundled runtime | Bundled runtime |
| LIT | Built in | Dedicated LIT flow adapter using bundled local normalization | Bundled runtime | Bundled runtime |

## Conversion capability

Five formats produce 20 ordered cross-format pairs. PageArc models all 20 pairs through `EbookConversionService.GetRequiredCapabilityMatrix()`.

Official v0.9.5 x64 packages bundle a pinned calibre 9.13.0 runtime and prefer `PageArcBundledConversionProvider`, so users do not need to install or configure calibre separately. Development/source builds that have not run `eng/prepare-calibre-runtime.ps1` keep the existing external-calibre provider as a compatibility fallback.

The signed v0.9.5 acceptance workflow generates seed EPUB / FB2 / MOBI / AZW3 / LIT books and executes all 20 directed conversions before the package can be accepted.

## DRM

DRM removal is outside PageArc's scope. Confirmed Kindle encryption and provider-reported DRM are terminal open/conversion conditions rather than signals to try another provider.

## Source-file policy

Original ebook files are never rewritten. Parsing workspaces, normalized EPUB copies and conversion outputs are separate files under PageArc's cache or the user-selected output location.

## Bundled runtime licensing

The bundled conversion runtime remains licensed by its upstream project. See `ThirdParty/calibre/PIN.md` and `THIRD_PARTY_NOTICES.md`. The corresponding calibre source archive is distributed beside official signed v0.9.5 acceptance/release assets.
