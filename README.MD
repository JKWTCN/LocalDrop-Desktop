# LocalDrop

LocalDrop是一个基于Windows App SDK的利用WiFi Direct技术的文件传输工具。

## 功能特性

- 无需在同一局域网就可以进行文件传输
- 文件快速传输
- 支持文本消息发送
- 系统托盘集成
- 现代化的WinUI 3界面
- 可以和安卓进行文件传输，安卓设备需安装[LocalDrop安卓端](https://github.com/JKWTCN/LocalDrop)

## 系统要求

- Windows 10 版本 17763.0 或更高版本
- .NET 8.0
- Windows App SDK 1.7.x

## 开发环境

- Visual Studio 2022
- Windows App SDK
- .NET 8.0

## 构建项目

1. 克隆仓库
2. 使用Visual Studio 2022打开`LocalDrop.sln`
3. 还原NuGet包
4. 编译并运行项目

## 使用的主要技术

- Windows App SDK / WinUI 3
- C# / .NET 8.0
- TCP/IP 网络通信
- WiFiDirect
- H.NotifyIcon.WinUI (系统托盘支持)

## 项目结构

- `/Assets` - 应用图标和资源文件
- `/Sender` - 文件发送相关实现
- `/Receiver` - 文件接收相关实现

## 贡献指南

欢迎提交 Issue 和 Pull Request 来帮助改进项目。

## 联系方式

如有问题或建议，请提交 Issue 或联系开发者。

