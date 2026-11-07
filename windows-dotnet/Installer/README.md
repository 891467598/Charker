# ANKER Windows x64 离线安装包

win.18 的设置中可启用 Windows 登录自启动。当前用户 Run 项为 `ANKER.Windows`，命令为带双引号的 ANKER.exe 绝对路径和 `--background` 参数。安装更新时，只有已经启用过的本应用启动项会迁移到安装路径；默认不会新建自启动。卸载仅在启动项仍精确指向本安装目录时删除它，避免影响另一个便携副本。

目标：Windows 10 2004（内部版本 19041）或更新的 x64 系统，以及 Windows 11 x64。不是 x86/32 位安装包。

`ANKER.iss` 使用 Inno Setup 6.5 或以上。安装仅针对当前用户，默认路径 `%LOCALAPPDATA%\Programs\ANKER`，可选择创建桌面快捷方式，并注册 Windows 应用卸载入口。App 与组件本身没有安装版专用逻辑。

程序目录需要先使用项目 build.ps1 生成，包含 .NET 8 自包含运行时、Node.js、Bridge/npm 依赖、UI 资源和许可。另外从微软官方获取 Evergreen Standalone **x64** 安装器，保留文件名 `MicrosoftEdgeWebView2RuntimeInstallerX64.exe`：

- 下载：https://go.microsoft.com/fwlink/?linkid=2124701
- 官方离线部署说明：https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution
- 安装编译器：https://jrsoftware.org/isdl.php
- 中文语言文件来自官方仓库：https://raw.githubusercontent.com/jrsoftware/issrc/refs/heads/main/Files/Languages/ChineseSimplified.isl

编译示例（PowerShell）：

```powershell
./Installer/build-setup.ps1 -Iscc 'C:/Tools/Inno/ISCC.exe' -PayloadDir 'C:/Build/ANKER/程序' -PrerequisiteDir 'C:/Build/Prerequisites' -OutputDir 'C:/Build/安装包'
```

setup 单文件内置全部程序和微软离线运行库，不需在目标电脑预先安装 .NET、Node.js 或开发工具；有 WebView2 时跳过其安装。安装本身无需联网，250W 云端通信和登录仍需联网，160W 仍需可用的蓝牙。

更新前从托盘退出旧版。安装器不会强制结束程序或修改旧便携文件夹。账号与偏好继续位于原 `%LOCALAPPDATA%\Charker.Windows` 目录，卸载保留用户数据；拷贝到另一台电脑后需要重新登录并保存设备，DPAPI 令牌不跨电脑迁移。WebView2 是共享微软组件，卸载 ANKER 不移除它。

`convert-icon.ps1` 只调整 ICO 的取样边距与七档尺寸，不改软件内 PNG 图案。setup 和主程序未附加商业数字签名。
