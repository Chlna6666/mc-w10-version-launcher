# 只是翻译

# MCLauncher

## ⚠️ 停止维护公告

> [!IMPORTANT]
> **本仓库（Chlna6666/mc-w10-version-launcher）已停止维护。** 后续不再计划提供新功能、Bug 修复或新版 Minecraft 兼容性适配。现有代码仍会保留供历史版本使用和学习参考，但不保证兼容未来的 Minecraft Bedrock 或 Windows 更新。
>
> **如果需要 GDK 版本支持、Linux 兼容性以及后续功能更新，请使用 [Better Minecraft Bedrock Launcher（BMCBL）](https://github.com/Chlna6666/Better-Minecraft-Bedrock-Launcher/)。**

BMCBL 是一个独立开发、持续维护的 Minecraft Bedrock 启动器，采用 Rust + GPUI 构建，支持 Windows 平台的 UWP / GDK 版本管理、游戏下载与安装、资源和存档管理等功能。Linux 平台通过 Proton / Wine 运行 GDK 游戏的功能**仍处于测试阶段**，Xbox 成就、在线状态等能力尚不完整。

- **项目地址：** [Better Minecraft Bedrock Launcher](https://github.com/Chlna6666/Better-Minecraft-Bedrock-Launcher/)
- **下载与发布：** [BMCBL Releases](https://github.com/Chlna6666/Better-Minecraft-Bedrock-Launcher/releases)

BMCBL 是独立项目，并非本仓库的原地升级版本。具体功能支持情况及已知限制，请参阅 [BMCBL 项目 README](https://github.com/Chlna6666/Better-Minecraft-Bedrock-Launcher/#readme)。

感谢所有使用、关注和支持本仓库的用户！

---

此工具允许您并行安装多个版本的Minecraft:Windows 10 Edition（Bedrock）。如果您想测试测试版、发行版或其他版本，而不需要卸载和重新安装游戏，这将非常有用。

## 免责声明
这个工具不会帮助你盗版游戏；它要求你有一个微软帐户并且拥有正版，可以用来从商店下载《Minecraft》。
## 使用条件
- 连接到微软商店的微软帐户，该商店拥有**Minecraft for Windows 10**
- **管理员权限** 在您的用户帐户上（或访问具有的帐户）
- **开发者模式** 在Windows 10设置中启用应用程序安装（设置开发者模式）
- 如果你想使用测试版，你还需要 **有测试版的权限（需要在使用Xbox Insider Hub订阅Minecraft Beta）**.
- 需要安装[Microsoft Visual C++ Redistributable](https://aka.ms/vs/16/release/vc_redist.x64.exe).

## 设置
- 下载最新版[Releases](https://github.com/MCMrARM/mc-w10-version-launcher/releases) 解压使用.
- 运行 `MCLauncher.exe` 启动器.

## 自行编译
你需要安装 Visual Studio、 Windows 10 SDK 10.0.17763 和 .NET Framework SDK 4.6.1。如果你没有安装，你可以在 Visual Studio Installer 里找到它们，开箱即用。
只要你没有做任何奇怪的事情，该项目应该在 VS 中开箱即用。

## 常见问题
**这个程序能多开基岩版 MC 吗？**

截止到写这篇文档时是不行的。它允许你_安装_多个版本，但一次只能运行一个版本。
