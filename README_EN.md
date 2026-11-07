# ANKER Windows · Charker .NET

A community Windows companion for Anker Prime 160W (A2687, Bluetooth) and 250W (A2345, cloud). This fork maintains the **C#/.NET 8 + WPF + WebView2 implementation** in [`windows-dotnet/`](windows-dotnet/README.md), based on [qzz0518/Charker v0.5.1](https://github.com/qzz0518/Charker/tree/v0.5.1). It is not affiliated with or endorsed by Anker.

[中文](README.md) · [Windows downloads](https://github.com/891467598/Charker/releases/tag/windows-v0.5.1-win.19) · [Build status](https://github.com/891467598/Charker/actions/workflows/windows-dotnet.yml)

## Install

Download the offline Setup or the portable ZIP from this fork's Releases. Requires **Windows 10 build 19041+ or Windows 11, x64**. Setup includes .NET, Node.js and the offline WebView2 installer. Exit the previous version from its tray menu before upgrading. Account credentials and settings remain local to the Windows user; another PC needs its own login.

## Features

- One Bluetooth 160W and one cloud 250W charger monitored simultaneously.
- Per-port watts/volts/amps, colored five-minute charts, energy/cost tracking and animated 3D cable views.
- Persistent desktop widget, tray operation, optional background startup at Windows sign-in, configurable value refresh intervals, and saved-device management.
- After an established 250W session drops unexpectedly, reconnect at seconds 2/4/6/8/10, with up to two seconds for each attempt. Stop on success; the last attempt finishes by second 12. Manual disconnect/logout cancels the retry round. The 160W policy is unchanged.
- Device controls and experimental features are separately gated. Full Apple Home/Matter pairing and some device controls still require hardware validation.

## Build

On Windows, install .NET SDK **8.0.426**, Node.js **24.19.0** and npm:

```powershell
git clone https://github.com/891467598/Charker.git
cd Charker/windows-dotnet
./build.ps1
node ./Tests/realtime-view.cjs
./Tests/run-native.ps1 -ProgramDirectory ./dist -Tls
```

The build uses NuGet/npm lockfiles and produces a self-contained x64 directory. CI builds and tests this implementation and exposes installer/portable artifacts. Original upstream macOS, Rust and Go sources are retained; they use separate build and release channels. See [the archived upstream README](docs/UPSTREAM_README.en.md) for their original instructions.

## Contribute and license

Read [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md). Do not upload account identifiers, device serial/MAC values, tokens, raw captures or personal data directories.

Code retains upstream attribution under [MIT](LICENSE). Product images, models, trademarks, the user-supplied bear icon and third-party runtimes are subject to their own rights and notices, not automatically MIT. See [Windows notices](windows-dotnet/WINDOWS-NOTICES.md) and [upstream notices](THIRD-PARTY-NOTICES.md).
