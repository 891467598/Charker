namespace Charker;
public sealed class LatestStateSignal {
 int pending=1;public void Set()=>Interlocked.Exchange(ref pending,1);public bool Take()=>Interlocked.Exchange(ref pending,0)!=0;
}
/// <summary>One probe per second; normal full-state reads stay infrequent. No synthesized measurements.</summary>
public sealed class BlePollSchedule {
 double lastProbe,lastRead,lastTrigger;
 public (bool Probe,bool Read,bool Trigger) Due(double seconds,double ageSeconds){
  bool probe=seconds-lastProbe>=1,read=seconds-lastRead>=12||(ageSeconds>=2&&seconds-lastRead>=2),trigger=ageSeconds>=3&&seconds-lastTrigger>=5;
  if(probe)lastProbe=seconds;if(read)lastRead=seconds;if(trigger)lastTrigger=seconds;return(probe,read,trigger);
 }
}
