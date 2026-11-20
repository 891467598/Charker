<div align="center">
  <img src="windows-dotnet/UI/assets/AppIcon.png" width="112" alt="ANKER Windows icon" />
  <h1>ANKER Windows · Charker .NET</h1>
  <p>监测 Anker Prime 160W / A2687、250W / A2345 与酷态科 10 Ultra 的社区 Windows 桌面应用</p>
  <p><a href="https://github.com/891467598/Charker/releases/tag/windows-v0.5.1-win.31">下载 Windows 安装包</a> · <a href="windows-dotnet/README.md">完整使用与构建说明</a> · <a href="README_EN.md">English</a></p>
</div>

[![Windows .NET build](https://github.com/891467598/Charker/actions/workflows/windows-dotnet.yml/badge.svg)](https://github.com/891467598/Charker/actions/workflows/windows-dotnet.yml)
[![MIT](https://img.shields.io/badge/code-MIT-blue.svg)](LICENSE)
[![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011%20x64-0078D4.svg)](windows-dotnet/README.md)

这是基于 [qzz0518/Charker](https://github.com/qzz0518/Charker) 的非官方定制 Windows 移植版，当前版本 **0.5.1-win.31**，采用 C#/.NET 8、WPF 与 WebView2。项目与 Anker 无关联，未获其背书。

## 下载

从 [本仓库 Releases](https://github.com/891467598/Charker/releases/tag/windows-v0.5.1-win.31) 下载 Setup 或便携 ZIP。Setup 内置 .NET、Node.js 和 WebView2 离线运行库，支持 Windows 10 build 19041+ / Windows 11 x64。更新前请先从托盘退出旧版。

## 功能

- 同时监测 160W 蓝牙充电器、250W 云端充电器和酷态科 10 Ultra，查看各接口功率、电压与电流。
- 最近五分钟的各端口曲线、能耗与电费，以及随接入变化的 3D 模型。
- 可长期放置的桌面组件，支持拖动、锁定、置顶与托盘后台运行。
- Windows 登录后在后台自启动、读数刷新间隔、已保存设备重命名和移除。
- 250W 云端恢复最多持续 20 秒，以 2 秒为间隔尝试。160W 直连失败会补扫保存地址。
- 设备控制、定时与实验性功能采用独立权限；Apple 家庭/Matter 功能保留，完整配对仍待实机验证。

连接和详细限制见 [Windows 使用说明](windows-dotnet/README.md)。测试结果不代表所有固件和网络环境均已验证。

## 源码布局

| 路径 | 内容 |
| --- | --- |
| `windows-dotnet/` | 本仓库维护的 Windows .NET 定制版 |
| `.github/workflows/windows-dotnet.yml` | Windows 构建、测试与安装包工件 |
| `Sources/`、`Matter/`、`charker-go/` | 保留的上游 macOS / Rust / Go 版源码 |
| `docs/UPSTREAM_README.*.md` | 带原始归属的上游说明存档 |

```powershell
git clone https://github.com/891467598/Charker.git
cd Charker/windows-dotnet
./build.ps1
```

需要 .NET SDK 8.0.426、Node.js 24.19.0 和 npm，详见 [构建说明](windows-dotnet/README.md#从源码构建)。请勿把上游 Go 版与本目录的程序、版本号及更新渠道混用。

## 贡献与许可

请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)、[SECURITY.md](SECURITY.md)，反馈时不要提交账号 ID、序列号、MAC、令牌或用户数据。

源代码保留上游作者署名，沿用 [MIT](LICENSE)。第三方图片、图标、3D 模型与商标不自动属于 MIT，许可范围见 [Windows 声明](windows-dotnet/WINDOWS-NOTICES.md) 和 [上游第三方声明](THIRD-PARTY-NOTICES.md)。感谢原作者 qzz0518 及其列出的协议研究项目。
