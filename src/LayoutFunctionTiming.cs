using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
namespace ApartmentPlan {
// One worker owns one collector. Inclusive times: nested rows must not be added together.
internal sealed class LayoutFunctionTiming {
 sealed class Entry {public long Calls,Ticks,Max;}
 readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
 internal struct Scope:IDisposable {
  readonly LayoutFunctionTiming owner;readonly string name;readonly long started;
  internal Scope(LayoutFunctionTiming owner,string name){this.owner=owner;this.name=name;started=owner==null?0:Stopwatch.GetTimestamp();}
  public void Dispose(){if(owner!=null)owner.Record(name,Stopwatch.GetTimestamp()-started);}
 }
 internal static Scope Start(LayoutFunctionTiming owner,string name)=>new Scope(owner,name);
 void Record(string name,long ticks){Entry e;if(!entries.TryGetValue(name,out e)){if(entries.Count>=64)return;entries[name]=e=new Entry();}e.Calls++;e.Ticks+=ticks;if(ticks>e.Max)e.Max=ticks;}
 internal void Flush(Action<string> write){foreach(var pair in entries){var e=pair.Value;double ms=e.Ticks*1000.0/Stopwatch.Frequency;write("function="+pair.Key+" calls="+e.Calls+" totalMs="+ms.ToString("F3",CultureInfo.InvariantCulture)+" meanMs="+(ms/e.Calls).ToString("F6",CultureInfo.InvariantCulture)+" maxMs="+(e.Max*1000.0/Stopwatch.Frequency).ToString("F3",CultureInfo.InvariantCulture)+" inclusive=true");}}
}
}
