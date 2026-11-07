using Microsoft.Win32;
namespace Charker;

// Current-user Windows sign-in entry. Test modes never modify the real Run key.
internal sealed class WindowsStartup(bool isolated=false) {
 const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
 internal const string ValueName="ANKER.Windows";
 string? simulatedCommand;
 public string? Command {get {if(isolated)return simulatedCommand;using var key=Registry.CurrentUser.OpenSubKey(RunKey);return key?.GetValue(ValueName) as string;}}
 public bool Enabled=>IsOurCommand(Command);
 internal static bool IsOurCommand(string? command)=>command is {} c&&c.StartsWith('"')&&c.EndsWith("\\ANKER.exe\" --background",StringComparison.OrdinalIgnoreCase);
 internal static string BuildCommand(string executable){if(string.IsNullOrWhiteSpace(executable)||executable.Contains('"'))throw new ArgumentException("自启动程序路径无效");return "\""+executable+"\" --background";}
 public void Set(bool enabled,string? executable=null){
  string? command=enabled?BuildCommand(executable??Environment.ProcessPath??throw new IOException("无法确定程序路径")):null;
  if(isolated){simulatedCommand=command;return;}
  using var key=Registry.CurrentUser.CreateSubKey(RunKey,true);
  if(enabled)key.SetValue(ValueName,command!,RegistryValueKind.String);else key.DeleteValue(ValueName,false);
 }
}
