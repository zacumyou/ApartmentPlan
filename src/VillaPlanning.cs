using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Mathematics;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace ApartmentPlan {
public partial class PlanSystem {
 internal bool Villa=>Mod.Options.PlanMode==1;
 internal readonly List<VillaConnection> VillaConnections=new List<VillaConnection>();
 readonly Dictionary<int,PlanEditData> modeDrafts=new Dictionary<int,PlanEditData>();
 internal bool ToggleVillaConnection(Point point){
  if(!Villa||NativeCommitted)return false;
  var gate=VillaConnections.OrderBy(c=>(c.Boundary-point).Length).FirstOrDefault();if(gate==null||(gate.Boundary-point).Length>8)return false;
  gate.Disabled=!gate.Disabled;SyncVillaEntrances();Rebuild();Revision++;return true;
 }
 void SyncVillaEntrances(){Entrances.Clear();Entrances.AddRange(VillaConnections.Where(c=>!c.Disabled).Select(c=>c.Boundary));Entrance=Entrances.Count>0?(Point?)Entrances[0]:Boundary.Count>0?(Point?)Boundary[0]:null;}
 void UndoVillaEntrance(){
  var last=VillaConnections.LastOrDefault(c=>!c.Disabled);if(last==null)return;
  if(last.Manual)VillaConnections.Remove(last);else last.Disabled=true;
  SyncVillaEntrances();Rebuild();
 }
 void AddVillaConnection(Point point){
  if(NativeCommitted||!Layout.Valid(Boundary))return;
  var edge=SiteFrame.Nearest(Boundary,point,out _);
  if((edge-point).Length>16){Status="AP.EntranceFit";Revision++;return;}
  var old=VillaConnections.FirstOrDefault(c=>(c.Boundary-edge).Length<5);
  if(old!=null){if(old.Disabled){old.Disabled=false;SyncVillaEntrances();Rebuild();}else Status="AP.ExistingEntrance";Revision++;return;}
  DetectVillaConnections(edge);Revision++;
 }
 // Read native entities on the game thread; only immutable geometry reaches the worker.
 List<PlanRoute> CollectVillaExistingRoads(){
  var result=new List<PlanRoute>();if(!Villa||!Layout.Valid(Boundary))return result;
  var bounds=new Bounds2(new float2((float)Boundary.Min(p=>p.X),(float)Boundary.Min(p=>p.Y)),new float2((float)Boundary.Max(p=>p.X),(float)Boundary.Max(p=>p.Y)));
  using(var entities=new NativeList<Entity>(Allocator.Temp)){
   var tree=World.GetOrCreateSystemManaged<Game.Net.SearchSystem>().GetNetSearchTree(true,out var job);job.Complete();var query=new WalkRoadQuery{Bounds=bounds,Items=entities};tree.Iterate(ref query);
   var heights=World.GetOrCreateSystemManaged<Game.Simulation.TerrainSystem>().GetHeightData();var seen=new HashSet<Entity>();
   foreach(var e in entities){
    if(!seen.Add(e)||!EntityManager.Exists(e)||EntityManager.HasComponent<Deleted>(e)||EntityManager.HasComponent<Temp>(e)||EntityManager.HasComponent<Owner>(e)||!EntityManager.HasComponent<Game.Net.Curve>(e)||!EntityManager.HasComponent<PrefabRef>(e))continue;
    var choice=Networks.Find(n=>n.Entity==EntityManager.GetComponentData<PrefabRef>(e).m_Prefab&&n.Car&&n.Walk&&!n.Path);if(choice==null)continue;
    var native=EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier;
    var c=new PlanBezier{A=new Point(native.a.x,native.a.z),B=new Point(native.b.x,native.b.z),C=new Point(native.c.x,native.c.z),D=new Point(native.d.x,native.d.z)};
    var points=VillaGeometry.Sample(c,2);bool internalRoad=false,grounded=true;
    for(int i=0;i<points.Count;i++){
     var q=points[i];if(!Layout.Inside(q,Boundary))continue;
     var xyz=MathUtils.Position(native,i/(float)(points.Count-1));var ground=Game.Simulation.TerrainUtils.SampleHeight(ref heights,xyz);
     if(Math.Abs(xyz.y-ground)>2){grounded=false;break;}
     if(TowerGeometry.Distance(q,Boundary)>choice.Width/2+2)internalRoad=true;
    }
    if(grounded&&internalRoad)result.Add(new PlanRoute{Use="aisle",Width=choice.Width,Points=points,Curves=new List<PlanBezier>{c}});
   }
  }
  Trace.Event("villa.existingRoads.snapshot","roads="+result.Count);return result;
 }
 void DetectVillaConnections(Point? requested=null){
  if(!Villa||!Layout.Valid(Boundary))return;
  var previous=VillaConnections.ToArray();var offered=new List<VillaConnection>();
  if(!requested.HasValue){VillaConnections.Clear();VillaConnections.AddRange(previous.Where(c=>c.Manual&&TowerGeometry.Distance(c.Boundary,Boundary)<.1));}
  var bounds=new Bounds2(new float2((float)Boundary.Min(p=>p.X)-64,(float)Boundary.Min(p=>p.Y)-64),new float2((float)Boundary.Max(p=>p.X)+64,(float)Boundary.Max(p=>p.Y)+64));
  using(var entities=new NativeList<Entity>(Allocator.Temp)){
   var tree=World.GetOrCreateSystemManaged<Game.Net.SearchSystem>().GetNetSearchTree(true,out var job);job.Complete();var query=new WalkRoadQuery{Bounds=bounds,Items=entities};tree.Iterate(ref query);
   var heights=World.GetOrCreateSystemManaged<Game.Simulation.TerrainSystem>().GetHeightData();
   var seen=new HashSet<Entity>();
   foreach(var e in entities){if(!seen.Add(e)||!EntityManager.Exists(e)||EntityManager.HasComponent<Deleted>(e)||EntityManager.HasComponent<Temp>(e)||EntityManager.HasComponent<Owner>(e)||!EntityManager.HasComponent<Game.Net.Curve>(e)||!EntityManager.HasComponent<PrefabRef>(e))continue;
    var choice=Networks.Find(n=>n.Entity==EntityManager.GetComponentData<PrefabRef>(e).m_Prefab&&n.Car&&!n.Path);if(choice==null)continue;
    var curve=EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier;
    void Offer(float3 q,bool junction,List<Point> arms=null){
     var road=new Point(q.x,q.z);var edge=SiteFrame.Nearest(Boundary,road,out var inward);
     if((edge-road).Length>choice.Width/2+Mod.Options.VillaConnectionDistance||Layout.Inside(road,Boundary))return;
     var ground=new float3((float)edge.X,0,(float)edge.Y);ground.y=Game.Simulation.TerrainUtils.SampleHeight(ref heights,ground);if(Math.Abs(ground.y-q.y)>2)return;
     if(!Layout.Inside(edge+inward*4,Boundary))return;
     int side=Enumerable.Range(0,Boundary.Count).OrderBy(i=>(BoundarySnap.Nearest(edge,new SnapEdge(Boundary[i],Boundary[(i+1)%Boundary.Count],1))-edge).Length).First();
     if(requested.HasValue&&(edge-requested.Value).Length>8)return;
     var nearby=VillaConnections.FirstOrDefault(c=>(c.Boundary-edge).Length<Math.Max(20,choice.Width*2));
     if(!requested.HasValue&&nearby!=null){if(nearby.Manual||!junction||nearby.Junction)return;VillaConnections.Remove(nearby);}
     if(VillaConnections.Count>=64)return;
     MathUtils.Distance(curve,q,out float curveT);var tangent=MathUtils.Tangent(curve,curveT);
     (requested.HasValue?offered:VillaConnections).Add(new VillaConnection{Manual=requested.HasValue,Tangent=new Point(tangent.x,tangent.z),Arms=arms??new List<Point>(),Boundary=edge,Road=road,Side=side,Junction=junction,Disabled=previous.Any(c=>c.Disabled&&(c.Boundary-edge).Length<20)});
    }
    if(EntityManager.HasComponent<Game.Net.Edge>(e)){
     var edge=EntityManager.GetComponentData<Game.Net.Edge>(e);
     foreach(var node in new[]{edge.m_Start,edge.m_End})if(EntityManager.HasComponent<Game.Net.Node>(node)&&EntityManager.HasBuffer<Game.Net.ConnectedEdge>(node)&&EntityManager.GetBuffer<Game.Net.ConnectedEdge>(node,true).Length>=3){
      var arms=new List<Point>();foreach(var connected in EntityManager.GetBuffer<Game.Net.ConnectedEdge>(node,true)){
       var arm=connected.m_Edge;if(!EntityManager.HasComponent<Game.Net.Curve>(arm)||!EntityManager.HasComponent<Game.Net.Edge>(arm))continue;
       var shape=EntityManager.GetComponentData<Game.Net.Curve>(arm).m_Bezier;bool start=EntityManager.GetComponentData<Game.Net.Edge>(arm).m_Start==node;var direction=start?shape.b-shape.a:shape.c-shape.d;arms.Add(new Point(direction.x,direction.z));
      }Offer(EntityManager.GetComponentData<Game.Net.Node>(node).m_Position,true,arms);
     }
    }
    if(requested.HasValue){var at=new float3((float)requested.Value.X,0,(float)requested.Value.Y);MathUtils.Distance(curve,at,out float manualT);Offer(MathUtils.Position(curve,manualT),false);continue;}
    // Major boundary chains, rather than each short polygon edge, define the
    // midpoint of the adjacent road frontage. Resolve it onto a real road curve.
    foreach(var side in VillaGeometry.BoundarySides(Boundary)){var mid=VillaGeometry.ChainMidpoint(side);var at=new float3((float)mid.X,0,(float)mid.Y);MathUtils.Distance(curve,at,out float t);Offer(MathUtils.Position(curve,t),false);}
    // One candidate per boundary side/nearby road, with nearby existing junctions preferred.
    for(int i=0;i<Boundary.Count;i++){
     var mid=(Boundary[i]+Boundary[(i+1)%Boundary.Count])*.5;var at=new float3((float)mid.X,0,(float)mid.Y);MathUtils.Distance(curve,at,out float t);Offer(MathUtils.Position(curve,t),false);
    }
   }
  }
  if(requested.HasValue){
   var chosen=offered.OrderByDescending(c=>c.Junction).ThenBy(c=>(c.Boundary-requested.Value).Length).FirstOrDefault();
   if(chosen==null){Status="AP.Villa.NoNearbyRoad";return;}
   var duplicate=VillaConnections.FirstOrDefault(c=>(c.Boundary-chosen.Boundary).Length<5);
   if(duplicate!=null)duplicate.Disabled=false;else{chosen.Disabled=false;VillaConnections.Add(chosen);}
   SyncVillaEntrances();Trace.Event("villa.connection.manual","count="+VillaConnections.Count+" side="+chosen.Side);Rebuild();return;
  }
  VillaConnections.Sort((a,b)=>b.Junction.CompareTo(a.Junction));SyncVillaEntrances();
 }
 bool VillaCommand(string command,string value){
  if(command=="planMode"){
   if(!int.TryParse(value,out int mode)||mode<0||mode>2||mode==Mod.Options.PlanMode)return true;
   // Switching tabs preserves each unfinished draft, including connection exclusions.
   modeDrafts[Mod.Options.PlanMode]=new PlanEditData{Boundary=Boundary.ToList(),Entrances=Entrances.ToList(),Zones=Zones.ToList(),Ramps=Ramps.ToList(),Roads=Roads.ToList(),Draft=Draft,Selected=Selected.ToArray(),VillaConnections=VillaConnections.ToList(),NativeCommitted=NativeCommitted,EditingNumber=EditingPlan};
   CancelLayout();Mod.Options.PlanMode=mode;Command("new","");Selected.Clear();
   if(modeDrafts.TryGetValue(mode,out var d)){
    Boundary.AddRange(d.Boundary);Entrances.AddRange(d.Entrances);Zones.AddRange(d.Zones);Ramps.AddRange(d.Ramps);Roads.AddRange(d.Roads);VillaConnections.AddRange(d.VillaConnections);foreach(var id in d.Selected)Selected.Add(id);Draft=d.Draft;NativeCommitted=d.NativeCommitted;EditingPlan=d.EditingNumber;
    Entrance=Entrances.Count>0?(Point?)Entrances[0]:null;Mode=Draft!=null?"preview":Boundary.Count>=3?"entrance":"boundary";if(Draft!=null)RenderDraft();
   }else if(Villa){if(string.IsNullOrEmpty(Mod.Options.VillaSelectedAssetsJson)){foreach(var a in Assets.Where(VillaLayout.Candidate))Selected.Add(a.Id);}else foreach(var id in Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(Mod.Options.VillaSelectedAssetsJson)??new string[0])if(Assets.Any(a=>a.Id==id))Selected.Add(id);}
   else foreach(var id in Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>((Tower?Mod.Options.TowerSelectedAssetsJson:Mod.Options.SelectedAssetsJson))??new string[0])if(Assets.Any(a=>a.Id==id))Selected.Add(id);
   if(Layout.Valid(Boundary)&&!NativeCommitted)Rebuild();Mod.Options.ApplyAndSave();Revision++;return true;
  }
  if(!command.StartsWith("villa",StringComparison.Ordinal))return false;
  if(!Villa||NativeCommitted)return true;
  if(command=="villaDetect"){DetectVillaConnections();Rebuild();return true;}
  if(command=="villaToggle"&&int.TryParse(value,out int i)&&i>=0&&i<VillaConnections.Count){VillaConnections[i].Disabled=!VillaConnections[i].Disabled;SyncVillaEntrances();Rebuild();return true;}
  if(command=="villaSelect"){foreach(var a in Assets.Where(VillaLayout.Candidate))Selected.Add(a.Id);Rebuild();return true;}
  if(command=="villaCentralRoad")Mod.Options.VillaCentralRoad=!Mod.Options.VillaCentralRoad;
  else if(command=="villaFollowBoundary")Mod.Options.VillaFollowBoundary=!Mod.Options.VillaFollowBoundary;
  else if(command=="villaMesh")Mod.Options.VillaMesh=!Mod.Options.VillaMesh;
  else if(double.TryParse(value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double n)&&!double.IsNaN(n)&&!double.IsInfinity(n)){
   switch(command){case "villaGap":Mod.Options.VillaGap=(float)Math.Max(-6,Math.Min(6,n));break;case "villaReduction":Mod.Options.VillaReduction=(float)Math.Max(0,Math.Min(6,n));break;case "villaMainSpacing":Mod.Options.VillaMainSpacing=(int)Math.Max(60,Math.Min(300,n));break;case "villaRows":Mod.Options.VillaRows=(int)Math.Max(1,Math.Min(2,n));break;case "villaBoundary":Mod.Options.VillaBoundary=(float)Math.Max(0,Math.Min(12,n));break;case "villaSetback":Mod.Options.VillaSetback=(float)Math.Max(0,Math.Min(6,n));break;case "villaMaxBuildings":Mod.Options.VillaMaxBuildings=(int)Math.Max(10,Math.Min(1000,n));break;case "villaConnectionDistance":Mod.Options.VillaConnectionDistance=(float)Math.Max(1,Math.Min(8,n));DetectVillaConnections();break;}
  }
  Mod.Options.ApplyAndSave();Rebuild();return true;
 }
}
}
