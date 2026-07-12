\# CodexQuotaFloat



一个运行在 Windows 桌面上的 Codex 剩余额度悬浮窗工具，用于快速查看 Codex 的额度使用情况。



\## 项目简介



CodexQuotaFloat 可以将 Codex 的剩余额度显示在桌面悬浮窗口中，减少频繁打开 Codex 查看额度的操作。



当前版本：



```text

v1.0.0

主要功能

显示 Codex 剩余额度

Windows 桌面悬浮窗口

保持窗口置顶

自动更新额度信息

提供额度查询相关脚本

包含基础测试项目

项目结构

CodexQuotaFloat

├─ src/

│  └─ CodexQuotaFloat/          主程序源码

├─ tests/

│  └─ CodexQuotaFloat.Tests/    测试项目

├─ 转换器/                       数据转换相关功能

├─ 浏览量/                       额度数据读取相关功能

├─ 脚本/                         构建及辅助脚本

├─ CodexQuotaFloat.slnx         解决方案文件

├─ .gitignore                   Git 忽略规则

└─ README.md                    项目说明

运行环境

Windows 11

.NET

Visual Studio 或兼容的 .NET 开发环境

已安装 Codex 桌面应用

使用方法

下载或克隆本项目。

使用 Visual Studio 打开 CodexQuotaFloat.slnx。

编译并运行主程序。

程序启动后，桌面上会显示 Codex 额度悬浮窗口。



克隆仓库：



git clone https://github.com/mucy15026007141-code/CodexQuotaFloat.git

开发状态



当前项目处于早期开发阶段，已经完成第一个可用版本。



后续计划可能包括：



优化悬浮窗口界面

增加不同额度周期的显示

增加低额度提醒

增加系统托盘功能

增加开机自动启动

增加窗口透明度和主题设置

增加额度消耗历史记录

注意事项



本项目不会在仓库中保存以下敏感信息：



Codex 登录凭据

Cookie

Token

API Key

本地用户隐私数据



使用或提交代码前，请确认没有将账号凭据上传到 GitHub。



版本记录

v1.0.0

完成 Codex 剩余额度悬浮窗基础版本

建立主程序项目

建立测试项目

添加相关脚本和辅助功能

作者



Mucy



许可



当前项目为个人学习和开发项目，暂未添加开源许可证。





\## 保存后提交到 GitHub



在项目目录打开 PowerShell：



```powershell

Set-Location "C:\\Users\\muche\\Projects\\CodexQuotaFloat"

