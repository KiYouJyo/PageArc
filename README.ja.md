# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

**PageArc** は、リフロー型電子書籍に特化した WinUI 3 製 Windows リーダーです。PAGEARC Figma を UI の基準とし、ローカル優先・元ファイル非変更の読書体験を重視します。

## v0.4.0

v0.4.0 では、最初のフォーマットエンジン段階を完了しました。

- EPUB 2 / EPUB 3 と FB2 は内蔵アダプターで解析
- MOBI / KF8・AZW3 は固定バージョンを同梱したローカル解析ランタイムを使用し、CDN には依存しません
- LIT は専用 `LitFlowAdapter` から共通 `FlowReaderEngine` に接続し、対応するローカル変換 Provider がある場合は読み取り専用 EPUB 正規化キャッシュを利用
- 目次、連続スクロール / ページ送り、章内位置を含む進捗、全文検索、ブックマーク、注釈データ、Figma に沿った検索 / ブックマーク / ノートのサイドペインを共通化
- MOBI / AZW3 は解析前に PalmDOC の暗号化情報を確認し、DRM が確認された場合は処理を停止して解除を試みません
- EPUB / FB2 / MOBI / AZW3 / LIT の 20 通りの方向付き相互変換能力を明示的にモデル化し、実際に端末上で利用可能な Provider に基づいて実行可否を判定
- calibre が既にインストールされている場合、`ebook-convert` を任意のローカル変換・互換性 Provider として利用可能。calibre 自体は PageArc に同梱しません

**元ファイル保護:** 読書キャッシュ、Kindle 解析ワークスペース、変換結果はすべてコピーまたは新規ファイルとして扱い、元の電子書籍を変更しません。DRM 解除は対象外です。

## 次の段階

- **v0.5:** ライブラリ完成度向上 — 一括インポート、詳細メタデータと表紙、検索 / 並べ替え / フィルター、コレクション、詳細表示、大規模ライブラリ性能。
- **v0.6:** Windows 深度統合 — ファイル関連付け、アクティベーション / 単一インスタンス、Explorer 連携、ジャンプリスト、ネイティブな「開く」体験。

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

詳しくは [docs/ROADMAP.md](docs/ROADMAP.md)、[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)、[docs/ENGINE_ARCHITECTURE.md](docs/ENGINE_ARCHITECTURE.md)、[docs/FORMAT_SUPPORT.md](docs/FORMAT_SUPPORT.md)、[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)、[PRIVACY.md](PRIVACY.md) を参照してください。

## License

PageArc 本体は MIT License です。同梱する解析コンポーネントは各ライセンスと出典を保持します。詳細は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
