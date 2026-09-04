# PageArc Privacy Policy / 隐私政策 / プライバシーポリシー

_Last updated / 最后更新 / 最終更新: 2026-09-04_

## 简体中文

PageArc 不要求用户注册账户，也不以应用功能为目的收集、出售或上传个人信息。

书库索引、阅读进度、阅读偏好、笔记、书签、封面与缓存保存在用户本机。PageArc 不修改原始电子书文件。用户主动执行“检查更新”时，应用会访问 GitHub 的 PageArc Release API；该请求用于判断是否存在新版本。用户主动配置并执行 WebDAV 同步时，PageArc 会使用用户提供的 HTTP/HTTPS WebDAV 地址、用户名和密码，将书库中的电子书文件与阅读进度、书签、标注和笔记打包后上传或下载；凭据保存在本机系统凭据存储中。PageArc v1.4 的基础安装包不包含 calibre。仅当用户首次使用需要转换运行时的功能并确认下载时，应用会访问 `KiYouJyo/PageArc.ConversionRuntime` 的 GitHub Release，下载固定版本运行时并进行大小与 SHA-256 校验；安装后转换在本地执行。除用户主动执行的更新检查、WebDAV 同步或转换运行时下载外，正常阅读与解析不依赖网络。

## 日本語

PageArc はアカウント登録を必要とせず、アプリ機能のために個人情報を収集・販売・アップロードしません。

ライブラリ情報、読書進捗、設定、ノート、ブックマーク、表紙、キャッシュは端末内に保存され、元の電子書籍ファイルは変更しません。ユーザーが「更新を確認」を実行した場合、PageArc は GitHub Release API にアクセスします。ユーザーが WebDAV を設定して同期を実行した場合のみ、指定された HTTP/HTTPS WebDAV URL と資格情報を使い、ライブラリ内の電子書籍ファイルと読書位置・しおり・注釈・ノートを 1 つのバックアップにまとめてアップロードまたはダウンロードします。資格情報は端末のシステム資格情報ストアに保存されます。PageArc v1.4 の基本インストーラには calibre を含みません。変換ランタイムが初めて必要となりユーザーがダウンロードを承認した場合のみ、`KiYouJyo/PageArc.ConversionRuntime` の GitHub Release から固定版ランタイムを取得し、サイズと SHA-256 を検証します。インストール後の変換はローカルで実行します。更新確認、WebDAV 同期、変換ランタイムのダウンロード以外の通常の読書・解析はネットワークを必要としません。

## English

PageArc requires no account and does not collect, sell, or upload personal information for normal app functionality.

Library metadata, reading progress, preferences, notes, bookmarks, covers, and caches are stored locally. Original ebook files are never modified. When the user explicitly chooses **Check for updates**, PageArc contacts the project's GitHub Release API to determine whether a newer version exists. When the user configures and starts WebDAV sync, PageArc uses the user-provided HTTP/HTTPS WebDAV URL and credentials to upload or download a backup bundle containing library ebook files together with reading progress, bookmarks, annotations, and notes; credentials are stored in the local system credential store. PageArc v1.4's base installer does not include calibre. Only when a conversion-dependent feature is first requested and the user approves the download does PageArc contact the `KiYouJyo/PageArc.ConversionRuntime` GitHub Release to fetch the pinned runtime; PageArc verifies its size and SHA-256 before installation. Conversion then runs locally. Apart from user-initiated update checks, WebDAV synchronization, or conversion-runtime download, normal reading and parsing do not require network access.
