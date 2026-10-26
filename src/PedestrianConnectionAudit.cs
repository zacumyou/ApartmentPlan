using System;
using System.Collections.Generic;
using System.Linq;
using Game.Common;
using Game.Prefabs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
namespace ApartmentPlan {
public partial class ConstructionTool {
 internal string PedestrianAuditFile="";
 static object AuditPoint(float3 p)=>new{x=p.x,y=p.y,z=p.z};
 string AuditPrefab(Entity e){if(!EntityManager.Exists(e)||!EntityManager.HasComponent<PrefabRef>(e))return "";return World.GetOrCreateSystemManaged<PrefabSystem>().TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab,out var prefab)?prefab.name:"unknown";}
 object AuditReference(Entity e)=>new{entity=e.ToString(),live=Live(e),prefab=AuditPrefab(e)};
 object AuditLaneBinding(Entity lane,float parameter){
  bool exists=EntityManager.Exists(lane),hasLane=exists&&EntityManager.HasComponent<Game.Net.Lane>(lane);
  object point=null;if(exists&&EntityManager.HasComponent<Game.Net.Curve>(lane)&&math.isfinite(parameter)){
   var c=EntityManager.GetComponentData<Game.Net.Curve>(lane).m_Bezier;float t=parameter,u=1-t;point=AuditPoint(c.a*(u*u*u)+c.b*(3*u*u*t)+c.c*(3*u*t*t)+c.d*(t*t*t));
  }
  return new{target=AuditReference(lane),state=lane==Entity.Null?"null":!exists?"missing":!Live(lane)?"not-live":!hasLane?"not-lane":"live-lane",hasLane,parameter,point};
 }
 object AuditAccessBindings(Entity e){
  bool hasAccess=EntityManager.HasComponent<Game.Routes.AccessLane>(e),hasRoute=EntityManager.HasComponent<Game.Routes.RouteLane>(e),hasSpawn=EntityManager.HasComponent<Game.Objects.SpawnLocation>(e);
  object accessLane=null,routeLane=null,spawn=null;
  if(hasAccess){var a=EntityManager.GetComponentData<Game.Routes.AccessLane>(e);accessLane=AuditLaneBinding(a.m_Lane,a.m_CurvePos);}
  if(hasRoute){var r=EntityManager.GetComponentData<Game.Routes.RouteLane>(e);routeLane=new{start=AuditLaneBinding(r.m_StartLane,r.m_StartCurvePos),end=AuditLaneBinding(r.m_EndLane,r.m_EndCurvePos)};}
  if(hasSpawn){var a=EntityManager.GetComponentData<Game.Objects.SpawnLocation>(e);spawn=new{first=AuditLaneBinding(a.m_ConnectedLane1,a.m_CurvePosition1),second=AuditLaneBinding(a.m_ConnectedLane2,a.m_CurvePosition2)};}
  return new{hasAccess,hasRoute,hasSpawn,accessLane,routeLane,spawn};
 }
 void AuditPedestrianConnections(){
  try{
   var reports=new List<object>();int warningBuildings=0,totalLanes=0;var stamp=DateTime.UtcNow;
   foreach(var building in appliedBuildings.Distinct()){
    if(!Live(building)||!EntityManager.HasComponent<Game.Buildings.Building>(building))continue;
    var road=EntityManager.GetComponentData<Game.Buildings.Building>(building).m_RoadEdge;
    var owned=new HashSet<Entity>();var queue=new List<Entity>{building};var lanes=new HashSet<Entity>();int networkCount=0,areaCount=0;var access=new List<object>();
    // Scoped traversal; cycles and corrupt/huge ownership graphs are bounded explicitly.
    int cursor=0;for(;cursor<queue.Count&&cursor<4096;cursor++){
     var e=queue[cursor];if(!Live(e)||!owned.Add(e))continue;
     if(EntityManager.HasBuffer<Game.Net.SubNet>(e))foreach(var item in EntityManager.GetBuffer<Game.Net.SubNet>(e,true))queue.Add(item.m_SubNet);
     if(EntityManager.HasComponent<Game.Net.Edge>(e)||EntityManager.HasComponent<Game.Net.Node>(e)){
      networkCount++;
      if(EntityManager.HasComponent<Game.Net.Edge>(e)){var edge=EntityManager.GetComponentData<Game.Net.Edge>(e);queue.Add(edge.m_Start);queue.Add(edge.m_End);}
     }
     if(EntityManager.HasBuffer<Game.Objects.SubObject>(e))foreach(var item in EntityManager.GetBuffer<Game.Objects.SubObject>(e,true))queue.Add(item.m_SubObject);
     if(EntityManager.HasBuffer<Game.Areas.SubArea>(e))foreach(var item in EntityManager.GetBuffer<Game.Areas.SubArea>(e,true))queue.Add(item.m_Area);
     if(EntityManager.HasBuffer<Game.Net.SubLane>(e))foreach(var item in EntityManager.GetBuffer<Game.Net.SubLane>(e,true))if(Live(item.m_SubLane))lanes.Add(item.m_SubLane);
     if(EntityManager.HasComponent<Game.Areas.Area>(e))areaCount++;
     if(EntityManager.HasComponent<PrefabRef>(e)){
      var prefab=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
      if(EntityManager.HasComponent<RouteConnectionData>(prefab)){
       var data=EntityManager.GetComponentData<RouteConnectionData>(prefab);
       if(data.m_AccessConnectionType==RouteConnectionType.Pedestrian||data.m_RouteConnectionType==RouteConnectionType.Pedestrian)access.Add(new{entity=e.ToString(),prefab=AuditPrefab(e),bindings=AuditAccessBindings(e),accessType=data.m_AccessConnectionType.ToString(),routeType=data.m_RouteConnectionType.ToString(),position=EntityManager.HasComponent<Game.Objects.Transform>(e)?AuditPoint(EntityManager.GetComponentData<Game.Objects.Transform>(e).m_Position):null});
      }
     }
    }
    var notifications=new List<object>();bool warning=false;
    if(EntityManager.HasBuffer<Game.Notifications.IconElement>(building))foreach(var icon in EntityManager.GetBuffer<Game.Notifications.IconElement>(building,true)){
     string name=AuditPrefab(icon.m_Icon);warning|=name=="No Pedestrian Access";var target=EntityManager.HasComponent<Target>(icon.m_Icon)?EntityManager.GetComponentData<Target>(icon.m_Icon).m_Target:Entity.Null;
     notifications.Add(new{name,target=AuditReference(target)});
    }
    if(warning)warningBuildings++;totalLanes+=lanes.Count;
    var buildingPrefab=EntityManager.GetComponentData<PrefabRef>(building).m_Prefab;
    reports.Add(new{entity=building.ToString(),prefab=AuditPrefab(building),road=AuditReference(road),warning,notifications,access,areaCount,networkCount,laneCount=lanes.Count,prefabAreas=EntityManager.HasBuffer<Game.Prefabs.SubArea>(buildingPrefab)?EntityManager.GetBuffer<Game.Prefabs.SubArea>(buildingPrefab,true).Length:0,prefabNetworks=EntityManager.HasBuffer<Game.Prefabs.SubNet>(buildingPrefab)?EntityManager.GetBuffer<Game.Prefabs.SubNet>(buildingPrefab,true).Length:0,traversalTruncated=cursor<queue.Count});
   }
   PedestrianAuditFile=Trace.WriteSnapshot("pedestrian-connections",new{schemaVersion=5,utc=stamp,build=typeof(Mod).Module.ModuleVersionId,run=RunGeneration,buildings=reports.Count,warningBuildings,totalLanes,scope="Plan buildings only. Child counts and pedestrian access bindings; decorative areas may be replaced. Counts and live lane references do not prove pathfinding success.",reports});
   Trace.Event("construction.pedestrian.snapshot","buildings="+reports.Count+" warnings="+warningBuildings+" lanes="+totalLanes+" file="+PedestrianAuditFile);
  }catch(Exception e){Trace.Failure("construction.pedestrian.snapshot.failed",e);}
 }
}
}
