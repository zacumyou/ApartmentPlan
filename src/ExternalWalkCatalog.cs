using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Collections;
using Colossal.Mathematics;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace ApartmentPlan {
public partial class PlanSystem {
 struct WalkRoadQuery:INativeQuadTreeIterator<Entity,QuadTreeBoundsXZ>{
  public Bounds2 Bounds;public NativeList<Entity> Items;
  public bool Intersect(QuadTreeBoundsXZ b)=>MathUtils.Intersect(Bounds,b.m_Bounds.xz);
  public void Iterate(QuadTreeBoundsXZ b,Entity e){if(Intersect(b))Items.Add(e);}
 }
 // Snapshot real neighbouring roads once per recalculation, before worker work.
 List<ExternalWalkTarget> CollectExternalWalkTargets(float pathWidth){
  var result=new List<ExternalWalkTarget>();if(Boundary.Count<3)return result;
  var bounds=new Bounds2(new float2((float)Boundary.Min(p=>p.X)-64,(float)Boundary.Min(p=>p.Y)-64),new float2((float)Boundary.Max(p=>p.X)+64,(float)Boundary.Max(p=>p.Y)+64));
  using(var entities=new NativeList<Entity>(Allocator.Temp)){
   var tree=World.GetOrCreateSystemManaged<Game.Net.SearchSystem>().GetNetSearchTree(true,out var job);job.Complete();
   var query=new WalkRoadQuery{Bounds=bounds,Items=entities};tree.Iterate(ref query);
   var roads=new List<Tuple<Bezier4x3,float>>();var seen=new HashSet<Entity>();
   var heights=World.GetOrCreateSystemManaged<Game.Simulation.TerrainSystem>().GetHeightData();
   foreach(var e in entities){if(!seen.Add(e)||!EntityManager.Exists(e)||EntityManager.HasComponent<Deleted>(e)||EntityManager.HasComponent<Temp>(e)||EntityManager.HasComponent<Owner>(e)||!EntityManager.HasComponent<Game.Net.Curve>(e)||!EntityManager.HasComponent<PrefabRef>(e))continue;
    var prefab=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;var choice=Networks.Find(n=>n.Entity==prefab&&n.Walk&&!n.Path);if(choice==null)continue;
    roads.Add(Tuple.Create(EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier,choice.Width));
   }
   double winding=Layout.SignedArea(Boundary)>0?1:-1;
   for(int i=0;i<Boundary.Count&&result.Count<48;i++){
    var a=Boundary[i];var d=Boundary[(i+1)%Boundary.Count]-a;double length=d.Length;var normal=new Point(-d.Y/length*winding,d.X/length*winding);
    int count=Math.Max(1,(int)Math.Ceiling(length/40));
    for(int j=0;j<count&&result.Count<48;j++){
     var boundary=a+d*((j+.5)/count);var inside=boundary+normal*(pathWidth+3);
     if(!Layout.Inside(inside,Boundary)||Entrances.Any(p=>(p-boundary).Length<24))continue;
     var p=new float3((float)boundary.X,0,(float)boundary.Y);p.y=Game.Simulation.TerrainUtils.SampleHeight(ref heights,p);
     float best=float.MaxValue;float3 end=default;
     foreach(var road in roads){MathUtils.Distance(road.Item1,p,out float t);var q=MathUtils.Position(road.Item1,t);float distance=math.distance(p.xz,q.xz);
      // Only roads directly alongside the boundary, at compatible ground level.
      if(distance>road.Item2/2+3||distance>=best||math.abs(q.y-p.y)>2||Layout.Inside(new Point(q.x,q.z),Boundary))continue;
      best=distance;var toward=math.normalizesafe(p.xz-q.xz);end=q;end.xz+=toward*(road.Item2/2+1);
     }
     if(best<float.MaxValue)result.Add(new ExternalWalkTarget{Boundary=boundary,Inside=inside,Road=new Point(end.x,end.z)});
    }
   }
  }return result;
 }
}
}
