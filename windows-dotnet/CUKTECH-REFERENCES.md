# 10 Ultra 参考与实现范围

本地核对日期：2026-11-08。机型：AD1204U，米家型号 njcuk.fitting.ad1204，广播产品ID 0x660e。

| 项目 | 本次用途 | 检查的提交 |
| --- | --- | --- |
| [zuyan9/ha-cuk-ble](https://github.com/zuyan9/ha-cuk-ble) | 主参考，具备独立的认证、会话、属性解析、抓包文档和模拟测试；相关部分以 Apache-2.0 改写为 C# | 64903f148c483e83803aa02c560048c2e7fd1d06 |
| [Dream-Architect1026/cuktech-ble-server](https://github.com/Dream-Architect1026/cuktech-ble-server) | 用户指定参考；核对Windows可行性、属性名称和设备行为，MIT | cb861f9b89b771278c7b5cf22730fd1e4cab1eb4 |
| [kairui1108/cuktech-ble-server](https://github.com/kairui1108/cuktech-ble-server) | 用户指定参考；对照功能与蓝牙实现，所检查提交无LICENSE文件，因此未复制代码 | 8efa6ca4faf4b05b758217f368b5dcf35ba7bb14 |

Windows运行不依赖上述服务、Python、MQTT或Home Assistant。蓝牙使用WinRT；认证使用.NET HKDF/HMAC/AES-CCM；小米云仅用于用户发起的二维码导入；Token使用Windows DPAPI独立保存。

完整支持的本版路径：连接/扫描/保存身份/重命名/移除、四口功率电压电流与协议显示、五分钟曲线、60秒组件、能耗、读数刷新选项、模式和屏幕设置、端口开关。新增模拟登录、加密、分片、主动上报交错、二维码流程、凭据保护、UI与组件显隐测试。

win22已在用户固件2.1.2_0073上完成真实蓝牙认证、50/114字节快照及20秒连续新采样验证；扫码导入由用户确认完成。控制写入及家庭桥接配对仍未做实机验证。倒计时和每周定时写入未开放；不适用的安克分配/图片上传功能不显示在酷态科控制页。3D为本次原创参数化示意，非官方精确产品网格。

官网形状参考：[CUKTECH 10 Ultra Power Charger](https://cuktech.com/products/cuktech-10-ultra-power-charger)。未分发官网照片。

许可证、来源及改写说明见 licenses/cuktech-NOTICE.txt、licenses/cuktech-Apache-2.0.txt、licenses/Xiaomi-cloud-tokens-extractor-LICENSE.txt。
