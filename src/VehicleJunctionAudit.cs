using System;
using System.Collections.Generic;
using System.Linq;
using Game.Common;
using Game.Prefabs;
using Unity.Entities;
namespace ApartmentPlan {
public partial class ConstructionTool {
 int AuditVehicleJunctions(out int checks){
  int missing=0;checks=0;var done=new HashSet<string>();var prefabs=World.GetOrCreateSystemManaged<PrefabSystem>();
  foreach(var edge in OwnedEntities){
   if(!Live(edge)||!EntityManager.HasComponent<Game.Net.Edge>(edge)||!EntityManager.HasComponent<PrefabRef>(edge))continue;
   var p=EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab;
   if(!prefabs.TryGetPrefab<PrefabBase>(p,out var road)||!road.name.StartsWith("AP Invisible ",StringComparison.Ordinal))continue;
   var network=EntityManager.GetComponentData<Game.Net.Edge>(edge);
   foreach(var node in new[]{network.m_Start,network.m_End}){
    if(!Live(node)||!EntityManager.HasBuffer<Game.Net.ConnectedEdge>(node)||!done.Add(edge.Index+"/"+node.Index))continue;
    var others=new HashSet<int>();foreach(var item in EntityManager.GetBuffer<Game.Net.ConnectedEdge>(node,true))if(item.m_Edge!=edge&&Live(item.m_Edge)&&EntityManager.HasComponent<PrefabRef>(item.m_Edge)&&EntityManager.HasComponent<RoadData>(EntityManager.GetComponentData<PrefabRef>(item.m_Edge).m_Prefab))others.Add(item.m_Edge.Index);
    if(others.Count==0)continue;checks++;bool enter=false,exit=false;int forbidden=0;
    if(EntityManager.HasBuffer<Game.Net.SubLane>(node))foreach(var sub in EntityManager.GetBuffer<Game.Net.SubLane>(node,true)){
     var e=sub.m_SubLane;if(!Live(e)||!EntityManager.HasComponent<Game.Net.CarLane>(e)||!EntityManager.HasComponent<Game.Net.Lane>(e))continue;
     var flags=EntityManager.GetComponentData<Game.Net.CarLane>(e).m_Flags;
     var lane=EntityManager.GetComponentData<Game.Net.Lane>(e);int a=lane.m_StartNode.GetOwnerIndex(),b=lane.m_EndNode.GetOwnerIndex();
     bool enters=others.Contains(a)&&b==edge.Index,exits=a==edge.Index&&others.Contains(b);if(!enters&&!exits)continue;
     if((flags&(Game.Net.CarLaneFlags.Forbidden|Game.Net.CarLaneFlags.PublicOnly))!=0){forbidden++;continue;}
     if(EntityManager.HasComponent<PrefabRef>(e)){var lp=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;if(EntityManager.HasComponent<NetLaneData>(lp)&&(EntityManager.GetComponentData<NetLaneData>(lp).m_Flags&LaneFlags.Virtual)!=0)continue;if(!EntityManager.HasComponent<CarLaneData>(lp)||(EntityManager.GetComponentData<CarLaneData>(lp).m_RoadTypes&Game.Net.RoadTypes.Car)==0)continue;}
     enter|=enters||(flags&Game.Net.CarLaneFlags.Twoway)!=0;exit|=exits||(flags&Game.Net.CarLaneFlags.Twoway)!=0;
    }
    if(!enter||!exit){
     missing++;
     Trace.Event("construction.junction.missing","edge="+DescribeConnection(edge)+" node="+DescribeConnection(node));
     if(EntityManager.HasBuffer<Game.Net.SubLane>(node)){int traced=0;foreach(var sub in EntityManager.GetBuffer<Game.Net.SubLane>(node,true)){
      var lane=sub.m_SubLane;if(!Live(lane)||!EntityManager.HasComponent<Game.Net.CarLane>(lane)||!EntityManager.HasComponent<Game.Net.Lane>(lane))continue;if(traced++>=32)break;
      var endpoints=EntityManager.GetComponentData<Game.Net.Lane>(lane);
      Trace.Event("construction.junction.candidate","node="+node+" lane="+DescribeParkingCarLane(lane)+" startOwner="+endpoints.m_StartNode.GetOwnerIndex()+" endOwner="+endpoints.m_EndNode.GetOwnerIndex());
     }}
    }
    Trace.Event("construction.junction.audit","road="+road.name+" edge="+edge+" node="+node+" enter="+enter+" exit="+exit+" forbidden="+forbidden+" connectedRoads="+others.Count);
   }
  }return missing;
 }
 internal void AuditStored(StoredPlan stored){
  var objects=Plans.OwnedObjects(stored);OwnedEntities.Clear();OwnedEntities.AddRange(objects);appliedBuildings.Clear();appliedBays.Clear();appliedTrees.Clear();expectedTrees.Clear();expected.Clear();expectedBays.Clear();
  foreach(var e in objects){if(!EntityManager.HasComponent<Game.Objects.Transform>(e)||!EntityManager.HasComponent<PrefabRef>(e))continue;var tr=EntityManager.GetComponentData<Game.Objects.Transform>(e);var p=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
   if(EntityManager.HasComponent<Game.Buildings.Building>(e)){appliedBuildings.Add(e);expected.Add(Tuple.Create(p,tr.m_Position,tr.m_Rotation));}
   else if(EntityManager.HasComponent<Game.Objects.Tree>(e))appliedTrees.Add(e);
   else if(World.GetOrCreateSystemManaged<PrefabSystem>().TryGetPrefab<PrefabBase>(p,out var prefab)&&prefab.name==PlanSystem.ParkingDecalId){appliedBays.Add(e);expectedBays.Add(Tuple.Create(p,tr.m_Position,tr.m_Rotation));}
  }Audit();Plans.NativeAuditAvailable=true;Plans.Revision++;
 }
}
}
