namespace Charker;
internal static class WidgetLoad {
 public static bool Active(DeviceState state,ChartSample[] samples){if(!state.Connected||state.Stale||state.Updated==null||samples.Length==0||samples[^1].At!=state.Updated.Value.ToUnixTimeMilliseconds())return false;
  // Cloud curves retain their existing policy; card visibility independently
  // rejects low transient pulses using the same five-second confirmation rule.
  if(!state.Ports.Any(p=>p.On!=false&&p.Power>PowerCurve.Minimum))return false;
  var latest=state.Model=="A2345"?CurveSignalFilter.Apply("A2687",samples.Select(s=>new ChartSample(s.At,s.Values)).ToArray())[^1]:samples[^1];
  return state.Ports.Select((p,i)=>p.On!=false&&p.Power>PowerCurve.Minimum&&(latest.PlotValues??latest.Values)[i]>PowerCurve.Minimum).Any(v=>v);
 }
}
