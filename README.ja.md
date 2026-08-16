# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

**PageArc** は、リフロー型電子書籍に特化した WinUI 3 製 Windows リーダーです。PAGEARC Figma を UI の基準とし、ローカル優先・元ファイル非変更の読書体験を重視します。

## v0.6.0

PageArc は、フォーマットエンジン・ライブラリ・Windows 統合の最初の基盤段階を完了しました。

- EPUB 2 / EPUB 3 と FB2 は内蔵解析、MOBI / KF8 / AZW3 は固定バージョンのローカル解析ランタイム、LIT は専用 Flow Adapter とローカル変換 Provider 境界を使用
- 目次、連続スクロール / ページ送り、章内進捗、全文検索、ブックマーク、注釈を一つの Reader 契約に統合
- 実際の表紙・メタデータ、一括インポート、重複検出、監視フォルダー、カテゴリ / お気に入り、詳細表示、欠損ファイル処理、大規模ライブラリ移行を備えたライブラリ
- パッケージ版では EPUB / FB2 / MOBI / AZW / AZW3 / LIT の Windows ファイル関連付けを登録し、Explorer から直接開けます
- Windows App SDK の単一インスタンス機構により、新しいファイル / プロトコル起動を既存の PageArc ウィンドウへリダイレクト
- `pagearc:` ディープリンクと Windows Jump List から最近の本を再度開くことが可能
- EPUB / FB2 / MOBI / AZW3 / LIT の 20 通りの方向付き相互変換能力を明示的にモデル化し、端末上で実際に利用可能な Provider に基づいて実行可否を判定

**元ファイル保護:** 読書キャッシュ、表紙キャッシュ、解析ワークスペース、変換結果はコピーまたは新規ファイルとして扱います。PageArc から本を削除しても元の電子書籍ファイルは削除しません。DRM 解除は対象外です。

## デザイン基準

表示 UI を追加・変更する前に、対応する PAGEARC Figma ノードを確認します。WinUI 3 のネイティブコントロール、Mica / Fluent の挙動、Windows システムアイコンを優先しつつ、承認済み Figma の階層と密度を維持します。

## プライバシー

アカウントは不要です。ライブラリ、設定、進捗、ブックマーク、注釈は端末内に保存します。通常の読書と内蔵解析はオフラインで動作します。ネットワークを使うのはユーザーが明示的に更新確認を行う場合のみで、任意の外部変換 Provider もローカルで実行します。

## ビルド

```powershell
dotnet restore PageArc.slnx
dotnet build PageArc.slnx -c Debug -p:Platform=x64
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Debug -p:Platform=x64
```

詳しくは [docs/ROADMAP.md](docs/ROADMAP.md)、[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)、[docs/ENGINE_ARCHITECTURE.md](docs/ENGINE_ARCHITECTURE.md)、[docs/WINDOWS_INTEGRATION.md](docs/WINDOWS_INTEGRATION.md)、[docs/FORMAT_SUPPORT.md](docs/FORMAT_SUPPORT.md)、[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)、[PRIVACY.md](PRIVACY.md) を参照してください。

## License

PageArc 本体は MIT License です。同梱する解析コンポーネントは各ライセンスと出典を保持します。詳細は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
