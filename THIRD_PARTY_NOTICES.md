# Third-party notices

PageArc includes or can interoperate with the following third-party components.

## foliate-js — MOBI/KF8 parser subset

- Project: `johnfactotum/foliate-js`
- Pinned source commit: `78914aef4466eb960965702401634c2cb348e9b1`
- Included subset: `mobi.js`
- License: MIT
- License text: `ThirdParty/foliate-js/LICENSE`

PageArc vendors the pinned parser source into the application package so ebook parsing does not depend on a CDN or network connection.

## fflate — zlib runtime used by the Kindle parser

- Project: `101arrowz/fflate`
- Version family declared by the pinned foliate-js package: `^0.8.2`
- Included artifact: the `vendor/fflate.js` rollup output stored by the same pinned foliate-js commit
- License: MIT
- License text: `ThirdParty/fflate/LICENSE`

## calibre 9.13.0 — bundled conversion runtime in official x64 packages

PageArc v0.9.5 official x64 signed packages bundle a fixed calibre **9.13.0** runtime and invoke its `ebook-convert.exe` as a separate local process through PageArc's conversion-provider boundary. This makes EPUB / FB2 / MOBI / AZW3 / LIT conversion and LIT normalization available without requiring a separate calibre installation.

- Project: calibre
- Version: 9.13.0
- License: GNU General Public License v3 (GPLv3)
- Runtime pin / provenance: `ThirdParty/calibre/PIN.md`
- Corresponding source archive: distributed beside the signed v0.9.5 acceptance/release package as `calibre-9.13.0.tar.xz`

calibre and its bundled dependencies retain their own copyrights and licenses. PageArc itself remains MIT licensed. The source ebook is never modified and PageArc does not attempt DRM removal.

Development/source builds that have not prepared the bundled runtime can still use an already installed calibre copy as a compatibility fallback.
