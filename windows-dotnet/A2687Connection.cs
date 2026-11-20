namespace Charker;

internal static class A2687Connection {
 public static async Task Open(Func<CancellationToken,Task> cached,
     Func<CancellationToken,Task> rediscovered,Action release,Action<Exception> report,CancellationToken ct){
  try{
   await cached(ct);
  }catch(Exception e) when(!ct.IsCancellationRequested && (e is IOException or TimeoutException or System.Runtime.InteropServices.COMException)){
   release();
   ct.ThrowIfCancellationRequested();
   report(e);
   await rediscovered(ct);
  }
 }
}
