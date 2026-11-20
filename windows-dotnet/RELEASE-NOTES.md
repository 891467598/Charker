Windows 10 2004+ / Windows 11 x64。安装或启动便携版前，请从系统托盘退出旧版。Setup 含离线 WebView2 运行库。

## 0.5.1-win.31

- 桌面组件从最后一次有效输出起，满 2 分钟收成一行，满 5 分钟隐藏。恢复有效输出时立即展开。
- Windows 解锁或唤醒后恢复桌面组件，并对先前连接且当前失联的蓝牙充电器进行有限重试。160W 直接打开失败时会补扫保存的地址。
- 支持酷态科 10 Ultra（AD1204U）的蓝牙监测、端口读数与 3D 模型。支持从米家导入配对信息。
- 250W 云端连接失败时以 2 秒为间隔尝试恢复，重试窗口到第 20 秒截止。支持保存、重命名和移除云端设备。
- 华为 SCP 端口按实测电压与电流计算功率。

## 0.5.1-win.31

- The desktop widget collapses a card after two minutes without confirmed output, hides it after five, and expands as soon as confirmed output resumes.
- Windows unlock or wake restores the widget and retries previously connected Bluetooth chargers when needed. A failed direct connection to a saved 160W charger triggers a targeted scan.
- Adds Bluetooth monitoring, per-port readings, and a 3D model for the CUKTECH 10 Ultra (AD1204U). Pairing information can be imported from Mi Home.
- Cloud reconnect for the 250W uses a 20-second window with attempts every two seconds. Cloud devices can be saved, renamed, and removed.
- Huawei SCP output is calculated from measured voltage and current.

Requires Windows 10 build 19041 or newer or Windows 11 (x64). Exit the previous version from the system tray before updating. For the portable ZIP, extract the entire archive before running ANKER.exe.
