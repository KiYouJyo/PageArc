# Contributing to PageArc

感谢你参与 PageArc。

## 分支与提交

- 从 `main` 创建功能分支，不直接在 `main` 开发。
- 推荐 Conventional Commits，例如 `feat: add epub parser`、`fix: preserve reading position`。
- 一个 PR 聚焦一个可验收目标。

## 本地验证

```powershell
dotnet restore PageArc.slnx
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Debug -p:Platform=x64
dotnet build PageArc.slnx -c Debug -p:Platform=x64
dotnet build PageArc.slnx -c Release -p:Platform=x64
```

## Figma 与 UI

`PAGEARC` Figma 文件是 UI 的设计基准。实现时优先复用 WinUI 3 原生控件和 Fluent 语义；不要用 Unicode 文本字符伪造导航图标，优先使用 `SymbolIcon`，必要时才使用 `FontIcon` + `SymbolThemeFontFamily`。

## 本地化

界面资源位于：

- `Strings/zh-CN/Resources.resw`
- `Strings/ja-JP/Resources.resw`
- `Strings/en-US/Resources.resw`

三份资源必须保持相同 key 集合。新增可见 UI 文案时必须同步补齐三语。

## 数据与隐私

- 不修改用户原始电子书。
- 不提交证书、Token、用户本机绝对路径、缓存、`bin/`、`obj/`。
- 新增网络行为必须在 `PRIVACY.md` 中说明。
