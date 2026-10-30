using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public static class PlanDeletionPolicy {
 public static IEnumerable<T> ForceTargets<T>(IEnumerable<T> requested,HashSet<T> owned,HashSet<T> other,Func<T,bool> live)=>requested.Where(e=>live(e)&&owned.Contains(e)&&!other.Contains(e)).Distinct();
 public static List<T> OldestForNewPlan<T>(IEnumerable<T> plans,int maximum,Func<T,int> number,Func<T,bool> eligible){var all=plans.ToList();return all.Where(eligible).OrderBy(number).Take(Math.Max(0,all.Count-maximum+1)).ToList();}
 public static int Target(bool open,int editing,int selected)=>!open?selected:editing>=0?editing:-2;
 public static bool Allowed(bool ready,bool calculating,bool nativeTool,bool demolition,bool dialog,bool hasTarget,bool ownsObjects,bool built)
  =>ready&&!calculating&&!nativeTool&&!demolition&&!dialog&&hasTarget&&!ownsObjects&&!built;
}
}
