namespace Charker;
public record ChartSample(long At,double[] Values,double[]? PlotValues=null,bool[]? PlotVisible=null);
// Independent of WebView rendering, selected model, widget visibility or display refresh settings.
internal sealed class PowerHistory {
 readonly object gate=new();readonly Dictionary<string,List<ChartSample>> samples=[];
 static string Key(DeviceState s)=>s.Model+":"+s.Demo+":"+(s.SerialNumber??s.Id);
 public void Record(DeviceState state,DateTimeOffset? clock=null){lock(state.Sync){if(!state.Connected||state.Stale||state.Updated is not {} at)return;lock(gate){Prune(clock??DateTimeOffset.UtcNow);var key=Key(state);var values=state.Ports.Select(p=>p.On==false?0:p.Power).ToArray();if(!samples.TryGetValue(key,out var list)){if(!values.Any(v=>v>PowerCurve.Minimum))return;samples[key]=list=[];}var stamp=at.ToUnixTimeMilliseconds();if(list.Count==0||stamp>list[^1].At){list.Add(new(stamp,values));samples[key]=CurveSignalFilter.Apply(state.Model,list).ToList();}}}}
 void Prune(DateTimeOffset now){long limit=now.AddMinutes(-5).ToUnixTimeMilliseconds();foreach(var pair in samples.ToArray()){pair.Value.RemoveAll(p=>p.At<limit);if(!pair.Value.Any(p=>p.Values.Any(v=>v>PowerCurve.Minimum)))samples.Remove(pair.Key);}}
 public ChartSample[] Read(DeviceState state,DateTimeOffset? clock=null){lock(gate){Prune(clock??DateTimeOffset.UtcNow);return samples.TryGetValue(Key(state),out var list)&&list.Any(s=>s.PlotValues?.Any(v=>v>PowerCurve.Minimum)==true)?list.SkipWhile(s=>!(s.PlotVisible?.Any(v=>v)??s.PlotValues?.Any(v=>v>PowerCurve.Minimum)??false)).Select(s=>new ChartSample(s.At,(double[])s.Values.Clone(),s.PlotValues is {} p?(double[])p.Clone():null,s.PlotVisible is {} m?(bool[])m.Clone():null)).ToArray():[];}}
}
