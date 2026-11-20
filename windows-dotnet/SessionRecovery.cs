namespace Charker;

// Only the Bluetooth devices connected before lock/suspend are eligible.
internal sealed class SessionRecovery {
 readonly Dictionary<string,SavedDevice> targets=[];
 CancellationTokenSource? cancellation;
 Task running=Task.CompletedTask;
 public void Capture(IEnumerable<SavedDevice> connected){
  Cancel();
  foreach(var device in connected.Where(d=>d.Model is "A2687" or "AD1204U"))targets[device.Model]=device;
 }
 public void Cancel(){cancellation?.Cancel();}
 public async Task Stop(){targets.Clear();Cancel();await running;}
 public Task Resume(Func<SavedDevice,bool> healthy,Func<SavedDevice,CancellationToken,Task> connect,Action<string> log,
     Func<TimeSpan,CancellationToken,Task>? delay=null){
  if(!running.IsCompleted)return cancellation?.IsCancellationRequested==true ? ResumeAfterStop(healthy,connect,log,delay) : running;
  if(targets.Count==0)return running;
  var pending=targets.Values.ToArray();targets.Clear();
  cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(90));
  running=Run(pending,healthy,connect,log,delay??Task.Delay,cancellation);
  return running;
 }
 async Task ResumeAfterStop(Func<SavedDevice,bool> healthy,Func<SavedDevice,CancellationToken,Task> connect,
     Action<string> log,Func<TimeSpan,CancellationToken,Task>? delay){
  await running;
  await Resume(healthy,connect,log,delay);
 }
 static async Task Run(SavedDevice[] targets,Func<SavedDevice,bool> healthy,Func<SavedDevice,CancellationToken,Task> connect,
     Action<string> log,Func<TimeSpan,CancellationToken,Task> delay,CancellationTokenSource owner){
  try{
   await delay(TimeSpan.FromSeconds(5),owner.Token);
   await Task.WhenAll(targets.Select(async device=>{
    for(int attempt=1;attempt<=3;attempt++){
     owner.Token.ThrowIfCancellationRequested();
     if(healthy(device))return;
     using var timeout=CancellationTokenSource.CreateLinkedTokenSource(owner.Token);
     timeout.CancelAfter(TimeSpan.FromSeconds(25));
     try{await connect(device,timeout.Token);return;}
     catch(OperationCanceledException) when(owner.IsCancellationRequested){return;}
     catch(Exception e){log(device.Name+" 解锁后恢复连接失败（"+attempt+"/3）："+e.Message);}
     if(attempt<3)await delay(TimeSpan.FromSeconds(5),owner.Token);
    }
   }));
  }catch(OperationCanceledException){}
 }
}
