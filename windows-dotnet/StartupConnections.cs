namespace Charker;
// One startup attempt for the most recently saved device of each supported model.
// A failed attempt is consumed; manual connections never pass through this policy.
internal sealed class StartupConnections {
 int started;
 public async Task Run(IEnumerable<SavedDevice> saved,Func<SavedDevice,Task> connect,Action<SavedDevice,Exception> failed){
  if(Interlocked.Exchange(ref started,1)!=0)return;
  var targets=saved.Where(d=>d.Model is "A2687" or "A2345" or "AD1204U").GroupBy(d=>d.Model).Select(g=>g.Last()).ToArray();
  await Task.WhenAll(targets.Select(async d=>{try{await connect(d);}catch(Exception e){failed(d,e);}}));
 }
}
