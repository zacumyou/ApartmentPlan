using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public static class CompoundRoadPolicy {
 public static bool DeleteOwnedInvisible(bool owned,bool invisible)=>owned&&invisible;
 public static string MigrationTarget(bool owned,string current,IEnumerable<string> allowed,IEnumerable<string> matching){
  if(!owned)return null;var names=new HashSet<string>(allowed);if(names.Contains(current))return current;
  var matches=matching.Where(names.Contains).Distinct().ToArray();return matches.Length==1?matches[0]:null;
 }
 public static bool Follows(PlanBezier curve,IEnumerable<PlanBezier> sources,double tolerance){
  var list=sources.ToArray();if(list.Length==0)return false;
  for(int i=0;i<=8;i++){var p=curve.At(i/8d);if(!list.Any(c=>c.NearBounds(p,tolerance)&&(c.At(c.Parameter(p))-p).Length<=tolerance))return false;}
  return true;
 }
 // Legacy ownership is necessary: proximity or an AP prefab alone never grants deletion.
 public static bool RecoverLegacyInvisible(bool hasVerifiedRecord,bool owned,bool invisible,bool referenceOnly,PlanBezier curve,IEnumerable<PlanBezier> planned)=>!hasVerifiedRecord&&owned&&invisible&&!referenceOnly&&Follows(curve,planned,.25);
 public static bool NewlyPlanned(PlanBezier curve,IEnumerable<PlanBezier> planned,IEnumerable<PlanBezier> existing)=>Follows(curve,planned,.25)&&!Follows(curve,existing,.5);
}
}
