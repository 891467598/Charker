namespace Charker;
internal static class DevicePermissions {
 public static void Check(string action,bool connected,bool portControl,bool experimental){
  bool ports=action is "port" or "timer" or "schedule";
  bool settings=action is "setting" or "custom" or "clockSchedule" or "theme" or "cloudList" or "cloudChange" or "upload";
  if(!ports&&!settings)return;
  if(!connected)throw new IOException("请先连接充电器");
  if(ports&&!portControl)throw new InvalidOperationException("请先在设置中打开“允许端口开关、倒计时与定时”。");
  if(settings&&!experimental)throw new InvalidOperationException("请先在设置中打开“允许实验性控制”。");
 }
}
