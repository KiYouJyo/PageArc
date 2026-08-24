# Microsoft Store 发布边界与检查清单

这份清单用于把 PageArc 的本地 Store 包交给 Microsoft Partner Center。它不代表已提交、已认证或已上架。

## 固定身份

Store 包必须使用 `Package.Store.appxmanifest` 中的身份：

- Name：`JoKiy.PageArc`
- Publisher：`CN=C4E4B33A-7B77-4121-897C-7D720A5471F8`
- PublisherDisplayName：`Jo Kiyō`
- 当前显示版本：`1.1.1`
- 当前 Store 包身份版本：`2026.824.460.0`（必须高于当前已发布版本）

GitHub 侧载包继续使用独立的包身份和更新通道，不得把侧载包上传到 Store。

## 本地生成与验证

```powershell
pwsh -File .\Packaging\Build-StorePackage.ps1 -Configuration Release -Platform x64 -OutputDirectory artifacts\store-package-v1.1-release
```

脚本会验证最终 MSIX 内嵌 manifest，并在 `artifacts/store-package-v1.1-release/store-package-validation.json` 写出身份证据。提交前还应运行：

```powershell
dotnet test tests/PageArc.Tests/PageArc.Tests.csproj -c Release -p:Platform=x64 --no-restore --nologo
git diff --check
```

## Partner Center 提交前

1. Store listing 应填写公开应用主页：`https://kiyoujyo.github.io/PageArc/`。
2. 隐私政策：`https://kiyoujyo.github.io/PageArc/privacy/`。
3. 支持页：`https://kiyoujyo.github.io/PageArc/support/`。
4. 上传脚本生成的 `.msixupload`，确认包身份、版本和架构与 listing 一致。
5. 将 Store 的认证、提交和上架状态记录在 Partner Center；GitHub Release 状态单独记录。

Store 认证、提交、发布和公开可用是独立阶段。本仓库不保存 Partner Center 凭据，也不在构建脚本中自动提交。
