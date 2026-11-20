namespace Charker;

// Windows can return null for an unpaired LE address after its discovery cache
// is lost (e.g. after reboot). Refresh only the saved target before reopening it.
internal static class CuktechDeviceOpening {
 public const int MaximumDiscoveredOpens=3;
 public static async Task<T> Resolve<T>(
  Func<CancellationToken,Task<T?>> cached,
  Func<CancellationToken,Task<int>> discover,
  Func<int,CancellationToken,Task<T?>> discovered,
  CancellationToken cancellation,
  Func<TimeSpan,CancellationToken,Task>? delay=null,
  Func<T,bool>? requiresRediscovery=null,Action<T>? releaseCached=null) where T:class {
  cancellation.ThrowIfCancellationRequested();
  var device=await cached(cancellation);
  if(device!=null){
   if(requiresRediscovery?.Invoke(device)!=true)return device;
   releaseCached?.Invoke(device);
  }
  cancellation.ThrowIfCancellationRequested();
  var type=await discover(cancellation);
  delay??=Task.Delay;
  for(int i=0;i<MaximumDiscoveredOpens;i++){
   cancellation.ThrowIfCancellationRequested();
   device=await discovered(type,cancellation);
   if(device!=null)return device;
   if(i+1<MaximumDiscoveredOpens)await delay(TimeSpan.FromMilliseconds(250*(i+1)),cancellation);
  }
  throw new IOException("Windows 已发现酷态科，但暂时无法打开设备，请稍后重新连接。");
 }
}
