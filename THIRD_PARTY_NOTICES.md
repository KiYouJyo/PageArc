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

## calibre 9.13.0 — optional on-demand conversion runtime

Starting with PageArc v1.4, calibre is **not embedded in the PageArc application package**. When a conversion-dependent feature is first requested, PageArc can download the pinned runtime package from the separate public repository `KiYouJyo/PageArc.ConversionRuntime`.

- Runtime repository: `KiYouJyo/PageArc.ConversionRuntime`
- Runtime package revision: `9.13.0-pagearc.1`
- calibre version: 9.13.0
- License: GNU General Public License v3 (GPLv3)
- Runtime archive: `PageArc.ConversionRuntime-win-x64.zip`
- Runtime archive SHA-256 pinned by PageArc v1.4: `1d223227254d6dfacc8f5645caf3cba26434e129cf5bb65decb0a121a61b5322`
- Corresponding source archive: `calibre-9.13.0.tar.xz`, published beside the runtime release

The optional runtime is installed per-user under PageArc's local runtime directory and can be removed independently from PageArc. calibre and its bundled dependencies retain their own copyrights and licenses. PageArc itself remains MIT licensed. The source ebook is never modified and PageArc does not attempt DRM removal.

If a compatible calibre installation is already available on the system, PageArc prefers that installation and does not need to download its managed runtime.
