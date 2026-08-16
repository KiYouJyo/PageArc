# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

**PageArc** は、リフロー型電子書籍に特化した WinUI 3 製 Windows リーダーです。Figma を UI の基準としつつ、ローカル優先・元ファイル非変更の読書体験を重視します。

## v0.1.0 基盤

現在の開発ブランチでは次を実装しています。

- Figma に沿った Library / Reader / Settings / About の WinUI 3 UI
- Windows ネイティブ `SymbolIcon` を使う折りたたみ式 NavigationView
- 简体中文 / 日本語 / English の三言語リソースとアプリ内言語切替
- ローカルのライブラリ・設定保存
- EPUB の metadata / OPF / spine / EPUB 3 nav 基礎解析
- WebView2 によるローカル本文表示、章移動、読書テーマ・文字サイズ・行間
- ユーザー操作時のみ実行する GitHub Release 更新確認
- CI、テスト、プライバシー文書、ロードマップ等のリポジトリ基盤

**形式の状況:** 製品目標は EPUB / FB2 / MOBI / AZW3 / LIT です。v0.1.0 ではまず EPUB 読書コアを優先し、他形式は後続マイルストーンで実装します。

## プライバシー

アカウントは不要です。ライブラリ、設定、読書データは端末内に保存し、元の電子書籍ファイルを変更しません。通常の読書はオフラインで動作し、現在の v0.1.0 でネットワークを使うのはユーザーが「更新を確認」を押した場合の GitHub アクセスだけです。

## ビルド

```powershell
dotnet restore PageArc.slnx
dotnet build PageArc.slnx -c Debug -p:Platform=x64
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Debug -p:Platform=x64
```

## License

MIT.
