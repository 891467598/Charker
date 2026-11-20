namespace Charker;

// A bounded recovery round for initial failures and established connection drops.
internal sealed class CloudReconnect {
 internal const int MaximumAttempts=9;
 internal record Attempt(string Id,int Number,int Epoch,DateTimeOffset Deadline);
 readonly object gate=new();string? armed,pending;DateTimeOffset dropped;int count,epoch;bool busy;
 public void Connected(string id){lock(gate){epoch++;armed=id;pending=null;count=0;busy=false;}}
 public bool Dropped(DateTimeOffset now){lock(gate){if(armed==null)return false;pending=armed;armed=null;dropped=now;count=0;busy=false;epoch++;return true;}}
 public bool Failed(string id,DateTimeOffset now){lock(gate){if(pending!=null)return false;armed=null;pending=id;dropped=now;count=0;busy=false;epoch++;return true;}}
 public bool Expire(DateTimeOffset now){lock(gate){if(pending==null||busy||now<dropped.AddSeconds(20))return false;pending=null;return true;}}
 public Attempt? Take(DateTimeOffset now){lock(gate){if(pending==null||busy||count>=MaximumAttempts||now<dropped.AddSeconds((count+1)*2))return null;if(now>=dropped.AddSeconds(20)){pending=null;return null;}count=Math.Min(MaximumAttempts,(int)((now-dropped).TotalSeconds/2));busy=true;return new(pending,count,epoch,dropped.AddSeconds((count+1)*2));}}
 public bool IsCurrent(Attempt attempt){lock(gate)return pending==attempt.Id&&epoch==attempt.Epoch;}
 public void Finished(Attempt attempt){lock(gate){if(epoch!=attempt.Epoch)return;busy=false;if(count>=MaximumAttempts)pending=null;}}
 public void Cancel(){lock(gate){epoch++;armed=pending=null;busy=false;}}
}
