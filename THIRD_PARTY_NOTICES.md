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

## calibre — optional external conversion provider

PageArc does **not** bundle calibre. If a user already has calibre installed, PageArc can invoke its `ebook-convert` executable as an optional provider for DRM-free format conversion and legacy-format normalization fallback. PageArc never modifies the source ebook and does not attempt DRM removal.
