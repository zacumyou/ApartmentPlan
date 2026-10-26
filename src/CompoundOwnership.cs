using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Serialization.Entities;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace ApartmentPlan {
// Entity references are remapped by the city serializer; never store raw indices.
public struct PlanOwnedEntity:IBufferElementData,ISerializable {
 public Entity Entity;
 public void Serialize<T>(T w) where T:IWriter{w.Write(Entity);}
 public void Deserialize<T>(T r) where T:IReader{r.Read(out Entity);}
}
public struct PlanBuiltRoad:IBufferElementData,ISerializable {
 public Entity Entity;
 public void Serialize<T>(T w) where T:IWriter{w.Write(Entity);}
 public void Deserialize<T>(T r) where T:IReader{r.Read(out Entity);}
}
public partial class PlanSystem {
 internal bool HasOwnedObjects(StoredPlan stored){
  if(stored==null||!EntityManager.Exists(stored.Entity)||!EntityManager.HasBuffer<PlanOwnedEntity>(stored.Entity))return false;
  foreach(var item in EntityManager.GetBuffer<PlanOwnedEntity>(stored.Entity,true))if(EntityManager.Exists(item.Entity)&&!EntityManager.HasComponent<Deleted>(item.Entity)&&!EntityManager.HasComponent<Temp>(item.Entity))return true;
  return false;
 }
 internal List<Entity> OwnedObjects(StoredPlan stored){
  var result=new List<Entity>();if(stored==null||!EntityManager.Exists(stored.Entity)||!EntityManager.HasBuffer<PlanOwnedEntity>(stored.Entity))return result;
  foreach(var item in EntityManager.GetBuffer<PlanOwnedEntity>(stored.Entity,true))if(EntityManager.Exists(item.Entity)&&!EntityManager.HasComponent<Deleted>(item.Entity)&&!EntityManager.HasComponent<Temp>(item.Entity))result.Add(item.Entity);
  return result.Distinct().ToList();
 }
 // Owned AP roads are always part of compound demolition, even when an old
 // verified-road buffer exists or a native merge changed their saved curve.
 // Ownership is still required: never sweep other compounds or nearby roads.
 void RecoverLegacyInvisibleRoads(StoredPlan stored,List<Entity> owned){
  var prefabs=World.GetOrCreateSystemManaged<PrefabSystem>();var recovered=new List<Entity>();
  var known=new HashSet<Entity>();
  if(EntityManager.HasBuffer<PlanBuiltRoad>(stored.Entity))foreach(var item in EntityManager.GetBuffer<PlanBuiltRoad>(stored.Entity,true))known.Add(item.Entity);
  foreach(var edge in owned){
   if(!EntityManager.HasComponent<Game.Net.Edge>(edge)||!EntityManager.HasComponent<PrefabRef>(edge))continue;
   bool invisible=prefabs.TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab,out var road)&&road.name.StartsWith("AP Invisible ",StringComparison.Ordinal);
   if(CompoundRoadPolicy.DeleteOwnedInvisible(true,invisible)&&known.Add(edge))recovered.Add(edge);
  }
  if(recovered.Count>0){
   if(!EntityManager.HasBuffer<PlanBuiltRoad>(stored.Entity))EntityManager.AddBuffer<PlanBuiltRoad>(stored.Entity);
   var verified=EntityManager.GetBuffer<PlanBuiltRoad>(stored.Entity);foreach(var edge in recovered)verified.Add(new PlanBuiltRoad{Entity=edge});
  }
  Trace.Event("plan.demolition.ownedInvisible","id="+stored.Info.Number+" added="+recovered.Count);
 }
 void BeginDemolition(){
  var stored=SelectedStored;var owned=OwnedObjects(stored);if(stored==null||owned.Count==0){Status="AP.NoOwnership";return;}
  RecoverLegacyInvisibleRoads(stored,owned);
  var verified=new HashSet<Entity>();
  if(EntityManager.HasBuffer<PlanBuiltRoad>(stored.Entity))foreach(var road in EntityManager.GetBuffer<PlanBuiltRoad>(stored.Entity,true))verified.Add(road.Entity);
  var retained=owned.Where(e=>EntityManager.HasComponent<Game.Net.Edge>(e)&&!verified.Contains(e)).ToHashSet();
  foreach(var node in owned.Where(e=>EntityManager.HasComponent<Game.Net.Node>(e))){
   if(!EntityManager.HasBuffer<Game.Net.ConnectedEdge>(node)){retained.Add(node);continue;}
   foreach(var edge in EntityManager.GetBuffer<Game.Net.ConnectedEdge>(node,true))if(EntityManager.Exists(edge.m_Edge)&&!EntityManager.HasComponent<Deleted>(edge.m_Edge)&&(!verified.Contains(edge.m_Edge)||!owned.Contains(edge.m_Edge))){retained.Add(node);break;}
  }
  owned.RemoveAll(retained.Contains);Trace.Event("plan.demolition.protectedRoads","retained="+retained.Count);
  EnsurePlanningCatalog();
  if(EntityManager.HasComponent<PlanGrassWork>(stored.Entity))EntityManager.SetComponentData(stored.Entity,new PlanGrassWork{State=2});
  Open=true;SelectedPlan=-1;Focused=false;NativeAuditAvailable=false;Status="AP.NativeChecking";
  var tool=World.GetOrCreateSystemManaged<ConstructionTool>();World.GetOrCreateSystemManaged<ToolSystem>().activeTool=tool;tool.PrepareDemolition(stored.Info.Number,owned);Revision++;
 }
 void CompleteDemolition(string value){
  if(!int.TryParse(value,out int id))return;var stored=Saved.Find(p=>p.Info.Number==id);if(stored==null)return;
  if(EntityManager.HasBuffer<PlanOwnedEntity>(stored.Entity)){
   var verified=new HashSet<Entity>();if(EntityManager.HasBuffer<PlanBuiltRoad>(stored.Entity))foreach(var road in EntityManager.GetBuffer<PlanBuiltRoad>(stored.Entity,true))verified.Add(road.Entity);
   var owned=new HashSet<Entity>(OwnedObjects(stored));var buffer=EntityManager.GetBuffer<PlanOwnedEntity>(stored.Entity);
   for(int i=buffer.Length-1;i>=0;i--){var entity=buffer[i].Entity;if(!owned.Contains(entity)||EntityManager.HasComponent<Game.Net.Edge>(entity)&&!verified.Contains(entity)){buffer.RemoveAt(i);continue;}
    if(EntityManager.HasBuffer<Game.Net.ConnectedEdge>(entity)){bool shared=false;foreach(var edge in EntityManager.GetBuffer<Game.Net.ConnectedEdge>(entity,true))if((!owned.Contains(edge.m_Edge)||!verified.Contains(edge.m_Edge))&&EntityManager.Exists(edge.m_Edge)&&!EntityManager.HasComponent<Deleted>(edge.m_Edge))shared=true;if(shared)buffer.RemoveAt(i);}
   }
  }
  if(OwnedObjects(stored).Count>0){Status="AP.DemolitionIncomplete";Revision++;return;}
  EntityManager.DestroyEntity(stored.Entity);Saved.Remove(stored);if(EditingPlan==id){EditingPlan=-1;Draft=null;Preview.Clear();Boundary.Clear();Entrances.Clear();Entrance=null;}SelectedPlan=-1;NativeCommitted=false;Status="AP.DemolitionApplied";Revision++;
  Trace.Event("plan.demolition.complete","id="+id);
 }
}
public partial class ConstructionTool {
 internal bool Demolishing;int demolitionPlan=-1;
 internal int DemolitionPlan=>demolitionPlan;
 int requestedDemolition=-1;List<Entity> requestedDemolitionTargets;
 internal readonly List<Entity> OwnedEntities=new List<Entity>();
 readonly HashSet<Entity> demolitionTargets=new HashSet<Entity>();
 // ToolSystem changes the active tool before ECS calls OnStartRunning. Keep
 // the request separate from per-run state so initialization cannot erase it.
 internal void PrepareDemolition(int id,IEnumerable<Entity> targets){requestedDemolition=id;requestedDemolitionTargets=targets.ToList();}
 void StartDemolitionRequest(){
  Demolishing=requestedDemolition>=0;demolitionPlan=requestedDemolition;demolitionTargets.Clear();
  if(Demolishing&&requestedDemolitionTargets!=null)foreach(var e in requestedDemolitionTargets)demolitionTargets.Add(e);
  requestedDemolition=-1;requestedDemolitionTargets=null;
 }
 readonly List<PlanBezier> preexistingRoadCurves=new List<PlanBezier>();
 static PlanBezier RoadCurve(Colossal.Mathematics.Bezier4x3 c)=>new PlanBezier{A=new Point(c.a.x,c.a.z),B=new Point(c.b.x,c.b.z),C=new Point(c.c.x,c.c.z),D=new Point(c.d.x,c.d.z)};
 internal void SaveVerifiedRoads(Entity plan){
  if(!EntityManager.HasBuffer<PlanBuiltRoad>(plan))EntityManager.AddBuffer<PlanBuiltRoad>(plan);
  var buffer=EntityManager.GetBuffer<PlanBuiltRoad>(plan);if(Plans?.CompoundApplying!=true)buffer.Clear();
  foreach(var e in OwnedEntities.Distinct())if(EntityManager.Exists(e)&&EntityManager.HasComponent<Game.Net.Edge>(e))buffer.Add(new PlanBuiltRoad{Entity=e});
 }
 void CaptureOwnership(){
  OwnedEntities.Clear();OwnedEntities.AddRange(stagedRoads.Where(Live));OwnedEntities.AddRange(appliedTrees);OwnedEntities.AddRange(appliedBuildings);OwnedEntities.AddRange(appliedBays);OwnedEntities.AddRange(appliedPavement);
  using(var entities=temps.ToEntityArray(Allocator.Temp))foreach(var e in entities){
   if(Plans?.Draft?.VillaExistingRoadsOnly==true)continue;
   var t=EntityManager.GetComponentData<Temp>(e);
   // Existing roads (including their split/replacement previews) are never owned.
   if(t.m_Original!=Entity.Null||(t.m_Flags&(TempFlags.Delete|TempFlags.Replace|TempFlags.Combine))!=0||EntityManager.HasComponent<Owner>(e))continue;
   if(EntityManager.HasComponent<Game.Net.Edge>(e)){
    if(!EntityManager.HasComponent<Game.Net.Curve>(e)||!CompoundRoadPolicy.NewlyPlanned(RoadCurve(EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier),submittedCourses.Select(c=>RoadCurve(c.Curve)),preexistingRoadCurves))continue;
    OwnedEntities.Add(e);
   }else if(EntityManager.HasComponent<Game.Net.Node>(e))OwnedEntities.Add(e);
  }
  Trace.Event("plan.ownership.capture","roots="+OwnedEntities.Count);
 }
 bool OwnedNode(Entity node){
  if(!EntityManager.HasBuffer<Game.Net.ConnectedEdge>(node))return false;
  foreach(var e in EntityManager.GetBuffer<Game.Net.ConnectedEdge>(node,true))if(Live(e.m_Edge)&&!demolitionTargets.Contains(e.m_Edge))return false;
  return true;
 }
 bool AllowedDeletion(Entity entity){
  var seen=new HashSet<Entity>();while(entity!=Entity.Null&&EntityManager.Exists(entity)&&seen.Add(entity)){
   if(demolitionTargets.Contains(entity))return !EntityManager.HasComponent<Game.Net.Node>(entity)||OwnedNode(entity);
   if(EntityManager.HasComponent<Game.Net.Node>(entity))return OwnedNode(entity);
   if(!EntityManager.HasComponent<Owner>(entity))return false;entity=EntityManager.GetComponentData<Owner>(entity).m_Owner;
  }return false;
 }
 void SubmitDemolition(){
  foreach(var target in demolitionTargets){
   if(!Live(target))continue;if(EntityManager.HasComponent<Game.Net.Node>(target)&&!OwnedNode(target))continue;
   var definition=EntityManager.CreateEntity(typeof(CreationDefinition),typeof(Updated));definitions.Add(definition);
   EntityManager.SetComponentData(definition,new CreationDefinition{m_Original=target,m_Flags=CreationFlags.Delete});
   if(EntityManager.HasComponent<Game.Net.Edge>(target)){
    var edge=EntityManager.GetComponentData<Game.Net.Edge>(target);var c=EntityManager.GetComponentData<Game.Net.Curve>(target);
    EntityManager.AddComponentData(definition,new NetCourse{m_Curve=c.m_Bezier,m_Length=c.m_Length,m_FixedIndex=-1,m_StartPosition=new CoursePos{m_Entity=edge.m_Start,m_Position=c.m_Bezier.a,m_Rotation=Game.Net.NetUtils.GetNodeRotation(c.m_Bezier.b-c.m_Bezier.a),m_CourseDelta=0},m_EndPosition=new CoursePos{m_Entity=edge.m_End,m_Position=c.m_Bezier.d,m_Rotation=Game.Net.NetUtils.GetNodeRotation(c.m_Bezier.d-c.m_Bezier.c),m_CourseDelta=1}});
   }else if(EntityManager.HasComponent<Game.Net.Node>(target)){
    var node=EntityManager.GetComponentData<Game.Net.Node>(target);var position=new CoursePos{m_Entity=target,m_Position=node.m_Position,m_Rotation=node.m_Rotation};var end=position;end.m_CourseDelta=1;
    EntityManager.AddComponentData(definition,new NetCourse{m_Curve=new Colossal.Mathematics.Bezier4x3(node.m_Position,node.m_Position,node.m_Position,node.m_Position),m_Length=0,m_FixedIndex=-1,m_StartPosition=position,m_EndPosition=end});
   }else if(EntityManager.HasComponent<Game.Areas.Area>(target)&&EntityManager.HasBuffer<Game.Areas.Node>(target)){
    // Area deletion uses the original node buffer, as native AreaToolSystem.
    var nodes=EntityManager.GetBuffer<Game.Areas.Node>(target,true).ToNativeArray(Allocator.Temp);
    try{EntityManager.AddBuffer<Game.Areas.Node>(definition).CopyFrom(nodes);}finally{nodes.Dispose();}
   }else if(EntityManager.HasComponent<Game.Objects.Transform>(target)){
    var t=EntityManager.GetComponentData<Game.Objects.Transform>(target);
    EntityManager.AddComponentData(definition,new ObjectDefinition{m_Position=t.m_Position,m_Rotation=t.m_Rotation,m_Probability=100,m_ParentMesh=-1,m_PrefabSubIndex=-1,m_Scale=1});
   }else throw new InvalidOperationException("Unsupported compound member "+target);
  }
  Trace.Event("plan.demolition.preview","id="+demolitionPlan+" definitions="+definitions.Count);
 }
 bool ValidateDemolition(out string detail){
  int deleted=0,foreign=0,errors=0;var covered=new HashSet<Entity>();temps.CompleteDependency();
  using(var entities=temps.ToEntityArray(Allocator.Temp))foreach(var e in entities){
   var t=EntityManager.GetComponentData<Temp>(e);if(EntityManager.HasComponent<Error>(e)){errors++;TraceErrors(e);}
   if((t.m_Flags&TempFlags.Delete)==0||t.m_Original==Entity.Null)continue;deleted++;covered.Add(t.m_Original);if(!AllowedDeletion(t.m_Original))foreign++;
  }
  int missing=demolitionTargets.Count(e=>Live(e)&&!EntityManager.HasComponent<Game.Net.Node>(e)&&!covered.Contains(e));
  m_ErrorQuery.CompleteDependency();Metrics.Clear();Metrics["DeleteObjects"]=deleted;Metrics["ForeignObjects"]=foreign;Metrics["MissingObjects"]=missing;Metrics["Errors"]=errors;
  Notice=foreign>0?"AP.DemolitionForeign":"AP.DemolishHint";detail="delete="+deleted+" foreign="+foreign+" missing="+missing+" errors="+errors;
  return deleted>0&&foreign==0&&missing==0&&errors==0&&m_ErrorQuery.IsEmptyIgnoreFilter;
 }
 // Explicit recovery path: discard native preview (including foreign deletions),
 // then mark only surviving recorded compound roots for native deletion cleanup.
 int forceDemolitionStage;
 internal bool CanForceDemolition=>Demolishing&&submitted&&!committed&&!applyRequested&&forceDemolitionStage==0&&frame>=8&&!Valid;
 internal void RequestForceDemolition(){if(CanForceDemolition)forceDemolitionStage=1;}
 bool ForceNodeExclusive(Entity node,HashSet<Entity> edges){
  if(!EntityManager.HasBuffer<Game.Net.ConnectedEdge>(node))return false;
  foreach(var c in EntityManager.GetBuffer<Game.Net.ConnectedEdge>(node,true))if(Live(c.m_Edge)&&!edges.Contains(c.m_Edge))return false;
  return true;
 }
 void AdvanceForceDemolition(){
  if(forceDemolitionStage==1){ClearDefinitions();applyMode=ApplyMode.Clear;forceDemolitionStage=2;return;}
  var stored=Plans.Saved.Find(p=>p.Info.Number==demolitionPlan);
  if(stored==null){forceDemolitionStage=0;return;}
  var owned=new HashSet<Entity>(Plans.OwnedObjects(stored));
  var other=new HashSet<Entity>(Plans.Saved.Where(p=>p.Entity!=stored.Entity).SelectMany(p=>Plans.OwnedObjects(p)));
  var targets=PlanDeletionPolicy.ForceTargets(demolitionTargets,owned,other,Live).ToList();
  // Re-evaluate shared nodes against the actual final edge set before mutation.
  var edges=new HashSet<Entity>(targets.Where(e=>EntityManager.HasComponent<Game.Net.Edge>(e)));
  targets.RemoveAll(e=>EntityManager.HasComponent<Game.Net.Node>(e)&&!ForceNodeExclusive(e,edges));
  var refresh=new HashSet<Entity>();
  foreach(var e in targets)if(EntityManager.HasComponent<Game.Net.Edge>(e)){var edge=EntityManager.GetComponentData<Game.Net.Edge>(e);refresh.Add(edge.m_Start);refresh.Add(edge.m_End);}
  foreach(var node in refresh.ToList())if(EntityManager.Exists(node)&&EntityManager.HasBuffer<Game.Net.ConnectedEdge>(node))foreach(var edge in EntityManager.GetBuffer<Game.Net.ConnectedEdge>(node,true))refresh.Add(edge.m_Edge);
  foreach(var e in targets)EntityManager.AddComponent<Deleted>(e);
  foreach(var e in refresh)if(Live(e))EntityManager.AddComponent<Updated>(e);
  Trace.Event("plan.demolition.force","id="+demolitionPlan+" roots="+targets.Count+" preserved="+(demolitionTargets.Count-targets.Count));
  forceDemolitionStage=0;committed=true;frame=0;Valid=false;Plans.NativeAuditAvailable=false;
  Plans.Request("nativeScheduleDemolition",demolitionPlan.ToString());Status("AP.DemolitionApplied");Plans.Sound(PlanSound.Demolish);
 }
 void AuditDemolition(){
  var remaining=demolitionTargets.Where(Live).ToList();Trace.Event("plan.demolition.audit","id="+demolitionPlan+" remaining="+remaining.Count);
  // Retained junction nodes shared with outside roads are no longer owned.
  var stored=Plans.Saved.Find(p=>p.Info.Number==demolitionPlan);if(stored!=null&&EntityManager.HasBuffer<PlanOwnedEntity>(stored.Entity)){
   var b=EntityManager.GetBuffer<PlanOwnedEntity>(stored.Entity);for(int i=b.Length-1;i>=0;i--)if(!Live(b[i].Entity)||EntityManager.HasComponent<Game.Net.Node>(b[i].Entity)&&!OwnedNode(b[i].Entity))b.RemoveAt(i);
  }

 }
}
}


