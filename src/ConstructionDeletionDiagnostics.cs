using System;
using System.Collections.Generic;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace ApartmentPlan {
public partial class ConstructionTool {
 // Read-only, bounded diagnostics using the same ownership policy as validation.
 // The Temp query is global; these records do not assert ownership by this tool.
 void TraceBlockedDeletions(NativeArray<Entity> entities,int blocked,HashSet<Entity> replacementRoots){
  try{
   int total=0,written=0;
   foreach(var e in entities){var t=EntityManager.GetComponentData<Temp>(e);if(t.m_Original!=Entity.Null&&(t.m_Flags&TempFlags.Delete)!=0)total++;}
   Trace.Event("construction.failure.deletion.summary","blocked="+blocked+" deletionTemps="+total+" allTemps="+entities.Length+" definitions="+definitions.Count+" limit=24 scope=globalTempQuery");
   // Prioritize the objects which actually trigger the veto, then road edits.
   for(int pass=0;pass<2;pass++)foreach(var e in entities){
    var t=EntityManager.GetComponentData<Temp>(e);
    if(t.m_Original==Entity.Null||(t.m_Flags&TempFlags.Delete)==0)continue;
    bool allowed=IsNetworkReplacement(t.m_Original,replacementRoots);if(allowed!=(pass==1)||written>=24)continue;
    written++;
    Trace.Event("construction.failure.deletion.object","blocked="+!allowed+" temp={"+DescribeDeletionEntity(e)+"} original={"+DescribeDeletionEntity(t.m_Original)+"}");
    TraceDeletionEdges(e);TraceDeletionEdges(t.m_Original);
    int related=0;
    foreach(var other in entities){if(other==e)continue;var state=EntityManager.GetComponentData<Temp>(other);if(state.m_Original!=t.m_Original)continue;
     if(related++<8)Trace.Event("construction.failure.deletion.related","original="+t.m_Original+" temp={"+DescribeDeletionEntity(other)+"}");
    }
    if(related>8)Trace.Event("construction.failure.deletion.related.omitted","original="+t.m_Original+" count="+(related-8));
   }
   if(total>written)Trace.Event("construction.failure.deletion.omitted","count="+(total-written));
  }catch(Exception error){Trace.Failure("construction.deletion.diagnostics.failed",error);}
 }
 string DescribeDeletionEntity(Entity e){
  if(e==Entity.Null||!EntityManager.Exists(e))return e+" missing";
  var parts=new List<string>{"entity="+e};
  if(EntityManager.HasComponent<Temp>(e)){var t=EntityManager.GetComponentData<Temp>(e);parts.Add("flags="+t.m_Flags+" original="+t.m_Original);}
  if(EntityManager.HasComponent<PrefabRef>(e)){
   var prefab=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;parts.Add("prefabEntity="+prefab);
   if(World.GetOrCreateSystemManaged<PrefabSystem>().TryGetPrefab<PrefabBase>(prefab,out var value))parts.Add("prefab="+Trace.Clean(value?.name,200));
  }
  if(EntityManager.HasComponent<Owner>(e))parts.Add("owner="+EntityManager.GetComponentData<Owner>(e).m_Owner);
  if(EntityManager.HasComponent<Game.Objects.Transform>(e))parts.Add("position="+EntityManager.GetComponentData<Game.Objects.Transform>(e).m_Position);
  if(EntityManager.HasComponent<Game.Net.Node>(e))parts.Add("nodePosition="+EntityManager.GetComponentData<Game.Net.Node>(e).m_Position);
  if(EntityManager.HasComponent<Game.Net.Edge>(e)){var edge=EntityManager.GetComponentData<Game.Net.Edge>(e);parts.Add("start="+edge.m_Start+" end="+edge.m_End);}
  if(EntityManager.HasComponent<Game.Net.Curve>(e)){var curve=EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier;parts.Add("curve="+curve.a+"->"+curve.d);}
  if(EntityManager.HasComponent<Game.Buildings.Building>(e))parts.Add("roadEdge="+EntityManager.GetComponentData<Game.Buildings.Building>(e).m_RoadEdge);
  return string.Join(" ",parts);
 }
 void TraceDeletionEdges(Entity node){
  if(node==Entity.Null||!EntityManager.Exists(node)||!EntityManager.HasBuffer<Game.Net.ConnectedEdge>(node))return;
  var edges=EntityManager.GetBuffer<Game.Net.ConnectedEdge>(node,true);
  for(int i=0;i<edges.Length&&i<8;i++)Trace.Event("construction.failure.deletion.connectedEdge","node="+node+" edge={"+DescribeDeletionEntity(edges[i].m_Edge)+"}");
  if(edges.Length>8)Trace.Event("construction.failure.deletion.edges.omitted","node="+node+" count="+(edges.Length-8));
 }
}
}
