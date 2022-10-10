

# MCLauncher
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

## 开发
懒得翻译中文

## Compiling the launcher yourself
You'll need Visual Studio with Windows 10 SDK version 10.0.17763 and .NET Framework 4.6.1 SDK installed. You can find these in the Visual Studio Installer if you don't have them out of the box.
The project should build out of the box with VS as long as you haven't done anything bizarre.

## Frequently Asked Questions
**Does this allow running multiple instances of Minecraft: Bedrock at the same time?**

At the time of writing, no. It allows you to _install_ multiple versions, but only one version can run at a time.
