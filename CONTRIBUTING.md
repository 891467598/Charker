# 参与 Windows .NET 版开发

本仓库维护的定制 Windows 版位于 `windows-dotnet/`。上游 macOS 与 Go 版仍保留；针对上游的问题请优先前往 [qzz0518/Charker](https://github.com/qzz0518/Charker)。

涉及设备时，请在 Issue 中说明型号、固件版本、Windows 版本、连接方式和复现步骤。不要提交账号 ID、手机号、序列号、MAC、令牌、原始报文、用户数据目录或未遮挡的个人截图。

构建命令见 [windows-dotnet/README.md](windows-dotnet/README.md)。使用功能分支提交 PR，说明行为变化和验证结果。协议、功率过滤、连接与设备控制改动需要有针对性的测试；纯文档修改不必增加行为测试。

依赖更新必须同时更新 lockfile 与许可证说明。不要提交 `bin/`、`obj/`、`node_modules/`、运行库、安装器、密钥或本地测试报告。可分发程序放在 Releases。代码保留 MIT 许可和原作者署名；第三方图片、模型不自动属于 MIT。

```powershell
cd windows-dotnet
./build.ps1
node ./Tests/realtime-view.cjs
./Tests/run-native.ps1 -ProgramDirectory ./dist -Tls
```

界面验证需要交互式 Windows 桌面、WebView2 和图形渲染环境，可添加 `-Ui`。这些测试使用隔离数据和模拟设备。真实硬件测试请在自己的设备上进行，并说明测试范围。
