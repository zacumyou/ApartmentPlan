using System;
using System.Collections.Generic;
using System.Linq;
using Game.Common;
using Game.Prefabs;
using Unity.Entities;
namespace ApartmentPlan {
public partial class PlanSystem {
 sealed class RoadMigration {internal Entity Edge,Before,After;internal PlanBezier Curve;}
 readonly List<RoadMigration> roadMigration=new List<RoadMigration>();
 int migrationPlan=-1,migrationSkipped,migrationApplied;string migrationStatus="";
 object MigrationState()=>new{plan=migrationPlan,count=roadMigration.Count,skipped=migrationSkipped,applied=migrationApplied,status=migrationStatus};
 void ClearRoadMigration(){roadMigration.Clear();migrationPlan=-1;migrationStatus="";migrationSkipped=migrationApplied=0;}
 static PlanBezier MigrationCurve(Colossal.Mathematics.Bezier4x3 c)=>new PlanBezier{A=new Point(c.a.x,c.a.z),B=new Point(c.b.x,c.b.z),C=new Point(c.c.x,c.c.z),D=new Point(c.d.x,c.d.z)};
 string MigrationName(Entity entity){return EntityManager.Exists(entity)&&World.GetOrCreateSystemManaged<PrefabSystem>().TryGetPrefab<PrefabBase>(entity,out var p)?p.name:"";}
 string PlannedMigrationName(StoredPlan stored,string use){
  var d=stored.Edit?.Draft;if(d==null||d.VillaExistingRoadsOnly||use=="pedestrian")return "";
  if(d.Tower)return d.TowerAlgorithmVersion==0?"AP Invisible Road Path - 1xTwoway":use=="turnaround"?"AP Invisible Road Path - 1xOneway":"";
  if(d.Villa)return "";
  if(use=="aisleWide")return "AP Invisible Road Path - 2xTwoway";
  object value;return stored.Edit.Settings!=null&&stored.Edit.Settings.TryGetValue(use=="main"?"MainNetwork":"AisleNetwork",out value)?Convert.ToString(value):"";
 }
 void ScanRoadMigration(){
  ClearRoadMigration();var stored=SelectedStored;if(stored==null)return;migrationPlan=stored.Info.Number;
  var ready=World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>();if(!ready.Available){migrationStatus=ready.UnavailableStatus;Trace.Event("invisibleRoad.migration.unavailable",ready.Diagnostic());return;}
  var courses=stored.Edit?.Draft==null?new List<ConstructionCourse>():ConstructionCurves.Create(stored.Edit.Draft);
  foreach(var e in OwnedObjects(stored)){
   if(!EntityManager.HasComponent<Game.Net.Road>(e)||!EntityManager.HasComponent<Game.Net.Edge>(e)||!EntityManager.HasComponent<Game.Net.Curve>(e)||!EntityManager.HasComponent<PrefabRef>(e))continue;
   var before=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;string name=MigrationName(before);var curve=MigrationCurve(EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier);
   if(!InvisibleRoadPrefabs.RequiredNames.Contains(name)){
    // A loaded fallback may have lost its AP name. Ownership AND an exact
    // saved course AND an explicit saved AP road choice must agree.
    var names=courses.GroupBy(c=>PlannedMigrationName(stored,c.Use)).Where(g=>InvisibleRoadPrefabs.RequiredNames.Contains(g.Key)&&CompoundRoadPolicy.Follows(curve,g.Select(c=>c.Curve),.25)).Select(g=>g.Key).Distinct().ToArray();
    name=CompoundRoadPolicy.MigrationTarget(true,name,InvisibleRoadPrefabs.RequiredNames,names);if(name==null){migrationSkipped++;continue;}
   }
   if(!ready.TryGetRoad(name,out var after)){migrationSkipped++;continue;}
   roadMigration.Add(new RoadMigration{Edge=e,Before=before,After=after,Curve=curve});
  }
  migrationStatus=roadMigration.Count>0?"AP.MigrationReview":"AP.MigrationNone";
  Trace.Event("invisibleRoad.migration.scan","plan="+migrationPlan+" candidates="+roadMigration.Count+" skipped="+migrationSkipped);
 }
 void ApplyRoadMigration(){
  var stored=SelectedStored;if(stored==null||stored.Info.Number!=migrationPlan||roadMigration.Count==0||NativeBusy||LayoutPending)return;
  var owned=new HashSet<Entity>(OwnedObjects(stored));var ready=World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>();
  foreach(var r in roadMigration){
   if(!owned.Contains(r.Edge)||!EntityManager.HasComponent<Game.Net.Edge>(r.Edge)||!EntityManager.HasComponent<Game.Net.Curve>(r.Edge)||!EntityManager.HasComponent<PrefabRef>(r.Edge)||EntityManager.GetComponentData<PrefabRef>(r.Edge).m_Prefab!=r.Before||!ready.IsReady(MigrationName(r.After))||!CompoundRoadPolicy.Follows(MigrationCurve(EntityManager.GetComponentData<Game.Net.Curve>(r.Edge).m_Bezier),new[]{r.Curve},.01)){roadMigration.Clear();migrationStatus="AP.MigrationStale";return;}
  }
  var targets=new HashSet<Entity>();var replacements=roadMigration.ToDictionary(r=>r.Edge,r=>r.After);
  foreach(var r in roadMigration){EntityManager.SetComponentData(r.Edge,new PrefabRef{m_Prefab=r.After});ResidentialRoadSupport.RepairLoaded(EntityManager,r.Edge);if(!EntityManager.HasComponent<Game.Net.Marker>(r.Edge))EntityManager.AddComponent<Game.Net.Marker>(r.Edge);
   targets.Add(r.Edge);var edge=EntityManager.GetComponentData<Game.Net.Edge>(r.Edge);targets.Add(edge.m_Start);targets.Add(edge.m_End);}
  foreach(var node in targets.ToArray()){
   if(!EntityManager.Exists(node)||!EntityManager.HasComponent<Game.Net.Node>(node)||!EntityManager.HasBuffer<Game.Net.ConnectedEdge>(node)||!EntityManager.HasComponent<PrefabRef>(node))continue;
   var connected=EntityManager.GetBuffer<Game.Net.ConnectedEdge>(node,true).ToNativeArray(Unity.Collections.Allocator.Temp);Entity target=Entity.Null;bool exclusive=connected.Length>0;
   foreach(var c in connected){if(!replacements.TryGetValue(c.m_Edge,out var p)||target!=Entity.Null&&target!=p){exclusive=false;break;}target=p;}connected.Dispose();
   // Shared external junctions retain their native node prefab.
   if(exclusive&&target!=Entity.Null){EntityManager.SetComponentData(node,new PrefabRef{m_Prefab=target});if(!EntityManager.HasComponent<Game.Net.Marker>(node))EntityManager.AddComponent<Game.Net.Marker>(node);}
  }
  foreach(var e in targets)if(EntityManager.Exists(e)&&!EntityManager.HasComponent<Deleted>(e)&&!EntityManager.HasComponent<Updated>(e))EntityManager.AddComponent<Updated>(e);
  migrationApplied=roadMigration.Count;roadMigration.Clear();migrationStatus="AP.MigrationApplied";
  Trace.Event("invisibleRoad.migration.apply","plan="+migrationPlan+" roads="+migrationApplied+" nativeUpdateTargets="+targets.Count+"; geometry and topology retained; native rebuild pending");
 }
}
}
