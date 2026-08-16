# Data storage

PageArc v0.1 uses `%LOCALAPPDATA%\PageArc`.

```text
PageArc/
├─ settings.json
├─ library.json
└─ Cache/
   └─ Books/
      └─ <book-id>/
```

- `settings.json`: UI language, app/read theme, typography and reader preferences.
- `library.json`: local file references and lightweight reading progress.
- `Cache/Books`: extracted EPUB content used by the local WebView2 reading host.

Deleting `Cache` must not delete library records or original ebook files. PageArc never writes into the source ebook.
