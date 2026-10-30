using System;
using System.Collections.Generic;
namespace ApartmentPlan {
internal static class NetworkReplacementPolicy {
 internal static HashSet<T> ConnectedComponent<T>(T start,List<Tuple<T,T>> links){
  var graph=new Dictionary<T,List<T>>();
  foreach(var link in links){if(!graph.TryGetValue(link.Item1,out var a)){a=new List<T>();graph[link.Item1]=a;}a.Add(link.Item2);if(!graph.TryGetValue(link.Item2,out var b)){b=new List<T>();graph[link.Item2]=b;}b.Add(link.Item1);}
  var seen=new HashSet<T>{start};var pending=new List<T>{start};
  for(int index=0;index<pending.Count;index++){var node=pending[index];if(graph.TryGetValue(node,out var neighbours))foreach(var other in neighbours)if(seen.Add(other))pending.Add(other);}
  return seen;
 }
 // Definition seeds are spaced 7919 apart; native split indices must stay inside
 // that interval. Geometry and prefab are independently verified by the caller.
 internal static bool MatchesSplitIdentity(int submitted,int candidate,bool samePrefab)=>samePrefab&&(long)candidate-submitted>=0&&(long)candidate-submitted<7919;
 // A previous construction stage supplies scope only; current geometry must survive.
 internal static bool SeedRetainedRoad<T>(HashSet<T> roots,HashSet<T> staged,T original,bool retained){if(!retained||!staged.Contains(original))return false;roots.Add(original);return true;}
 // Existing survivors must preserve the whole neighbouring edge, not just its joint.
 internal static bool VerifiedSurvivor(bool isNew,bool isNeighbour,bool jointCovered,bool neighbourCovered)=>jointCovered&&(isNew||isNeighbour&&neighbourCovered);
 internal static bool CoversIntervals(List<Tuple<double,double>> intervals,double tolerance){
  intervals.Sort((a,b)=>a.Item1.CompareTo(b.Item1));double end=0;
  foreach(var span in intervals){if(span.Item1>end+tolerance)return false;end=Math.Max(end,span.Item2);if(end>=1-tolerance)return true;}return false;
 }
 internal struct Merge<T>{internal T Removed,Joint,Neighbour,Survivor;internal bool Verified,NewSurvivorVerified;}
 internal static void ExpandMerges<T>(HashSet<T> roots,List<Merge<T>> candidates,HashSet<T> connectionAnchors=null){
  for(int pass=0;pass<candidates.Count;pass++){
   bool changed=false;
   foreach(var m in candidates)if(m.Verified&&(roots.Contains(m.Neighbour)||(m.NewSurvivorVerified&&connectionAnchors!=null&&connectionAnchors.Contains(m.Neighbour)))&&(roots.Contains(m.Survivor)||m.NewSurvivorVerified)){changed|=roots.Add(m.Removed);changed|=roots.Add(m.Joint);}
   if(!changed)return;
  }
 }
 // Native reduction may delete only the joint while both edge identities survive.
 internal static bool JointScope(bool directConnection,bool verifiedReplacementRoad)=>directConnection||verifiedReplacementRoad;
 internal static bool RetainedJoint(bool degreeTwo,bool scoped,bool bothRoadsPreserved,bool bridged)=>degreeTwo&&scoped&&bothRoadsPreserved&&bridged;
 internal static bool RetainedNeighbour(bool adjacent,bool samePrefab,bool fullCoverage)=>adjacent&&samePrefab&&fullCoverage;
 // A building is never an incidental road child. Cycles/missing owners fail closed.
 internal static bool Allows<T>(T item,HashSet<T> roots,Func<T,bool> exists,Func<T,bool> building,Func<T,T> owner){
  var seen=new HashSet<T>();
  for(int depth=0;depth<32&&exists(item)&&seen.Add(item);depth++){
   if(building(item))return false;
   if(roots.Contains(item))return true;
   item=owner(item);
  }return false;
 }
}
}


