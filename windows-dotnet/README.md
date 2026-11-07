# ANKER Windows · .NET 定制版

本目录是基于 [qzz0518/Charker v0.5.1](https://github.com/qzz0518/Charker/tree/v0.5.1) 的社区 Windows 移植版，当前版本 **0.5.1-win.19**。它与同仓库保留的上游 `charker-go/`（Go 版）是两个实现，版本号和更新渠道互不混用。本项目与 Anker 无关联，未获其背书。

## 下载和系统要求

在本仓库 [Windows Releases](https://github.com/891467598/Charker/releases/tag/windows-v0.5.1-win.19) 下载 `ANKER-0.5.1-win.19-Setup-x64.exe`，或下载便携 ZIP 后完整解压。

- Windows 10 2004 / build 19041 及以上，或 Windows 11，**x64**。
- 160W 需要可用的低功耗蓝牙适配器；250W 需要网络和已绑定设备的 Anker 账号。
- Setup 包含 .NET、Node.js 和 WebView2 离线运行库。安装默认仅针对当前用户；软件尚未附加商业代码签名。
- 更新前从系统托盘退出旧版。便携版必须保留整个目录；数据仍在 `%LOCALAPPDATA%\Charker.Windows`，卸载不会清除用户数据。

## 连接和使用

**Prime 160W / A2687**：先退出占用连接的手机 Anker App，再扫描连接；可保存、重命名和移除设备。多个已保存的 160W 可切换使用，同一时刻连接一台 160W。

**Prime 250W / A2345**：先在官方手机 App 绑定并配置 Wi-Fi，再登录同一账号。可与一台 160W 同时监测。

功能包括端口功率、电压、电流、每端口独立颜色曲线、最近五分钟历史、3D 接线动画、能耗与电费、可调读数刷新间隔，以及可拖动/锁定/置顶的桌面组件。组件显示最近一分钟曲线，并与 App 共用纵轴范围。

应用设置可开启 Windows 登录自启动，启动后仅在托盘运行，组件按原开关显示。多份同版本/不同版本共用一个启动项，开启状态下手动运行新副本会更新其启动路径。

**250W 的断线恢复**：仅对已成功连接后发生的意外断线，在第 2、4、6、8、10 秒尝试重连，每次最多两秒；第五次最晚第十二秒结束。成功即停止，五次失败后手动连接。手动断开、退出账号、切换云端设备或退出程序会取消重连；首次启动失败不连续重试。160W 不使用这项云端重试策略。

端口开关、倒计时与定时，以及实验性控制受两组独立权限控制；未连接时不可操作。Apple 家庭/Matter 功能保留，但其完整实机配对和部分设备控制仍待验证。

## 曲线与低负载

后台持续缓存最近五分钟，不依赖当前页面或主窗口可见性。普通每端口 ≤0.2W 待机及未确认短脉冲不绘图。160W 的 0.2W～0.7W 低负载需要持续超过五秒确认；充电攀升即时确认并回填起点。已确认负载三秒内完成恢复的握手低点保留真实数值。该规则用于显示，不能百分之百判断物理接入；原始数字和能耗不因过滤而改写。

## 从源码构建

安装 .NET SDK **8.0.426**（`global.json`）、Node.js **24.19.0** 和 npm，然后在 Windows PowerShell 7 中运行：

```powershell
git clone https://github.com/891467598/Charker.git
cd Charker/windows-dotnet
./build.ps1
./dist/ANKER.exe
```

脚本使用 NuGet/npm lockfile，生成 x64 自包含程序并运行隔离自测。无需上传或配置 Anker 账号。运行库和安装器均不提交进 Git。

```powershell
node ./Tests/realtime-view.cjs
./Tests/run-native.ps1 -ProgramDirectory ./dist -Tls
# 交互式桌面上可额外验证界面：
./Tests/run-native.ps1 -ProgramDirectory ./dist -Ui
```

`--self-test`、`--ui-test` 与 `--startup-test` 使用测试模式，不修改真实 Windows 自启动项。实际设备和网络切换效果需要硬件测试，自动测试不能替代它。

离线安装器构建方法见 [Installer/README.md](Installer/README.md)。GitHub Actions 自动构建并提供程序、Setup 和校验和工件；人工发布版通过 Releases 提供。

## 隐私、许可和反馈

密码/验证码不保存，云端令牌通过 Windows DPAPI 保存在当前用户的数据目录中。250W 与 Anker 云端通信；160W 使用本机蓝牙，Matter 使用本地网络。不要上传用户数据目录、未遮挡的设备标识或原始报文。

源代码沿用 [MIT](LICENSE) 和上游版权声明。第三方商标、产品图片、模型、图标和运行库适用各自的权利与许可，见 [WINDOWS-NOTICES.md](WINDOWS-NOTICES.md)、[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) 与 `licenses/`。小熊图标来自用户提供的参考图，经图像工具提取；不对该图案主张独立版权或额外授权。

反馈请使用本仓库的 Windows Issue 模板，遵循 [贡献说明](../CONTRIBUTING.md) 和 [安全政策](../SECURITY.md)。版本历史见 [CHANGELOG-Windows.md](CHANGELOG-Windows.md)。
