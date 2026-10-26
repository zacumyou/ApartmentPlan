using System;
using System.Collections.Generic;
using Game.Common;
using Unity.Entities;
namespace ApartmentPlan {
public partial class ConstructionTool {
 // Stage 0 is a reversible roads-only preview. Only the user's Apply enters stage 1.
 int villaStage,roadWaitFrames,roadStableFrames,roadSignature=-1;
 readonly List<Entity> stagedRoads=new List<Entity>();
 void CheckpointRoads(){
  var s=Plans;var stored=s?.Saved.Find(p=>p.Info.Number==s.EditingPlan);if(stored==null)throw new InvalidOperationException("Road stage has no saved plan");
  if(!EntityManager.HasBuffer<PlanOwnedEntity>(stored.Entity))EntityManager.AddBuffer<PlanOwnedEntity>(stored.Entity);
  var owned=EntityManager.GetBuffer<PlanOwnedEntity>(stored.Entity);owned.Clear();foreach(var e in stagedRoads)owned.Add(new PlanOwnedEntity{Entity=e});
  SaveVerifiedRoads(stored.Entity);
  if(!EntityManager.HasComponent<PlanConstructionState>(stored.Entity))EntityManager.AddComponent<PlanConstructionState>(stored.Entity);
  EntityManager.SetComponentData(stored.Entity,new PlanConstructionState{Applied=1});s.NativeCommitted=true;s.Revision++;
 }
 void AdvanceRoadStage(){
  roadWaitFrames++;
  int edges=0,signature=17,missingCar=0,missingWalk=0,paths=0;bool ready=true;
  foreach(var e in stagedRoads){
   if(!EntityManager.Exists(e)||EntityManager.HasComponent<Deleted>(e)||EntityManager.HasComponent<Game.Tools.Temp>(e)){ready=false;continue;}
   if(!EntityManager.HasComponent<Game.Net.Edge>(e))continue;edges++;
   if(EntityManager.HasComponent<Updated>(e)||!EntityManager.HasComponent<Game.Net.EdgeGeometry>(e)||!EntityManager.HasComponent<Game.Net.Composition>(e)||!EntityManager.HasBuffer<Game.Net.SubLane>(e)){ready=false;continue;}
   bool car=false,walk=false;foreach(var item in EntityManager.GetBuffer<Game.Net.SubLane>(e,true)){
    var lane=item.m_SubLane;if(!EntityManager.Exists(lane)||EntityManager.HasComponent<Deleted>(lane)||EntityManager.HasComponent<Game.Tools.Temp>(lane)||!EntityManager.HasComponent<Game.Net.Curve>(lane)){ready=false;continue;}
    car|=EntityManager.HasComponent<Game.Net.CarLane>(lane);walk|=EntityManager.HasComponent<Game.Net.PedestrianLane>(lane);unchecked{signature=signature*31+lane.Index;}
   }
   bool requireCar=true,requireWalk=true;
   if(EntityManager.HasComponent<Game.Prefabs.PrefabRef>(e)){
    var prefab=EntityManager.GetComponentData<Game.Prefabs.PrefabRef>(e).m_Prefab;
    var choice=Plans?.Networks.Find(n=>n.Entity==prefab);
    if(choice!=null){requireCar=choice.Car;requireWalk=choice.Walk;}
    else if(EntityManager.HasComponent<Game.Prefabs.PathwayData>(prefab)&&!EntityManager.HasComponent<Game.Prefabs.RoadData>(prefab)){requireCar=false;requireWalk=true;}
   }
   if(!requireCar)paths++;
   if(requireCar&&!car){missingCar++;ready=false;}if(requireWalk&&!walk){missingWalk++;ready=false;}
   var edge=EntityManager.GetComponentData<Game.Net.Edge>(e);
   foreach(var node in new[]{edge.m_Start,edge.m_End}){
    if(!EntityManager.Exists(node)||EntityManager.HasComponent<Game.Tools.Temp>(node)||EntityManager.HasComponent<Updated>(node)||!EntityManager.HasBuffer<Game.Net.ConnectedEdge>(node)||!EntityManager.HasBuffer<Game.Net.SubLane>(node)){ready=false;continue;}
    bool attached=false;foreach(var c in EntityManager.GetBuffer<Game.Net.ConnectedEdge>(node,true))if(c.m_Edge==e)attached=true;if(!attached)ready=false;
    foreach(var lane in EntityManager.GetBuffer<Game.Net.SubLane>(node,true)){if(!EntityManager.Exists(lane.m_SubLane)||EntityManager.HasComponent<Deleted>(lane.m_SubLane)||EntityManager.HasComponent<Game.Tools.Temp>(lane.m_SubLane))ready=false;unchecked{signature=signature*31+lane.m_SubLane.Index;}}
   }
  }
  roadStableFrames=ready&&edges>0&&signature==roadSignature?roadStableFrames+1:0;roadSignature=signature;
  if(roadWaitFrames==1||roadWaitFrames%120==0||roadWaitFrames>=900)Trace.Event("construction.roads.wait","frames="+roadWaitFrames+" edges="+edges+" paths="+paths+" missingCar="+missingCar+" missingWalk="+missingWalk+" pending="+(!ready)+" stable="+roadStableFrames);
  if(roadStableFrames<4){if(roadWaitFrames>=900)throw new InvalidOperationException("Road initialization timed out: edges="+edges+", paths="+paths+", missingCar="+missingCar+", missingWalk="+missingWalk+", stable="+roadStableFrames+". Built roads remain owned by this plan; buildings were not applied.");return;}
  Trace.Event("construction.roads.ready","frames="+roadWaitFrames+" edges="+edges+" stable="+roadStableFrames);
  villaStage=2;submitted=false;frame=1;ResetProgress();Status("AP.BuildingStagePreparing");
 }
}
}
