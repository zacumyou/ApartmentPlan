using System;
using System.Threading;
using ApartmentPlan;
static class LayoutWorkerTests {
 public static void Run(){
  var delay=new LayoutDebounce();delay.Schedule(0);delay.Schedule(.6);
  if(delay.Take(1)||delay.Take(1.59)||!delay.Take(1.61)||delay.Take(2))throw new Exception("debounce must execute once after last change");
  delay.Schedule(3);delay.Cancel();if(delay.Take(5))throw new Exception("cancelled debounce executed");
  Console.WriteLine("PASS burst coalescing / deadline reset / single execution / cancellation");

  var work=new LatestLayoutWork<int>();using(var started=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
   work.Submit(token=>{started.Set();release.Wait(3000);return 1;});
   if(!started.Wait(2000))throw new Exception("Worker did not start");
   var timer=System.Diagnostics.Stopwatch.StartNew();if(work.Poll(out _,out _)||timer.ElapsedMilliseconds>100)throw new Exception("Poll blocked");
   work.Submit(token=>2);work.Submit(token=>3);release.Set();
   int value=0;Exception error=null;timer.Restart();while(!work.Poll(out value,out error)){if(timer.ElapsedMilliseconds>3000)throw new Exception("Worker timeout");Thread.Sleep(1);}
   if(value!=3||error!=null)throw new Exception("Stale output published");
  }
  work.Submit(token=>throw new ArgumentException("expected"));
  var wait=System.Diagnostics.Stopwatch.StartNew();while(true){if(work.Poll(out _,out var error)){if(!(error is ArgumentException))throw new Exception("Missing error");break;}if(wait.ElapsedMilliseconds>2000)throw new Exception("Timeout");Thread.Sleep(1);}
  using(var started=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
   work.Submit(token=>{started.Set();release.Wait(3000);return 4;});if(!started.Wait(2000))throw new Exception("Start timeout");work.Cancel();release.Set();
   for(int i=0;i<30;i++){if(work.Poll(out _,out _))throw new Exception("Cancelled output published");Thread.Sleep(1);}
  }
  Console.WriteLine("PASS nonblocking poll / latest request only / failures / cancelled session output discarded");
 }
}
