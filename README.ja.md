# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

![Version](https://img.shields.io/badge/version-0.9.3-005fb8)
![Windows](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11)
![WinUI 3](https://img.shields.io/badge/WinUI-3-005FB8)
![Languages](https://img.shields.io/badge/UI-中文%20%7C%20日本語%20%7C%20English-6A5ACD)
![Offline First](https://img.shields.io/badge/offline-first-2E7D32)
![License](https://img.shields.io/badge/license-MIT-blue)

**PageArc** は、リフロー型電子書籍に特化した WinUI 3 / Windows App SDK 製 Windows リーダーです。PAGEARC Figma を UI の基準とし、ローカル優先・元ファイル非変更の読書体験を重視します。

## v0.9.3

v0.9.3 では、すでに完成しているフォーマットエンジン、ライブラリ、Windows 統合を土台として、現段階のデスクトップ読書体験を収束させました。

- EPUB 2 / EPUB 3 と FB2 は内蔵解析、MOBI / KF8 / AZW3 は固定バージョンのローカル解析ランタイム、LIT は専用 Flow Adapter とローカル変換 Provider 境界を使用
- タイトルバーをマルチタブ化し、起動時および `+` から Home / ライブラリタブを作成可能。複数の電子書籍を独立した Reader タブで同時に開いたまま切り替え可能
- Grid/List 切り替え、実際の表紙・メタデータ、一括インポート、重複検出、監視フォルダー、カテゴリ / お気に入り、詳細表示、欠損ファイル処理、大規模ライブラリ移行を備えたライブラリ
- Reader 左側は目次 / 検索 / ブックマーク / ノートを統合し、右側 `Aa` ペインでは縦・横・折り返し読書、単一 / 奇数 / 偶数見開き、ズーム、自動サイズ、幅に合わせる、高さに合わせるを選択可能
- Reader のツールバー、左右ペイン、周辺読書領域はカスタムタイトルバーと同じ Mica を表示し、実際の文書ページのみ選択した読書テーマに従う
- テキスト選択は現在ノート優先の操作で、入力内容を自動保存し、閉じる前に保留中の内容を反映。ノート付きテキストは低彩度の落ち着いた赤で表示
- 読書進捗、全文検索、ブックマーク、ノート、読書設定、表示モードをセッション間で保持
- パッケージ版では EPUB / FB2 / MOBI / AZW / AZW3 / LIT の Windows ファイル関連付け、単一インスタンス起動、`pagearc:` ディープリンク、Jump List の最近の本に対応
- EPUB / FB2 / MOBI / AZW3 / LIT の 20 通りの方向付き相互変換能力を明示的にモデル化し、端末上で実際に利用可能な Provider に基づいて実行可否を判定

**元ファイル保護:** 読書キャッシュ、表紙キャッシュ、解析ワークスペース、変換結果はコピーまたは新規ファイルとして扱います。PageArc から本を削除しても元の電子書籍ファイルは削除しません。DRM 解除は対象外です。

## リリース

正式版と署名済みインストールパッケージは [GitHub Releases](https://github.com/KiYouJyo/PageArc/releases) で公開します。アプリ内の更新確認も GitHub Releases を更新元として使用します。

## デザイン基準

表示 UI を追加・変更する前に、対応する PAGEARC Figma ノードを確認します。WinUI 3 のネイティブコントロール、Mica / Fluent の挙動、Windows システムアイコンを優先しつつ、承認済み Figma の階層と密度を維持します。

## プライバシー

アカウントは不要です。ライブラリ、設定、進捗、ブックマーク、ノートは端末内に保存します。通常の読書と内蔵解析はオフラインで動作します。ネットワークを使うのはユーザーが明示的に更新確認を行う場合のみで、任意の外部変換 Provider もローカルで実行します。

## ビルド

```powershell
dotnet restore PageArc.slnx
dotnet build PageArc.slnx -c Debug -p:Platform=x64
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Debug -p:Platform=x64
```

詳しくは [docs/ROADMAP.md](docs/ROADMAP.md)、[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)、[docs/ENGINE_ARCHITECTURE.md](docs/ENGINE_ARCHITECTURE.md)、[docs/WINDOWS_INTEGRATION.md](docs/WINDOWS_INTEGRATION.md)、[docs/FORMAT_SUPPORT.md](docs/FORMAT_SUPPORT.md)、[docs/TABBED_SHELL_0.9.3.md](docs/TABBED_SHELL_0.9.3.md)、[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)、[PRIVACY.md](PRIVACY.md)、[CONTRIBUTING.md](CONTRIBUTING.md)、[CHANGELOG.md](CHANGELOG.md) を参照してください。

## License

PageArc 本体は MIT License です。同梱する解析コンポーネントは各ライセンスと出典を保持します。詳細は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。