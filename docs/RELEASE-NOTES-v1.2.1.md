# PageArc v1.2.1

PageArc 1.2.1 是 v1.2 的界面与更新链路修复版本，继续以 SpatialViewer 当前已经验收的 WinUI 3 shell 行为作为基准。

## 导航与汉堡菜单

- 展开宽度由 240 DIP 调整为与 SpatialViewer 一致的 252 DIP；紧凑栏保持 64 DIP。
- `NavigationView` 保持原生 `PaneDisplayMode="Auto"`，不再由窗口宽度阈值强制切换 `Left / LeftCompact`。
- 移除页面层 `SizeChanged` 对 `IsPaneOpen` 的强制写入。用户手动收起汉堡菜单后，不会因为布局重新测量而立即被程序重新展开。
- 移除 `DisplayModeChanged` 中强制关闭面板的接管逻辑，让 WinUI 自己处理宽屏、紧凑和窄屏 Overlay 状态。
- Pane 打开时只重新同步透明 Mica 背景，不改变展开状态，因此保留此前已经修好的标题栏/汉堡区域同色效果。

## 内容表面

- 书库、监视文件夹、格式转换等旧 `PageArcCardStyle` 已统一到关于页/设置页相同的中性 Fluent 卡片色。
- 深浅模式继续使用 WinUI `CardBackgroundFillColorDefaultBrush` / `CardStrokeColorDefaultBrush` 自动切换，不再出现内容卡片单独泛青。

## 更新器

- GitHub 更新流程采用两阶段“下载并验证 → 重启并更新”。
- 更新包校验 SHA-256、Windows 签名和官方发布证书后才进入待安装状态。
- 安装阶段使用 `RegisterApplicationRestart` 与 `DeploymentOptions.ForceApplicationShutdown`，避免旧版延迟注册后立即重启导致仍运行旧版本的竞态。

## 版本

- 产品版本：`1.2.1`
- 程序/程序集版本：`1.2.1.0`
- GitHub MSIX：`1.2.1.0`
- Microsoft Store 包版本：`2026.903.121.0`
