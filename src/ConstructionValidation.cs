using System;
using System.Linq;
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
 sealed class ValidationResult {internal readonly List<Entity> Buildings=new List<Entity>();internal bool Valid;internal string Detail="";}
 struct FrontageCandidate {internal Bezier4x3 Curve;internal Game.Net.EdgeGeometry Geometry;internal CompositionFlags Flags;}
 IEnumerable<int> FrontageSnapshotSteps(SpatialLookup<FrontageCandidate> index){
  Entity[] snapshot;int visited=0;var plan=World.GetExistingSystemManaged<PlanSystem>()?.Draft;
  using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Game.Net.EdgeGeometry>(),ComponentType.ReadOnly<Game.Net.Curve>(),ComponentType.ReadOnly<Game.Buildings.ConnectedBuilding>(),ComponentType.Exclude<Deleted>()))
  using(var es=q.ToEntityArray(Allocator.Temp))snapshot=es.ToArray();
  foreach(var edge in snapshot){
   if(++visited%32==0)yield return 0;
   if(!EntityManager.Exists(edge)||EntityManager.HasComponent<Deleted>(edge)||!EntityManager.HasComponent<Game.Net.Composition>(edge))continue;
   if(EntityManager.HasComponent<Temp>(edge)&&(EntityManager.GetComponentData<Temp>(edge).m_Flags&TempFlags.Delete)!=0)continue;
   if(plan?.Villa==true&&!VillaInternalRoad(edge,plan))continue;
   var composition=EntityManager.GetComponentData<Game.Net.Composition>(edge).m_Edge;
   if(!EntityManager.HasComponent<NetCompositionData>(composition))continue;
   var flags=EntityManager.GetComponentData<NetCompositionData>(composition).m_Flags;
   if((flags.m_General&(CompositionFlags.General.Elevated|CompositionFlags.General.Tunnel))!=0)continue;
   var geometry=EntityManager.GetComponentData<Game.Net.EdgeGeometry>(edge);
   var min=new float2(float.MaxValue);var max=new float2(float.MinValue);
   foreach(var c in new[]{geometry.m_Start.m_Left,geometry.m_Start.m_Right,geometry.m_End.m_Left,geometry.m_End.m_Right}){
    min=math.min(min,math.min(math.min(c.a.xz,c.b.xz),math.min(c.c.xz,c.d.xz)));
    max=math.max(max,math.max(math.max(c.a.xz,c.b.xz),math.max(c.c.xz,c.d.xz)));
   }
   index.Add(new FrontageCandidate{Curve=EntityManager.GetComponentData<Game.Net.Curve>(edge).m_Bezier,Geometry=geometry,Flags=flags},min.x-8.4,min.y-8.4,max.x+8.4,max.y+8.4);
  }
  Trace.Event("construction.frontage.snapshot","edges="+snapshot.Length+" scans=1");
 }
 bool VillaInternalRoad(Entity edge,Plan plan){
  if(!EntityManager.Exists(edge)||!EntityManager.HasComponent<Game.Net.Curve>(edge))return false;
  if(EntityManager.HasComponent<Temp>(edge)&&EntityManager.GetComponentData<Temp>(edge).m_Original!=Entity.Null&&!plan.VillaExistingRoadsOnly)return false;
  var curve=EntityManager.GetComponentData<Game.Net.Curve>(edge).m_Bezier;var mid=MathUtils.Position(curve,.5f);var world=new Point(mid.x,mid.z);
  if(!plan.VillaExistingRoadsOnly&&!Layout.Inside(world,plan.Boundary))return false;var local=plan.Local(world);
  foreach(var route in plan.Routes){for(int i=1;i<route.Points.Count;i++)if((VillaGeometry.Nearest(local,route.Points[i-1],route.Points[i])-local).Length<1)return true;
   foreach(var c in route.Curves??new List<PlanBezier>())if((c.At(c.Parameter(local))-local).Length<1)return true;
  }return false;
 }
 bool HasFrontageCandidate(Entity building,SpatialLookup<FrontageCandidate> index){
  var data=EntityManager.GetComponentData<BuildingData>(EntityManager.GetComponentData<PrefabRef>(building).m_Prefab);
  if((data.m_Flags&Game.Prefabs.BuildingFlags.NoRoadConnection)!=0)return false;
  var front=Game.Buildings.BuildingUtils.CalculateFrontPosition(EntityManager.GetComponentData<Game.Objects.Transform>(building),data.m_LotSize.y);
  foreach(var candidate in index.At(front.x,front.z)){
   var curve=candidate.Curve;var flags=candidate.Flags;MathUtils.Distance(curve.xz,front.xz,out float t);
   var side=math.dot(MathUtils.Right(MathUtils.Tangent(curve,t).xz),front.xz-MathUtils.Position(curve,t).xz)>=0?flags.m_Right:flags.m_Left;
   if((side&(CompositionFlags.Side.Raised|CompositionFlags.Side.Lowered))!=0)continue;
   var g=candidate.Geometry;
   foreach(var boundary in new[]{g.m_Start.m_Left,g.m_Start.m_Right,g.m_End.m_Left,g.m_End.m_Right})if(MathUtils.Distance(boundary.xz,front.xz,out _) <8.4f)return true;
  }return false;
 }
 IEnumerable<int> ValidateSteps(ValidationResult result){
  failureStage=4;Metrics.Clear();SetProgress(80);
  int edges=0,errors=0,blocked=0,replacements=0;var buildings=result.Buildings;
  var candidates=new List<Tuple<Entity,Entity,Game.Objects.Transform>>();
  temps.CompleteDependency();
  using(var entities=temps.ToEntityArray(Allocator.Temp)){
   var roots=NetworkReplacementRoots(entities);
   foreach(var e in entities){var t=EntityManager.GetComponentData<Temp>(e);
    if(EntityManager.HasComponent<Error>(e)){errors++;TraceErrors(e);}
    if(t.m_Original!=Entity.Null&&(t.m_Flags&TempFlags.Delete)!=0){if(IsNetworkReplacement(t.m_Original,roots))replacements++;else blocked++;}
    if(EntityManager.HasComponent<Game.Net.Edge>(e))edges++;
    if(t.m_Original==Entity.Null&&(t.m_Flags&TempFlags.Delete)==0&&EntityManager.HasComponent<Game.Buildings.Building>(e)&&EntityManager.HasComponent<Game.Objects.Transform>(e)&&EntityManager.HasComponent<PrefabRef>(e))
     candidates.Add(Tuple.Create(e,EntityManager.GetComponentData<PrefabRef>(e).m_Prefab,EntityManager.GetComponentData<Game.Objects.Transform>(e)));
   }
   Trace.Event("construction.deletion.classified","networkReplacements="+replacements+" blocked="+blocked+" scopedRoots="+roots.Count);
   if(blocked>0)TraceBlockedDeletions(entities,blocked,roots);
  }
  Metrics["Edges"]=edges;Metrics["Errors"]=errors;Metrics["BlockedDeletions"]=blocked;Metrics["NetworkReplacements"]=replacements;
  if(blocked>0){Metrics["FailureFlags"]=1;result.Detail="Native operation would delete an existing object; blocked="+blocked+" (see construction.deletion.* log)";yield break;}
  yield return 1;
  foreach(var target in expected){Entity found=Entity.Null;foreach(var c in candidates){
   if(buildings.Contains(c.Item1)||c.Item2!=target.Item1||math.distancesq(c.Item3.m_Position,target.Item2)>=1||math.abs(math.dot(c.Item3.m_Rotation.value,target.Item3.value))<=.999)continue;
   found=c.Item1;break;
  }if(found==Entity.Null){Metrics["FailureFlags"]=2;result.Detail="Native building preview incomplete: "+buildings.Count+"/"+expected.Count;yield break;}buildings.Add(found);yield return 0;}
  failureStage=5;SetProgress(84);
  foreach(var step in ValidateTreesSteps())yield return step;
  if(appliedTrees.Count!=expectedTrees.Count){Metrics["FailureFlags"]=4;result.Detail="Native tree preview incomplete: "+appliedTrees.Count+"/"+expectedTrees.Count;yield break;}
  failureStage=6;SetProgress(88);
  foreach(var step in ValidatePavementSteps())yield return step;
  string pavement="pavement="+appliedPavement.Count+"/"+expectedPavement.Count;
  if(appliedPavement.Count!=expectedPavement.Count){Metrics["FailureFlags"]=8;result.Detail="Native pavement preview incomplete: "+pavement;yield break;}
  failureStage=7;SetProgress(92);
  foreach(var step in MatchParkingPreviewsSteps())yield return step;
  failureStage=8;SetProgress(95);
  var frontageIndex=new SpatialLookup<FrontageCandidate>(32);
  foreach(var step in FrontageSnapshotSteps(frontageIndex))yield return step;
  int frontage=0,roadCandidates=0,attached=0,parkingLinks=0,aisleCandidates=0;
  foreach(var b in buildings){if(!PreviewExists(b)){Metrics["FailureFlags"]=16;result.Detail="Native preview changed during validation; reopen preview";yield break;}
   if(EntityManager.GetComponentData<Game.Buildings.Building>(b).m_RoadEdge!=Entity.Null)frontage++;
   var road=EntityManager.GetComponentData<Game.Buildings.Building>(b).m_RoadEdge;var draft=World.GetExistingSystemManaged<PlanSystem>()?.Draft;bool external=draft?.Villa==true&&road!=Entity.Null&&!VillaInternalRoad(road,draft);
   if(!external&&HasFrontageCandidate(b,frontageIndex))roadCandidates++;yield return 0;
  }
  foreach(var bay in appliedBays){
   yield return 0;
   if(!PreviewExists(bay)||!EntityManager.HasComponent<Game.Objects.Attached>(bay))continue;
   var parent=EntityManager.GetComponentData<Game.Objects.Attached>(bay).m_Parent;
   if(!EntityManager.Exists(parent)||EntityManager.HasComponent<Deleted>(parent)||!EntityManager.HasComponent<Game.Net.Edge>(parent))continue;attached++;
   if(EntityManager.HasBuffer<Game.Net.SubLane>(parent))foreach(var lane in EntityManager.GetBuffer<Game.Net.SubLane>(parent,true))if(EntityManager.Exists(lane.m_SubLane)&&!EntityManager.HasComponent<Deleted>(lane.m_SubLane)&&EntityManager.HasComponent<Game.Net.CarLane>(lane.m_SubLane)){aisleCandidates++;break;}
   if(ParkingConnected(bay,false,out _,out _))parkingLinks++;
  }
  SetProgress(99);
  // No native error suppression, and Apply runs a fresh validation iterator.
  m_ErrorQuery.CompleteDependency();
  result.Detail="buildings="+buildings.Count+" road="+frontage+" roadCandidates="+roadCandidates+" bays="+appliedBays.Count+"/"+expectedBays.Count+" attached="+attached+" aisleCandidates="+aisleCandidates+" parkingLinks="+parkingLinks+" networkEdges="+edges+" "+pavement+" errors="+errors;
  Metrics["AttachedBays"]=attached;Metrics["Frontage"]=frontage;Metrics["ParkingLinks"]=parkingLinks;Metrics["NativeErrorEntities"]=m_ErrorQuery.CalculateEntityCount();Metrics["Trees"]=appliedTrees.Count;Metrics["Buildings"]=buildings.Count;Metrics["RoadCandidates"]=roadCandidates;Metrics["Bays"]=appliedBays.Count;Metrics["AisleCandidates"]=aisleCandidates;Metrics["Edges"]=edges;Metrics["Errors"]=errors;
  bool requireRoad=Mod.Options.RequireBuildingRoad||World.GetExistingSystemManaged<PlanSystem>()?.Draft?.Villa==true;
   Notice=requireRoad&&roadCandidates!=buildings.Count?"AP.Native.FrontageNeeded":aisleCandidates!=expectedBays.Count?"AP.Native.ParkingNeeded":"AP.Native.ParkingPending";
  Metrics["FailureFlags"]=(requireRoad&&roadCandidates!=buildings.Count?32:0)|(appliedBays.Count!=expectedBays.Count?64:0)|(attached!=expectedBays.Count||aisleCandidates!=expectedBays.Count?128:0)|(errors>0||!m_ErrorQuery.IsEmptyIgnoreFilter?256:0);
  if((Plans?.Draft?.Villa==true||Plans?.Draft?.Tower==true)&&villaStage==0&&Plans?.Draft?.VillaExistingRoadsOnly!=true){Notice="AP.RoadStageHint";result.Valid=edges>0&&errors==0&&m_ErrorQuery.IsEmptyIgnoreFilter;Metrics["FailureFlags"]=(edges==0?512:0)|(errors>0||!m_ErrorQuery.IsEmptyIgnoreFilter?256:0);yield break;}
  result.Valid=(expected.Count>0||Plans?.CompoundApplying==true&&(expectedBays.Count>0||edges>0))&&(!requireRoad||roadCandidates==buildings.Count)&&appliedBays.Count==expectedBays.Count&&attached==expectedBays.Count&&aisleCandidates==expectedBays.Count&&(Plans?.Draft?.VillaExistingRoadsOnly==true||Plans?.CompoundApplying==true&&roadCandidates==expected.Count||edges>0||villaStage>=2&&stagedRoads.Any(e=>Live(e)&&EntityManager.HasComponent<Game.Net.Edge>(e)))&&errors==0&&m_ErrorQuery.IsEmptyIgnoreFilter;
  if(!result.Valid&&Metrics["FailureFlags"]==0)Metrics["FailureFlags"]=512;
 }
 bool PreviewExists(Entity e)=>EntityManager.Exists(e)&&!EntityManager.HasComponent<Deleted>(e)&&EntityManager.HasComponent<Temp>(e)&&(EntityManager.GetComponentData<Temp>(e).m_Flags&TempFlags.Delete)==0;
}
}
