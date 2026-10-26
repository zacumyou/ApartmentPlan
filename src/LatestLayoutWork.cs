using System;
using System.Threading;
using System.Threading.Tasks;
namespace ApartmentPlan {
internal sealed class LayoutDebounce {
 double due;public bool Pending{get;private set;}
 public void Schedule(double now){due=now+1;Pending=true;}
 public void Cancel(){Pending=false;}
 public bool Take(double now){if(!Pending||now<due)return false;Pending=false;return true;}
}
// One active worker, one replaceable pending request. Poll never waits. No
// game objects are touched here; callers submit detached managed snapshots.
internal sealed class LatestLayoutWork<T> {
 Task<T> running;Func<CancellationToken,T> pending;CancellationTokenSource cancel;
 int revision,activeRevision;
 public void Submit(Func<CancellationToken,T> work){revision++;cancel?.Cancel();pending=work;Start();}
 public void Cancel(){revision++;pending=null;cancel?.Cancel();}
 void Start(){if(running!=null||pending==null)return;var work=pending;pending=null;cancel=new CancellationTokenSource();var token=cancel.Token;activeRevision=revision;running=Task.Run(()=>work(token),token);}
 public bool Poll(out T value,out Exception error){
  value=default(T);error=null;if(running==null||!running.IsCompleted){Start();return false;}
  bool current=activeRevision==revision;
  try{var result=running.GetAwaiter().GetResult();if(current)value=result;}catch(Exception e){if(current)error=e;}
  running=null;cancel.Dispose();cancel=null;Start();return current;
 }
}
}
