# Architecture

PageArc separates format parsing, book state, and rendering so additional ebook formats can share one reflowable reading experience.

```text
WinUI 3 Shell
├─ Library / Settings / About / Import
└─ Reader
   ├─ Format layer
   │  ├─ EPUB (v0.1 active)
   │  ├─ FB2 (planned)
   │  ├─ MOBI/AZW3 (planned)
   │  └─ LIT (planned)
   └─ Reflowable renderer
      └─ WebView2
```

## Core rules

1. Original ebook files are read-only from PageArc's point of view.
2. Parsed/extracted content lives only under the PageArc cache directory and can be rebuilt.
3. Reading state is independent from book file format.
4. EPUB content is served to WebView2 through a local virtual host mapping, not uploaded to a remote service.
5. A malformed book must fail the single import/open operation without taking down the library.
6. DRM bypass is out of scope.
