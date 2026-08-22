# PageArc

[简体中文](README.md) · [日本語](README.ja.md) · [English](README.en.md)

![Version](https://img.shields.io/badge/version-0.9.5-005fb8)
![Windows](https://img.shields.io/badge/Windows-11-0078D4?logo=windows11)
![WinUI 3](https://img.shields.io/badge/WinUI-3-005FB8)
![Languages](https://img.shields.io/badge/UI-中文%20%7C%20日本語%20%7C%20English-6A5ACD)
![Offline First](https://img.shields.io/badge/offline-first-2E7D32)
![License](https://img.shields.io/badge/license-MIT-blue)

**PageArc** は、リフロー型電子書籍に特化した WinUI 3 / Windows App SDK 製 Windows リーダーです。PAGEARC Figma を UI の基準とし、ローカル優先・元ファイル非変更の読書体験を重視します。

## v0.9.5

v0.9.5 では、v0.9.3 のマルチタブ Reader を土台に、データ復元、内蔵変換ランタイム、本文互換性と読書操作を補完します。

- 読書データのバックアップを schema v2 に更新し、「マージ / 置換」で復元可能。端末や保存場所が変わっても、PageArc ID、内容フィンガープリント、固有の書誌情報から進捗・しおり・ノートを再照合します。
- 公式 x64 パッケージに固定版 calibre 9.13.0 のローカル変換ランタイムを同梱し、EPUB / FB2 / MOBI / AZW3 / LIT の方向付き 20 通りの変換を追加インストールなしで利用できます。外部 calibre は開発・互換用フォールバックとしてのみ残します。
- リフロー本文に中国語/日本語の厳格な改行、Ruby / ルビ、縦書き writing-mode、MathML / SVG の応答表示、幅広い表の横スクロール互換を追加します。完全な Fixed-layout EPUB エンジンを意味するものではありません。
- Home / Reader タブの順序、識別子、選択中タブを保存し、再起動後に有効な Reader セッションを自動復元します。
- 同一文書内の注記リンクは軽量な脚注ポップオーバーで表示し、元の注記位置へ移動する操作も残します。
- 本文画像をクリックすると Reader 内画像ビューアを開き、ズーム、パン、ウィンドウに合わせる、100%、安全な保存を利用できます。
- EPUB 2/3 と FB2 の内蔵解析、MOBI / KF8 / AZW3 の固定ローカル解析、LIT の専用 Flow Adapter、完成済みライブラリ、目次 / 検索 / しおり / ノート、Windows ファイル関連付け、単一インスタンス、`pagearc:`、Jump List は引き続き維持します。

**元ファイル保護:** 読書キャッシュ、表紙キャッシュ、解析ワークスペース、変換結果はコピーまたは新規ファイルとして扱います。PageArc から本を削除しても元の電子書籍ファイルは削除しません。DRM 解除は対象外です。

## リリース

正式版と署名済みインストールパッケージは [GitHub Releases](https://github.com/KiYouJyo/PageArc/releases) で公開します。アプリ内の更新確認も GitHub Releases を更新元として使用します。

## デザイン基準

表示 UI を追加・変更する前に、対応する PAGEARC Figma ノードを確認します。WinUI 3 のネイティブコントロール、Mica / Fluent の挙動、Windows システムアイコンを優先しつつ、承認済み Figma の階層と密度を維持します。

## プライバシー

アカウントは不要です。ライブラリ、設定、進捗、しおり、ノート、タブセッションは端末内に保存します。通常の読書、解析、電子書籍変換はローカルで動作します。公式インストール版は変換のためにネットワークを必要とせず、実行時のネットワーク利用はユーザーが明示的に行う更新確認に限定します。固定版第三者ランタイムのダウンロードはインストーラ生成時のみです。

## ビルド

```powershell
dotnet restore PageArc.slnx
dotnet build PageArc.slnx -c Debug -p:Platform=x64
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Debug -p:Platform=x64
```

詳しくは [docs/ROADMAP.md](docs/ROADMAP.md)、[docs/V095_FEATURES.md](docs/V095_FEATURES.md)、[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)、[docs/ENGINE_ARCHITECTURE.md](docs/ENGINE_ARCHITECTURE.md)、[docs/WINDOWS_INTEGRATION.md](docs/WINDOWS_INTEGRATION.md)、[docs/FORMAT_SUPPORT.md](docs/FORMAT_SUPPORT.md)、[docs/TABBED_SHELL_0.9.3.md](docs/TABBED_SHELL_0.9.3.md)、[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)、[PRIVACY.md](PRIVACY.md)、[CONTRIBUTING.md](CONTRIBUTING.md)、[CHANGELOG.md](CHANGELOG.md) を参照してください。

## License

PageArc 本体は MIT License です。同梱する第三者コンポーネントは各ライセンスと出典を保持し、公式 x64 パッケージの calibre ランタイムは GPLv3 のままです。詳細は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
