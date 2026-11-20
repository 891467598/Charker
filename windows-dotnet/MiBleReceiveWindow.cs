namespace Charker;

// Called only AFTER AES-CCM authentication. Notifications and replies can arrive
// out of counter order. Keep a bounded replay window instead of treating every
// counter below the high-water mark as a broken connection.
internal sealed class MiBleReceiveWindow {
 public const int Capacity=128;
 readonly Dictionary<int,byte[]> seen=[];
 public int Highest {get;private set;}=-1;
 public int Count=>seen.Count;
 internal enum Result {Fresh,Late,Duplicate,TooOld}
 public Result Accept(int counter,ReadOnlySpan<byte> authenticatedCipher){
  if(counter is <0 or >65535)throw new CryptographicException("蓝牙接收计数超出范围，请重新连接");
  if(counter<=Highest-Capacity)return Result.TooOld;
  var fingerprint=SHA256.HashData(authenticatedCipher);
  if(seen.TryGetValue(counter,out var prior)){
   if(!CryptographicOperations.FixedTimeEquals(prior,fingerprint))throw new CryptographicException("同一蓝牙接收计数的内容发生变化，请重新连接");
   return Result.Duplicate;
  }
  var result=counter>Highest?Result.Fresh:Result.Late;
  seen.Add(counter,fingerprint);
  if(counter>Highest){Highest=counter;foreach(var old in seen.Keys.Where(k=>k<=Highest-Capacity).ToArray())seen.Remove(old);}
  return result;
 }
}
