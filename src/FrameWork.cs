using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace ApartmentPlan {
// Main-thread cooperative work. A yielded 1 is an unconditional frame boundary;
// 0 is one bounded unit. Never runs ECS/Unity work on a background thread.
internal sealed class FrameWork:IDisposable {
 IEnumerator<int> iterator;readonly Func<double> clock;
 internal int Slices {get;private set;} internal double CpuMs {get;private set;} internal double MaxSliceMs {get;private set;}
 internal FrameWork(IEnumerable<int> steps,Func<double> milliseconds=null){iterator=steps.GetEnumerator();clock=milliseconds??(()=>Stopwatch.GetTimestamp()*1000d/Stopwatch.Frequency);}
 internal bool Step(){
  if(iterator==null)return true;double start=clock();bool done=false;
  try{for(int units=0;units<16;units++){
   if(!iterator.MoveNext()){done=true;Dispose();break;}
   if(iterator.Current!=0||clock()-start>=2)break;
  }return done;}finally{double ms=clock()-start;Slices++;CpuMs+=ms;MaxSliceMs=Math.Max(MaxSliceMs,ms);}
 }
 public void Dispose(){var old=iterator;iterator=null;old?.Dispose();}
}
}
