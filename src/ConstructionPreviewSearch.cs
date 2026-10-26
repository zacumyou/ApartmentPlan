using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace ApartmentPlan {
public partial class ConstructionTool {
 struct ConnectionCandidate {internal Entity Entity;internal Bezier4x3 Curve;internal bool Path;internal float Width;}
 static void IndexCurve(SpatialLookup<ConnectionCandidate> index,ConnectionCandidate candidate,float margin){
  var c=candidate.Curve;
  // Cubic curves remain inside their control-point convex hull, including bends.
  var min=math.min(math.min(c.a.xz,c.b.xz),math.min(c.c.xz,c.d.xz))-margin;
  var max=math.max(math.max(c.a.xz,c.b.xz),math.max(c.c.xz,c.d.xz))+margin;
  index.Add(candidate,min.x,min.y,max.x,max.y);
 }
 IEnumerable<int> ConnectionSnapshotSteps(SpatialLookup<ConnectionCandidate> index){
  int count=0,visited=0;Entity[] snapshot;
  using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Game.Net.Edge>(),ComponentType.ReadOnly<Game.Net.Curve>(),ComponentType.ReadOnly<PrefabRef>(),ComponentType.Exclude<Temp>(),ComponentType.Exclude<Deleted>()))
  using(var es=q.ToEntityArray(Allocator.Temp))snapshot=es.ToArray();
  foreach(var e in snapshot){
   if(++visited%64==0)yield return 0;
   if(!EntityManager.Exists(e)||EntityManager.HasComponent<Deleted>(e))continue;
   var prefab=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
   bool road=EntityManager.HasComponent<RoadData>(prefab),path=EntityManager.HasComponent<PathwayData>(prefab);
   if(!road&&!path)continue;
   if(villaStage<2)preexistingRoadCurves.Add(RoadCurve(EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier));
   IndexCurve(index,new ConnectionCandidate{Entity=e,Curve=EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier,Path=!road},1.5f);count++;
  }
  Trace.Event("construction.connections.snapshot","roads="+count+" scans=1");
 }
 struct PreviewObject {internal Entity Entity,Prefab;internal float3 Position;}
 IEnumerable<int> MatchParkingPreviewsSteps(){
  var index=new SpatialLookup<PreviewObject>(2);var used=new HashSet<Entity>();
  using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Temp>(),ComponentType.ReadOnly<Game.Objects.Transform>(),ComponentType.ReadOnly<PrefabRef>(),ComponentType.Exclude<Deleted>()))
  using(var es=q.ToEntityArray(Allocator.Temp))foreach(var e in es){
   var t=EntityManager.GetComponentData<Temp>(e);if(t.m_Original!=Entity.Null||(t.m_Flags&TempFlags.Delete)!=0)continue;
   var prefab=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
   if(expectedBays.Count==0||prefab!=expectedBays[0].Item1)continue;
   var p=EntityManager.GetComponentData<Game.Objects.Transform>(e).m_Position;
   index.Add(new PreviewObject{Entity=e,Prefab=prefab,Position=p},p.x-1,p.z-1,p.x+1,p.z+1);
  }
  appliedBays.Clear();foreach(var target in expectedBays){yield return 0;foreach(var candidate in index.At(target.Item2.x,target.Item2.z)){
   if(used.Contains(candidate.Entity)||candidate.Prefab!=target.Item1||math.distancesq(candidate.Position,target.Item2)>=.1f)continue;
   used.Add(candidate.Entity);appliedBays.Add(candidate.Entity);break;
  }}
 }
}
}
