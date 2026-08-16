# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

**PageArc** is a WinUI 3 ebook reader for Windows focused on reflowable ebook formats. The UI follows the project Figma closely while keeping the reading path local-first and leaving original ebook files untouched.

## v0.1.0 foundation

The current development branch establishes:

- a Figma-aligned WinUI 3 shell and Library, Reader, Settings and About views;
- a collapsible NavigationView with native Windows `SymbolIcon` glyphs;
- Simplified Chinese, Japanese and English resources with in-app language switching;
- local library/settings persistence;
- EPUB metadata, OPF, spine and EPUB 3 nav parsing;
- local WebView2 rendering with chapter navigation and reading appearance controls;
- user-invoked GitHub Release update checking;
- CI, tests and baseline repository documentation.

**Format status:** PageArc targets EPUB / FB2 / MOBI / AZW3 / LIT. v0.1.0 currently prioritizes the EPUB reading core; other adapters are planned milestones and are not yet advertised as stable reading support.

## Privacy

No account is required. Library metadata, settings and reading data stay on the device. Original ebook files are never modified. The normal reading path is offline; the current v0.1.0 network action is the explicit **Check for updates** request to GitHub.

## Build

```powershell
dotnet restore PageArc.slnx
dotnet build PageArc.slnx -c Debug -p:Platform=x64
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Debug -p:Platform=x64
```

See [docs/ROADMAP.md](docs/ROADMAP.md), [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [PRIVACY.md](PRIVACY.md), and [CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT.
