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
 // A native Combine flag is not evidence of pre-existing ownership: two new
 // courses may merge. Original/replacement roads still fail closed.
 public static bool CaptureCandidate(bool hasOriginal,bool deleting,bool replacing,bool child,bool node,bool combined)=>!hasOriginal&&!deleting&&!replacing&&!child&&(!node||!combined);
 static bool ParallelInterior(PlanBezier curve,PlanBezier source){
  // Sample each original road too, so a short old span inside a long merged
  // candidate cannot hide between candidate samples. Endpoint contact is legal.
   for(int i=1;i<16;i++){
    var point=source.At(i/16d);if(!curve.NearBounds(point,.5))continue;
    double t=curve.Parameter(point);if(t<=.001||t>=.999)continue;
    var tangent=curve.Tangent(t);var other=source.Tangent(i/16d);
    if(tangent.Length<.001||other.Length<.001)continue;
    double parallel=System.Math.Abs(Layout.Cross(tangent,other))/(tangent.Length*other.Length);
    if(parallel<.05&&(curve.At(t)-point).Length<=.5)return true;
   }
  return false;
 }
 public static bool OverlapsExisting(PlanBezier curve,IEnumerable<PlanBezier> existing)=>existing.Any(source=>ParallelInterior(curve,source)||ParallelInterior(source,curve));
 public static bool NewlyPlanned(PlanBezier curve,IEnumerable<PlanBezier> planned,IEnumerable<PlanBezier> existing)=>Follows(curve,planned,.25)&&!OverlapsExisting(curve,existing);
}
}
