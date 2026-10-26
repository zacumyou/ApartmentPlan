using System;
using System.Collections.Generic;
using System.Linq;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace ApartmentPlan {
public partial class ConstructionTool {
 readonly List<float3[]> expectedPavement=new List<float3[]>();
 readonly List<Entity> appliedPavement=new List<Entity>();
 Entity pavementPrefab;
 readonly List<Entity> expectedSurfacePrefabs=new List<Entity>();
 IEnumerable<int> SubmitPavementSteps(PlanSystem s,IList<ConstructionCourse> graph){
  expectedPavement.Clear();expectedSurfacePrefabs.Clear();appliedPavement.Clear();pavementPrefab=Entity.Null;Entity tilePrefab=Entity.Null,grassPrefab=Entity.Null;
  var prefabSystem=World.GetOrCreateSystemManaged<PrefabSystem>();
  using(var query=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SurfaceData>(),ComponentType.ReadOnly<AreaData>()))
  using(var candidates=query.ToEntityArray(Allocator.Temp))foreach(var e in candidates)
   if(prefabSystem.TryGetPrefab<PrefabBase>(e,out var p)&&p!=null&&EntityManager.GetComponentData<AreaData>(e).m_Archetype.Valid){if(p.name=="Pavement Surface 01")pavementPrefab=e;if(p.name=="Tiles Surface 01")tilePrefab=e;if(p.name=="Grass Surface 01")grassPrefab=e;}
  if(pavementPrefab==Entity.Null)throw new InvalidOperationException("Pavement Surface 01 unavailable");
  if((s.Draft.Villa?Mod.Options.VillaTileLots:Mod.Options.TileBuildingLots)&&tilePrefab==Entity.Null)throw new InvalidOperationException("Tiles Surface 01 unavailable; disable lot tiles to preserve asset surfaces");
  // Standalone surface definitions use the native area generation/Apply path.
  // Surface layer sits beneath decals and invisible lanes, not inside lots.
  bool invisibleAisle=s.Network("aisle")?.Id.StartsWith("AP Invisible ",StringComparison.Ordinal)==true;
  void Submit(IList<Point> points,Entity surface,bool world=false){
   if(s.Draft.Tower)points=TowerGeometry.SurfaceNodes(points);if(points.Count<3)return;
   if(s.CompoundApplying&&points.Any(v=>s.ExistingCompoundPoint(world?v:s.Draft.World(v))))return;
   var corners=points.Select(v=>Ground(world?v:s.Draft.World(v))).ToArray();
   if(Layout.SignedArea(points)>0)Array.Reverse(corners);
   var e=Definition(surface);var nodes=EntityManager.AddBuffer<Game.Areas.Node>(e);
   foreach(var point in corners)nodes.Add(new Game.Areas.Node(point,0));nodes.Add(nodes[0]);
   expectedPavement.Add(corners);expectedSurfacePrefabs.Add(surface);
  }
  if(s.Draft.Tower){
   Entity FindSurface(string id){using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SurfaceData>(),ComponentType.ReadOnly<AreaData>()))using(var es=q.ToEntityArray(Allocator.Temp))foreach(var e in es)if(prefabSystem.TryGetPrefab<PrefabBase>(e,out var f)&&f!=null&&f.name==id&&EntityManager.GetComponentData<AreaData>(e).m_Archetype.Valid)return e;throw new InvalidOperationException("Selected surface unavailable: "+id);}
   var internalSurface=FindSurface(s.Draft.TowerSurface);
   foreach(var outline in SurfaceMerge.Create(s.Draft.TowerPaving)){Submit(outline,internalSurface);yield return 1;}
   if(s.Draft.TowerEntryOutlines?.Count>0){var entrySurface=FindSurface(s.Draft.TowerEntranceSurface);foreach(var outline in s.Draft.TowerEntryOutlines){Submit(outline,entrySurface);yield return 1;}}
   if(s.Draft.TowerEntryPaving.Count>0){var entrySurface=FindSurface(s.Draft.TowerEntranceSurface);foreach(var outline in SurfaceMerge.Create(s.Draft.TowerEntryPaving)){Submit(outline,entrySurface);yield return 1;}}
   if(Mod.Options.TileBuildingLots)foreach(var lot in s.Draft.Parcels.Where(r=>r.Kind==0)){Submit(lot.PlacementOutline??lot.Corners,tilePrefab);yield return 1;}
   if(Mod.Options.GrassSurfaces&&grassPrefab!=Entity.Null)foreach(var outline in (s.Draft.TowerGrassOutlines?.Count>0?s.Draft.TowerGrassOutlines:SurfaceMerge.Create(s.Draft.GrassPockets))){Submit(outline,grassPrefab);yield return 1;}
   Trace.Event("construction.towerPavement.submitted","entryPolygons="+(s.Draft.TowerEntryOutlines?.Count??0)+" entrySurface="+s.Draft.TowerEntranceSurface+" total="+expectedPavement.Count);
   yield break;
  }
  // Joined pavement stays square at internal seams; rounding individual grid
  // fragments would leave tiny holes. Tile lots retain exposed-corner rounding.
  foreach(var outline in SurfaceMerge.Create(ParkingSurfaceGeometry.Paving(s.Draft,invisibleAisle))){
   Submit(outline,pavementPrefab);yield return expectedPavement.Count%4==0?1:0;
  }
  foreach(var r in s.Draft.Parcels.Where(r=>r.Kind==0&&(s.Draft.Villa?Mod.Options.VillaTileLots:Mod.Options.TileBuildingLots))){
   Submit(s.Draft.Villa?VillaGeometry.Box(new Point(r.X+r.W/2,r.Y+r.D/2),r.PlacementForward??new Point(0,1),16,16):SurfaceOutline.Create(r,s.Draft.Parcels,Mod.Options.SurfaceCornerRadius),tilePrefab);yield return 1;
  }
  if(invisibleAisle)foreach(var apron in ParkingSurfaceGeometry.JunctionAprons(graph)){
   Submit(apron,pavementPrefab,true);yield return 0;
  }
  if(Mod.Options.GrassSurfaces&&grassPrefab!=Entity.Null)foreach(var outline in SurfaceMerge.Create(s.Draft.GrassPockets??new List<Parcel>())){
   Submit(outline,grassPrefab);yield return expectedPavement.Count%4==0?1:0;
  }
  else if(Mod.Options.GrassSurfaces&&s.Draft.GrassPockets?.Count>0)Trace.Event("construction.grassSurface.unavailable","Grass Surface 01; terrain retained");
  Trace.Event("construction.pavement.submitted","prefab=Pavement Surface 01 patches="+expectedPavement.Count);
 }
 IEnumerable<int> ValidatePavementSteps(){
  appliedPavement.Clear();
  var candidates=new List<Entity>();var matched=new HashSet<Entity>();
  using(var entities=temps.ToEntityArray(Allocator.Temp))foreach(var e in entities)
   if(EntityManager.HasComponent<Game.Areas.Area>(e)&&EntityManager.HasBuffer<Game.Areas.Node>(e)&&EntityManager.HasComponent<PrefabRef>(e)){
    var t=EntityManager.GetComponentData<Temp>(e);
    if(t.m_Original==Entity.Null&&(t.m_Flags&TempFlags.Create)!=0&&(t.m_Flags&TempFlags.Delete)==0&&(EntityManager.GetComponentData<Game.Areas.Area>(e).m_Flags&Game.Areas.AreaFlags.Complete)!=0)candidates.Add(e);
   }
  int triangles=0,empty=0;
  for(int index=0;index<expectedPavement.Count;index++){yield return 0;var target=expectedPavement[index];
   foreach(var e in candidates){
    if(matched.Contains(e)||EntityManager.GetComponentData<PrefabRef>(e).m_Prefab!=expectedSurfacePrefabs[index])continue;
    var nodes=EntityManager.GetBuffer<Game.Areas.Node>(e,true);if(nodes.Length!=target.Length)continue;
    // Native generation can rotate or reverse a closed polygon's node order.
    bool match=true;foreach(var p in target){bool found=false;for(int i=0;i<nodes.Length;i++)found|=math.distancesq(nodes[i].m_Position.xz,p.xz)<.0001f;if(!found){match=false;break;}}
    if(match){int count=EntityManager.HasBuffer<Game.Areas.Triangle>(e)?EntityManager.GetBuffer<Game.Areas.Triangle>(e,true).Length:0;triangles+=count;if(count==0)empty++;appliedPavement.Add(e);matched.Add(e);break;}
   }
  }
  if(expectedPavement.Count>0)Trace.Event("construction.pavement.geometry","matched="+appliedPavement.Count+" expected="+expectedPavement.Count+" triangles="+triangles+" emptyMeshes="+empty);
 }
}
}
