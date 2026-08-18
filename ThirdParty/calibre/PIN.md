# calibre runtime pin for PageArc

PageArc v0.9.5 official x64 acceptance and release packaging use a fixed local calibre runtime as the built-in conversion engine.

- Upstream: calibre — https://calibre-ebook.com/
- Version: **9.13.0**
- Windows x64 installer: `calibre-64bit-9.13.0.msi`
- Corresponding source archive: `calibre-9.13.0.tar.xz`
- License: GNU General Public License v3 (GPLv3)
- Runtime entry point used by PageArc: `ebook-convert.exe`

The runtime is prepared during the signed packaging workflow and copied into `ThirdParty/calibre/runtime/` before MSIX creation. The generated runtime directory is intentionally not committed to Git because it is a large third-party binary distribution.

The corresponding source archive is retained with the signed acceptance/release assets so recipients of the bundled GPL runtime have direct access to the exact source distribution used for that release.

PageArc invokes calibre as a separate local executable process through the conversion-provider boundary. PageArc itself remains MIT licensed; calibre retains GPLv3 and its own copyright notices.
