# Architecture

PageArc separates format parsing, book state, and rendering so additional ebook formats can share one reflowable reading model without modifying original ebook files.

```text
WinUI 3 Shell
├─ Library / Categories / Conversion / Settings / About / Import Folders
└─ Reader
   ├─ Format layer
   │  ├─ EPUB 2 / EPUB 3 (v0.1 active)
   │  ├─ FB2 (planned)
   │  ├─ MOBI/AZW3 (planned)
   │  └─ LIT (planned)
   └─ Reflowable renderer
      ├─ Native WinUI text reader (v0.1 active)
      └─ Rich HTML/CSS renderer (future)
```

## Core rules

1. Original ebook files are read-only from PageArc's point of view.
2. Parsed/extracted content lives only under the PageArc cache directory and can be rebuilt.
3. Reading state is independent from book file format.
4. v0.1 uses a native WinUI text reader for the stable EPUB reading path; richer HTML/CSS fidelity remains a later renderer layer and must not block basic reading.
5. A malformed book must fail the single import/open operation without taking down the library.
6. DRM bypass is out of scope.
7. UI language and theme changes are applied in place and must preserve the active window geometry and navigation state.


## v1.4 optional conversion runtime

The conversion provider boundary is now physically separated from the application package:

```text
PageArc MSIX
├─ built-in EPUB / FB2 / Kindle parser assets
├─ EbookConversionService
│  ├─ external system calibre provider (preferred when present)
│  └─ PageArcManagedConversionProvider
│     └─ ConversionRuntimeManager
└─ no calibre payload

%LOCALAPPDATA%/PageArc/Runtimes/Conversion
└─ 9.13.0-pagearc.1/win-x64
   └─ runtime/ebook-convert.exe

GitHub: KiYouJyo/PageArc.ConversionRuntime
└─ pinned release + manifest + SHA-256 + matching GPL source
```

The runtime manager pins the release tag, archive filename, byte size and SHA-256; extracts into a staging directory with path traversal checks; validates `ebook-convert --version`; and only then activates the per-user installation. PageArc updates therefore do not carry the heavy runtime again.
