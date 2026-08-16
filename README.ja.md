# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

**PageArc** は、リフロー型電子書籍に特化した WinUI 3 製 Windows リーダーです。Figma を UI の基準とし、ローカル優先・元ファイル非変更の読書体験を重視します。

## v0.1.0

PageArc v0.1.0 は最初の公開リリースです。

- PAGEARC Figma に沿った WinUI 3 / Windows App SDK の Library、Categories、Reader、Format Conversion、Import Folders、Settings、About UI
- 画面幅に適応する NavigationView、Windows ネイティブの Fluent アイコン、ウィンドウのアクティブ状態に応じたシアン系 / ニュートラルグレーのナビゲーション配色
- 简体中文 / 日本語 / English の三言語リソース、システム追従、ウィンドウを再生成しないアプリ内言語切替
- ローカルのライブラリ、分類、お気に入りフィルター、読書進捗、読書設定の保存
- EPUB 2 / EPUB 3 の metadata、OPF、spine、nav / NCX 解析と安全なキャッシュ
- WinUI ネイティブ本文表示、目次移動、前後章、進捗、文字サイズ、行間、読書テーマ
- About ページからの GitHub Release 更新確認
- CI、テスト、署名済み MSIX 検証、プライバシー / コントリビューション / アーキテクチャ文書

**形式の状況:** v0.1.0 の安定した読書経路は **EPUB** です。ライブラリは FB2 / MOBI / AZW3 / LIT を認識しますが、これらの読書アダプターは後続バージョンで実装予定です。形式変換ページは UI とタスクフローのみで、変換エンジンは v0.1.0 には含まれません。

## プライバシー

アカウントは不要です。ライブラリ、設定、読書データは端末内に保存し、元の電子書籍ファイルを変更しません。通常の読書はオフラインで動作し、ネットワークを使うのはユーザーが「更新を確認」を実行した場合の GitHub Release API へのアクセスだけです。

## ビルド

```powershell
dotnet restore PageArc.slnx
dotnet build PageArc.slnx -c Debug -p:Platform=x64
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Debug -p:Platform=x64
```

## License

MIT.
