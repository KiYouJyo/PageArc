<p align="center">
  <img src="docs/assets/app-icon.png" width="128" height="128" alt="PageArc">
</p>
<h1 align="center">PageArc</h1>
<p align="center">リフロー型電子書籍のためのローカル優先 Windows リーダー。</p>
<p align="center">
  <a href="https://github.com/KiYouJyo/PageArc/releases/latest"><img src="https://img.shields.io/github/v/release/KiYouJyo/PageArc?display_name=tag&amp;sort=semver" alt="GitHub Release"></a>
  <a href="https://github.com/KiYouJyo/PageArc/actions/workflows/ci.yml"><img src="https://github.com/KiYouJyo/PageArc/actions/workflows/ci.yml/badge.svg?branch=main" alt="CI"></a>
  <a href="https://github.com/KiYouJyo/PageArc"><img src="https://img.shields.io/badge/Windows-WinUI%203-0078D4?logo=windows" alt="Windows"></a>
  <a href="https://github.com/KiYouJyo/PageArc"><img src="https://img.shields.io/badge/Languages-中文%20%7C%20日本語%20%7C%20English-6F42C1" alt="Languages"></a>
  <a href="https://github.com/KiYouJyo/PageArc"><img src="https://img.shields.io/badge/Design-Local--first-2EA043" alt="Local First"></a>
  <a href="https://github.com/KiYouJyo/PageArc/blob/main/LICENSE"><img src="https://img.shields.io/badge/License-MIT-D4A72C" alt="MIT License"></a>
  <a href="https://kiyoujyo.github.io/PageArc/"><img src="https://img.shields.io/badge/Website-PageArc-0078D4" alt="Website"></a>
</p>
<p align="center">
  <a href="https://get.microsoft.com/installer/download/9NTHMP210H9B?referrer=appbadge"><img src="https://get.microsoft.com/images/ja%20dark.svg" width="240" alt="Get PageArc from Microsoft Store"></a>
</p>
<p align="center"><a href="README.md">简体中文</a> | <a href="README.ja.md">日本語</a> | <a href="README.en.md">English</a></p>

## v1.4

v1.4 では容量の大きい電子書籍変換ランタイムを基本リーダーから分離します。

- calibre 9.13.0 は PageArc MSIX に同梱しません。
- 独立した `KiYouJyo/PageArc.ConversionRuntime` から固定版 `9.13.0-pagearc.1` を配布し、変換または互換変換を必要とする MOBI / AZW3 / LIT を初めて開く時だけダウンロードします。
- Release、サイズ、SHA-256 を固定検証し、`PageArc/Runtimes` のユーザー領域へ保存します。システムに calibre がある場合はそちらを優先します。

## v1.0

v1.0 では、v0.9.5 を土台に Reader、ライブラリ、設定、更新、Windows 配布の正式版体験を統合します。

- 読書データのバックアップを schema v2 に更新し、「マージ / 置換」で復元可能。端末や保存場所が変わっても、PageArc ID、内容フィンガープリント、固有の書誌情報から進捗・しおり・ノートを再照合します。
- 公式 x64 パッケージに固定版 calibre 9.13.0 のローカル変換ランタイムを同梱し、EPUB / FB2 / MOBI / AZW3 / LIT の方向付き 20 通りの変換を追加インストールなしで利用できます。外部 calibre は開発・互換用フォールバックとしてのみ残します。
- リフロー本文に中国語/日本語の厳格な改行、Ruby / ルビ、縦書き writing-mode、MathML / SVG の応答表示、幅広い表の横スクロール互換を追加します。完全な Fixed-layout EPUB エンジンを意味するものではありません。
- Home / Reader タブの順序、識別子、選択中タブを保存し、再起動後に有効な Reader セッションを自動復元します。
- 同一文書内の注記リンクは軽量な脚注ポップオーバーで表示し、元の注記位置へ移動する操作も残します。
- 本文画像をクリックすると Reader 内画像ビューアを開き、ズーム、パン、ウィンドウに合わせる、100%、安全な保存を利用できます。
- EPUB 2/3 と FB2 の内蔵解析、MOBI / KF8 / AZW3 の固定ローカル解析、LIT の専用 Flow Adapter、完成済みライブラリ、目次 / 検索 / しおり / ノート、Windows ファイル関連付け、単一インスタンス、`pagearc:`、Jump List は引き続き維持します。

**元ファイル保護:** 読書キャッシュ、表紙キャッシュ、解析ワークスペース、変換結果はコピーまたは新規ファイルとして扱います。PageArc から本を削除しても元の電子書籍ファイルは削除しません。DRM 解除は対象外です。

## リリース

[Microsoft Store](https://apps.microsoft.com/detail/9NTHMP210H9B) からのインストールを推奨し、更新は Store が管理します。署名済みサイドロード版は [GitHub Releases](https://github.com/KiYouJyo/PageArc/releases) で配布します。各チャネルのパッケージ ID と更新元は独立しています。

## デザイン基準

表示 UI を追加・変更する前に、対応する PAGEARC Figma ノードを確認します。WinUI 3 のネイティブコントロール、Mica / Fluent の挙動、Windows システムアイコンを優先しつつ、承認済み Figma の階層と密度を維持します。

## プライバシー

アカウントは不要です。ライブラリ、設定、進捗、しおり、ノート、タブセッションは端末内に保存します。通常の読書、解析、電子書籍変換はローカルで動作します。基本インストーラには calibre を含みません。変換ランタイムが初めて必要になった時、PageArc は確認後に固定版 PageArc.ConversionRuntime をダウンロードし、それ以降の変換はローカルで実行します。更新確認とユーザーが開始した WebDAV 同期も必要時のみ通信します。

## ビルド

```powershell
dotnet restore PageArc.slnx
dotnet build PageArc.slnx -c Debug -p:Platform=x64
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Debug -p:Platform=x64
```

アプリの [ホームページ](https://kiyoujyo.github.io/PageArc/)、[公開プライバシーポリシー](https://kiyoujyo.github.io/PageArc/privacy/)、[サポートページ](https://kiyoujyo.github.io/PageArc/support/)、[Microsoft Store 公開チェックリスト](docs/STORE_PUBLISHING.md) を参照してください。技術資料は [docs/ROADMAP.md](docs/ROADMAP.md)、[docs/V095_FEATURES.md](docs/V095_FEATURES.md)、[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)、[docs/ENGINE_ARCHITECTURE.md](docs/ENGINE_ARCHITECTURE.md)、[docs/WINDOWS_INTEGRATION.md](docs/WINDOWS_INTEGRATION.md)、[docs/FORMAT_SUPPORT.md](docs/FORMAT_SUPPORT.md)、[docs/TABBED_SHELL_0.9.3.md](docs/TABBED_SHELL_0.9.3.md)、[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)、[PRIVACY.md](PRIVACY.md)、[SECURITY.md](SECURITY.md)、[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)、[CONTRIBUTING.md](CONTRIBUTING.md)、[CHANGELOG.md](CHANGELOG.md) です。

## License

PageArc 本体は MIT License です。基本パッケージには calibre を同梱しません。任意ダウンロードの PageArc.ConversionRuntime 内の calibre は GPLv3 のままで、対応するソースも各 Runtime Release に併載します。詳細は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
