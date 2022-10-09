# MCLauncher
此工具允许您并行安装多个版本的Minecraft:Windows 10 Edition（Bedrock）。如果您想测试测试版、发行版或其他版本，而不需要卸载和重新安装游戏，这将非常有用。

## Disclaimer
这个工具不会帮助你盗版游戏；它要求你有一个微软帐户，可以用来从商店下载《Minecraft》。
## Prerequisites
- 连接到微软商店的微软帐户，该商店拥有**Minecraft for Windows 10**
- **Administrator permissions** on your user account (or access to an account that has)
- **Developer mode** enabled for app installation in Windows 10 Settings
- If you want to be able to use beta versions, you'll additionally need to **subscribe to the Minecraft Beta program using Xbox Insider Hub**.
- [Microsoft Visual C++ Redistributable](https://aka.ms/vs/16/release/vc_redist.x64.exe) installed.

## Setup
- Download the latest release from the [Releases](https://github.com/MCMrARM/mc-w10-version-launcher/releases) section. Unzip it somewhere.
- Run `MCLauncher.exe` to start the launcher.

## Compiling the launcher yourself
You'll need Visual Studio with Windows 10 SDK version 10.0.17763 and .NET Framework 4.6.1 SDK installed. You can find these in the Visual Studio Installer if you don't have them out of the box.
The project should build out of the box with VS as long as you haven't done anything bizarre.

## Frequently Asked Questions
**Does this allow running multiple instances of Minecraft: Bedrock at the same time?**

At the time of writing, no. It allows you to _install_ multiple versions, but only one version can run at a time.
