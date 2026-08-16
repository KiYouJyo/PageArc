# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

**PageArc** is a WinUI 3 ebook reader for Windows focused on reflowable ebook formats. The UI follows the project Figma closely while keeping the reading path local-first and leaving original ebook files untouched.

## v0.1.0

PageArc v0.1.0 is the first public release. It includes:

- a PAGEARC Figma-aligned WinUI 3 / Windows App SDK shell with Library, Categories, Reader, Format Conversion, Import Folders, Settings and About views;
- an adaptive NavigationView, native Windows Fluent icons, and cyan / neutral-gray navigation surfaces tied to window activation state;
- Simplified Chinese, Japanese and English resources, Follow system, and in-place language switching without recreating the app window;
- local library, categories, favorite filtering, reading progress and reading-preference persistence;
- EPUB 2 / EPUB 3 metadata, OPF, spine and nav / NCX parsing with safe local caching;
- native WinUI EPUB text reading with TOC navigation, previous/next chapter, progress, font size, line spacing and reading themes;
- user-invoked GitHub Release update checking from About;
- CI, automated tests, signed MSIX validation and baseline repository documentation.

**Format status:** the stable v0.1.0 reading path is **EPUB**. The library recognizes FB2 / MOBI / AZW3 / LIT, but their reading adapters are planned for later versions. The Format Conversion page currently provides the UI and task flow only; conversion engines are not included in v0.1.0.

## Privacy

No account is required. Library metadata, settings and reading data stay on the device. Original ebook files are never modified. Normal reading is offline; v0.1.0 accesses GitHub only when the user explicitly chooses **Check for updates**.

## Build

```powershell
dotnet restore PageArc.slnx
dotnet build PageArc.slnx -c Debug -p:Platform=x64
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Debug -p:Platform=x64
```

See [docs/ROADMAP.md](docs/ROADMAP.md), [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [PRIVACY.md](PRIVACY.md), and [CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT.
