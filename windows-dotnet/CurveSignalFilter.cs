namespace Charker;
internal static class CurveSignalFilter {
 public const double SuspectMaximum=.7;
 public const long ConfirmationMs=5000,MaxSampleGapMs=2500;
 // This is display filtering, not physical device identification. Only timestamps
 // of distinct received samples can confirm a sustained low load.
 public static ChartSample[] Apply(string model,IReadOnlyList<ChartSample> raw){
  var result=raw.Select(s=>new ChartSample(s.At,s.Values,(double[])s.Values.Clone())).ToArray();
  int ports=raw.Count==0?0:raw[0].Values.Length;
  if(model is "A2687" or "AD1204U")for(int port=0;port<ports;port++){
   int start=-1;bool confirmed=false;long previous=long.MinValue;
   for(int i=0;i<raw.Count;i++){
    var s=raw[i];double watts=s.Values[port];if(previous!=long.MinValue&&s.At-previous>MaxSampleGapMs){start=-1;confirmed=false;}previous=s.At;
    if(watts<=PowerCurve.Minimum){result[i].PlotValues![port]=0;start=-1;confirmed=false;continue;}
    if(watts>=SuspectMaximum){if(start>=0)for(int j=start;j<i;j++)result[j].PlotValues![port]=raw[j].Values[port];start=-1;confirmed=false;continue;}
    if(start<0)start=i;
    if(!confirmed&&(raw[i].PlotValues?[port]>PowerCurve.Minimum||s.At-raw[start].At>ConfirmationMs)){confirmed=true;for(int j=start;j<i;j++)result[j].PlotValues![port]=raw[j].Values[port];}
    result[i].PlotValues![port]=confirmed?watts:0;
   }
  }
  for(int i=0;i<result.Length;i++)result[i]=result[i] with{PlotVisible=Enumerable.Range(0,ports).Select(p=>result[i].PlotValues![p]>PowerCurve.Minimum||raw[i].PlotVisible?[p]==true).ToArray()};
  for(int i=0;i<result.Length;i++)for(int port=0;port<ports;port++)if(raw[i].PlotVisible?[port]==true)result[i].PlotValues![port]=raw[i].Values[port];
  // A brief dip is provisional until actual recovery is observed. A high-power
  // session must recover above the low band; confirmed low loads may recover
  // near their previous level. Never replace readings with interpolated values.
  for(int port=0;port<ports;port++){
   int active=-1,dip=-1;double recovery=SuspectMaximum;
   for(int i=0;i<raw.Count;i++){
    if(i>0&&raw[i].At-raw[i-1].At>3000){active=-1;dip=-1;}
    if(dip>=0&&raw[i].At-raw[dip].At>3000){active=-1;dip=-1;}
    double watts=raw[i].Values[port];
    if(dip>=0&&watts>PowerCurve.Minimum&&watts>=recovery){for(int j=dip;j<=i;j++){result[j].PlotValues![port]=raw[j].Values[port];result[j].PlotVisible![port]=true;}dip=-1;active=i;continue;}
    if(result[i].PlotVisible![port]&&watts>PowerCurve.Minimum){active=i;dip=-1;continue;}
    if(active>=0&&dip<0&&!result[i].PlotVisible![port]){dip=i;recovery=Math.Min(SuspectMaximum,raw[active].Values[port]*.8);}
   }
  }
  return result;
 }
}
