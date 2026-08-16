# PageArc Kindle parser pin

PageArc v0.3 uses a narrowly scoped, vendored subset of `johnfactotum/foliate-js` for DRM-free MOBI/KF8 parsing.

- Upstream repository: `johnfactotum/foliate-js`
- Pinned upstream commit: `78914aef4466eb960965702401634c2cb348e9b1`
- License: MIT (see `ThirdParty/foliate-js/LICENSE`)
- Vendored parser file: `mobi.js`
- Expected Git blob SHA-1 for `mobi.js`: `20c77a83db677cc01a0549bc8dad073ab7e9f030`
- Upstream package metadata at this pin declares `fflate ^0.8.2` as the zlib dependency used by the MOBI runtime.
- Vendored `vendor/fflate.js` is the rollup output present at the same foliate-js commit.
- Expected Git blob SHA-1 for `vendor/fflate.js`: `fab2b3ee006a57d4a705be3a5b9e0e9cdeae7ea0`
- fflate license: MIT (see `ThirdParty/fflate/LICENSE`)

The vendored runtime is loaded only from the application package. PageArc does not load parser code from a CDN or execute JavaScript embedded in ebooks. The source ebook remains read-only, and DRM removal is outside the project scope.
