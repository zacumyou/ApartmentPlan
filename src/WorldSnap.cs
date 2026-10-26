using System.Collections.Generic;
using Colossal.Collections;
using Colossal.Mathematics;
using Game.Areas;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace ApartmentPlan {
public partial class PlanTool {
 readonly List<Bezier4x3> nativeRoadCurves=new List<Bezier4x3>();
 readonly List<SnapEdge> nearby=new List<SnapEdge>();float3 cachePosition;float cacheTime=-10;int cacheMask;
 struct NearbyEntity:INativeQuadTreeIterator<Entity,QuadTreeBoundsXZ>{public Bounds2 Box;public NativeList<Entity> Items;public bool Intersect(QuadTreeBoundsXZ b)=>MathUtils.Intersect(Box,b.m_Bounds.xz);public void Iterate(QuadTreeBoundsXZ b,Entity e){if(Intersect(b))Items.Add(e);}}
 struct NearbyArea:INativeQuadTreeIterator<AreaSearchItem,QuadTreeBoundsXZ>{public Bounds2 Box;public NativeList<Entity> Items;public bool Intersect(QuadTreeBoundsXZ b)=>MathUtils.Intersect(Box,b.m_Bounds.xz);public void Iterate(QuadTreeBoundsXZ b,AreaSearchItem e){if(Intersect(b))Items.Add(e.m_Area);}}
 struct NearbyZone:INativeQuadTreeIterator<Entity,Bounds2>{public Bounds2 Box;public NativeList<Entity> Items;public bool Intersect(Bounds2 b)=>MathUtils.Intersect(Box,b);public void Iterate(Bounds2 b,Entity e){if(Intersect(b))Items.Add(e);}}
 bool Live(Entity e)=>EntityManager.Exists(e)&&!EntityManager.HasComponent<Deleted>(e)&&!EntityManager.HasComponent<Temp>(e);
 void Edge(float3 a,float3 b,int kind){if(nearby.Count>=1024||!math.all(math.isfinite(a))||!math.all(math.isfinite(b))||math.distancesq(a.xz,b.xz)<.01f||math.abs((a.y+b.y)*.5f-cachePosition.y)>12)return;var e=new SnapEdge(new Point(a.x,a.z),new Point(b.x,b.z),kind);if((BoundarySnap.Nearest(new Point(cachePosition.x,cachePosition.z),e)-new Point(cachePosition.x,cachePosition.z)).Length<=40)nearby.Add(e);}
 void Curve(Bezier4x3 c){float3 a=c.a;for(int i=1;i<=16;i++){float t=i/16f,u=1-t;float3 b=u*u*u*c.a+3*u*u*t*c.b+3*u*t*t*c.c+t*t*t*c.d;Edge(a,b,1);a=b;}}
 void Junction(EdgeNodeGeometry g){Curve(g.m_Left.m_Left);Curve(g.m_Left.m_Right);Curve(g.m_Right.m_Left);Curve(g.m_Right.m_Right);}
 void CollectWorld(float3 position){var o=Mod.Options;bool roadDrawing=World.GetExistingSystemManaged<PlanSystem>()?.Mode=="road";int mask=(roadDrawing?16:0)|(o.SnapRoads?1:0)|(o.SnapBuildings?2:0)|(o.SnapLots?4:0)|(o.SnapGrid?8:0);if(mask==cacheMask&&UnityEngine.Time.unscaledTime-cacheTime<.35f&&math.distance(position.xz,cachePosition.xz)<12)return;cachePosition=position;cacheTime=UnityEngine.Time.unscaledTime;cacheMask=mask;nearby.Clear();nativeRoadCurves.Clear();var bounds=new Bounds2(position.xz-56,position.xz+56);
  using(var entities=new NativeList<Entity>(Allocator.Temp)){
   var query=new NearbyEntity{Box=bounds,Items=entities};
   if(o.SnapRoads){var tree=World.GetOrCreateSystemManaged<Game.Net.SearchSystem>().GetNetSearchTree(true,out var job);job.Complete();tree.Iterate(ref query);foreach(var e in entities){if(!Live(e)||!EntityManager.HasComponent<Road>(e)||EntityManager.HasComponent<Owner>(e))continue;if(roadDrawing){if(EntityManager.HasComponent<Game.Net.Curve>(e)){var curve=EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier;nativeRoadCurves.Add(curve);Curve(curve);}continue;}if(EntityManager.HasComponent<EdgeGeometry>(e)){var g=EntityManager.GetComponentData<EdgeGeometry>(e);Curve(g.m_Start.m_Left);Curve(g.m_Start.m_Right);Curve(g.m_End.m_Left);Curve(g.m_End.m_Right);}if(EntityManager.HasComponent<StartNodeGeometry>(e))Junction(EntityManager.GetComponentData<StartNodeGeometry>(e).m_Geometry);if(EntityManager.HasComponent<EndNodeGeometry>(e))Junction(EntityManager.GetComponentData<EndNodeGeometry>(e).m_Geometry);}}
   entities.Clear();if(o.SnapBuildings){var tree=World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>().GetStaticSearchTree(true,out var job);job.Complete();tree.Iterate(ref query);foreach(var e in entities){if(!Live(e)||!EntityManager.HasComponent<Game.Objects.Transform>(e)||!EntityManager.HasComponent<PrefabRef>(e))continue;var prefab=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;if(!EntityManager.HasComponent<BuildingData>(prefab))continue;var lot=EntityManager.GetComponentData<BuildingData>(prefab).m_LotSize;var tr=EntityManager.GetComponentData<Game.Objects.Transform>(e);var corners=new float3[4];for(int i=0;i<4;i++){float x=(i==0||i==3?-1:1)*lot.x*4,z=(i<2?-1:1)*lot.y*4;corners[i]=tr.m_Position+math.rotate(tr.m_Rotation,new float3(x,0,z));}for(int i=0;i<4;i++)Edge(corners[i],corners[(i+1)%4],2);}}
   entities.Clear();if(o.SnapLots){var tree=World.GetOrCreateSystemManaged<Game.Areas.SearchSystem>().GetSearchTree(true,out var job);job.Complete();var aq=new NearbyArea{Box=bounds,Items=entities};tree.Iterate(ref aq);var seen=new HashSet<Entity>();foreach(var e in entities){if(!seen.Add(e)||!Live(e)||!EntityManager.HasComponent<Lot>(e)||!EntityManager.HasBuffer<Game.Areas.Node>(e))continue;var nodes=EntityManager.GetBuffer<Game.Areas.Node>(e,true);for(int i=0;i<nodes.Length;i++)Edge(nodes[i].m_Position,nodes[(i+1)%nodes.Length].m_Position,3);}}
   entities.Clear();if(o.SnapGrid){var tree=World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>().GetSearchTree(true,out var job);job.Complete();var zq=new NearbyZone{Box=bounds,Items=entities};tree.Iterate(ref zq);foreach(var e in entities){if(!Live(e)||!EntityManager.HasComponent<Game.Zones.Block>(e)||!EntityManager.HasBuffer<Game.Zones.Cell>(e))continue;var block=EntityManager.GetComponentData<Game.Zones.Block>(e);var cells=EntityManager.GetBuffer<Game.Zones.Cell>(e,true);var idx=math.clamp(Game.Zones.ZoneUtils.GetCellIndex(block,position.xz),0,block.m_Size-1);for(int y=math.max(0,idx.y-2);y<=math.min(block.m_Size.y-1,idx.y+2);y++)for(int x=math.max(0,idx.x-2);x<=math.min(block.m_Size.x-1,idx.x+2);x++){int k=x+y*block.m_Size.x;if(k<0||k>=cells.Length||(cells[k].m_State&Game.Zones.CellFlags.Visible)==0)continue;var c=Game.Zones.ZoneUtils.GetCellPosition(block,new int2(x,y));float3 along=new float3(block.m_Direction.x,0,block.m_Direction.y)*4;float3 across=new float3(-along.z,0,along.x);Edge(c-along-across,c+along-across,8);Edge(c+along-across,c+along+across,8);Edge(c+along+across,c-along+across,8);Edge(c-along+across,c-along-across,8);}}}
  }
  Trace.Sample("snap.world","edges="+nearby.Count+" mask="+mask);
 }
}
}

