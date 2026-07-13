# Codex 额度悬浮窗

`CodexQuotaFloat` 是 Windows 桌面悬浮工具，用于显示本机 Codex CLI 所登录 ChatGPT 账号的 5 小时和每周剩余额度。当前版本：**v1.1.2**。

本工具为非官方第三方工具，与 OpenAI 无隶属或背书关系。它使用自定义程序图标，不使用或复制 OpenAI、ChatGPT、Codex 官方商标图标。

## 系统要求

- Windows 10/11 x64、网络连接
- 已安装 Codex CLI（最低支持 `0.144.1`）
- 使用自己的 ChatGPT 账号登录 Codex CLI

## 安装与首次配置

运行 `CodexQuotaFloat-Setup-1.1.2-win-x64.exe`。安装不需要管理员权限，默认目录为 `%LocalAppData%\Programs\CodexQuotaFloat`。开始菜单快捷方式默认创建；桌面快捷方式和登录 Windows 时自动启动由安装器选项决定。

首次运行时，程序会检查本机 Codex CLI、版本、登录状态和额度接口：

1. 如果没有 CLI，点击“复制官方安装命令”并在自己打开的 PowerShell 中执行：

   ```powershell
   powershell -ExecutionPolicy ByPass -c "irm https://chatgpt.com/codex/install.ps1 | iex"
   ```

   备用命令（需已有 Node.js）：`npm install -g @openai/codex`。
2. 如果尚未登录，点击“开始登录”，在可见终端中完成 `codex login`。
3. 登录后用 `codex login status` 确认状态，再回到程序点击“重新检测”。

程序绝不在后台执行安装命令、自动登录、读取登录网页或保存密码。API Key 模式可能没有 ChatGPT 套餐的两类额度，向导会清楚提示并允许你自行重新登录。

额度窗口由 OpenAI 动态调整。若当前套餐暂时没有 5 小时限制，程序会显示“5h 不限”；限制恢复后会自动恢复百分比显示。

## 使用、更新与卸载

悬浮窗每 60 秒刷新一次，可展开查看详情和手动刷新；默认始终置顶但不抢键盘焦点，可在托盘关闭。窗口位置会在拖动结束后保存，靠近工作区边缘时吸附；显示器变化、睡眠唤醒或网络恢复后会自动修正位置并尝试重连。托盘菜单提供显示/隐藏、立即刷新、重新连接 Codex、始终置顶、重置窗口位置、配置 Codex CLI、开机启动、日志和退出。再次运行新的安装包会覆盖程序文件并保留 `%LocalAppData%\CodexQuotaFloat\settings.json`、日志、窗口位置和开机启动偏好。数据超过 5 分钟未成功刷新时会标记为过期；断网时保留上次额度，不会显示为 0。卸载会删除程序、快捷方式和本程序的启动项，但默认保留上述用户数据；如需清除，可手动删除 `%LocalAppData%\CodexQuotaFloat`。

## 隐私与安全

- 程序仅与本机 Codex App Server 通信，不上传额度或账号数据到开发者服务器。
- 不收集遥测，不保存 Codex 认证信息，不打包 CLI、`auth.json`、Token、Cookie、API Key、用户日志或用户设置。
- 日志和“复制诊断信息”只保留应用版本、Windows 版本、CLI 路径/版本、连接状态、错误类型和时间，且会脱敏。

## 未签名安装包

当前安装包未进行商业代码签名，Windows 可能显示“未知发布者”或 SmartScreen 提示。请只从可信来源取得安装包，并使用同目录 `SHA256SUMS.txt` 中的 SHA-256 校验值确认文件来源。

## 开发构建

安装 Inno Setup 6 后执行：

```powershell
.\scripts\Build-Installer.ps1
```

脚本会先运行 Release 测试，再发布自包含单文件到 `release\CodexQuotaFloat-1.2.0-win-x64\`，生成 `CodexQuotaFloat-Setup-1.2.0-win-x64.exe` 与校验值到 `dist\`。构建产物不会提交到 Git。
