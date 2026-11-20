namespace Charker;

internal enum WidgetCardMode { Hidden, Expanded, Compact }

internal static class WidgetVisibility {
 public static WidgetCardMode Mode(bool connected, long? lastLoadAt, DateTimeOffset now){
  if (!connected || lastLoadAt is null) return WidgetCardMode.Hidden;
  var age = now.ToUnixTimeMilliseconds() - lastLoadAt.Value;
  if (age < 0 || age >= 300_000) return WidgetCardMode.Hidden;
  return age < 120_000 ? WidgetCardMode.Expanded : WidgetCardMode.Compact;
 }
}
