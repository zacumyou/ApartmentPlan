using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;
namespace ApartmentPlan {
public partial class ConstructionTool {
 // Native NodeReductionSystem.TryJoinCurve refits Y for ground roads. Only this
 // joint-only path may use XZ coverage, and only with preserved endpoint identity
 // and a connected same-prefab ground chain contained in the two original roads.
 bool GroundRoad(Entity e){
  if(!EntityManager.HasComponent<Game.Net.Edge>(e)||!EntityManager.HasComponent<PrefabRef>(e))return false;
  var prefab=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
  if(!EntityManager.HasComponent<NetGeometryData>(prefab))return false;
  var edge=EntityManager.GetComponentData<Game.Net.Edge>(e);
  Game.Net.Elevation Height(Entity x)=>EntityManager.HasComponent<Game.Net.Elevation>(x)?EntityManager.GetComponentData<Game.Net.Elevation>(x):default;
  var flags=NetCompositionHelpers.GetElevationFlags(Height(edge.m_Start),Height(e),Height(edge.m_End),EntityManager.GetComponentData<NetGeometryData>(prefab));
  return (flags.m_General&(CompositionFlags.General.Elevated|CompositionFlags.General.Tunnel))==0&&
   (flags.m_Left&(CompositionFlags.Side.Raised|CompositionFlags.Side.Lowered))==0&&
   (flags.m_Right&(CompositionFlags.Side.Raised|CompositionFlags.Side.Lowered))==0;
 }
 Entity OriginalNode(Entity e){if(EntityManager.HasComponent<Temp>(e)){var t=EntityManager.GetComponentData<Temp>(e);if(t.m_Original!=Entity.Null)return t.m_Original;}return e;}
 bool GroundJointPreserved(Entity a,Entity b,Entity joint,List<Entity> survivors){
  if(jointDiagnosticsByEntity.TryGetValue(joint,out var diagnostic))diagnostic[2]=1;
  if(!GroundRoad(a)||!GroundRoad(b)||!EntityManager.HasComponent<Game.Net.Curve>(a)||!EntityManager.HasComponent<Game.Net.Curve>(b))return false;
  if(diagnostic!=null)diagnostic[2]=2;
  var prefab=EntityManager.GetComponentData<PrefabRef>(a).m_Prefab;
  var ca=EntityManager.GetComponentData<Game.Net.Curve>(a).m_Bezier.xz;
  var cb=EntityManager.GetComponentData<Game.Net.Curve>(b).m_Bezier.xz;
  var ea=EntityManager.GetComponentData<Game.Net.Edge>(a);var eb=EntityManager.GetComponentData<Game.Net.Edge>(b);
  var start=ea.m_Start==joint?ea.m_End:ea.m_Start;var end=eb.m_Start==joint?eb.m_End:eb.m_Start;
  var candidates=new List<Entity>();var links=new List<System.Tuple<Entity,Entity>>();
  foreach(var e in survivors){
   if(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab!=prefab||!GroundRoad(e))continue;
   var c=EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier;
   int samples=math.clamp((int)math.ceil(MathUtils.Length(c)/.25f),16,8192);bool contained=true;
   for(int i=0;i<=samples;i++){var p=MathUtils.Position(c,(float)i/samples).xz;if(math.min(MathUtils.Distance(ca,p,out _),MathUtils.Distance(cb,p,out _))>.25f){contained=false;break;}}
   if(!contained){if(diagnostic!=null)diagnostic[0]|=1024;continue;}
   var edge=EntityManager.GetComponentData<Game.Net.Edge>(e);
   // An original endpoint must also remain at its 3D position. Reusing an ID
   // with a moved endpoint does not prove preservation.
   bool Endpoint(Entity node,float3 position){var original=OriginalNode(node);return (original!=start&&original!=end)||EntityManager.HasComponent<Game.Net.Node>(original)&&math.distance(EntityManager.GetComponentData<Game.Net.Node>(original).m_Position,position)<=.25f;}
   if(!Endpoint(edge.m_Start,c.a)||!Endpoint(edge.m_End,c.d)){if(diagnostic!=null)diagnostic[0]|=2048;continue;}
   Entity Key(Entity node){var original=OriginalNode(node);return original==start||original==end?original:node;}
   candidates.Add(e);links.Add(System.Tuple.Create(Key(edge.m_Start),Key(edge.m_End)));
  }
  var component=NetworkReplacementPolicy.ConnectedComponent(start,links);
  if(!component.Contains(end)){Trace.Event("construction.failure.joint.ground","joint="+joint+" connected=false candidates="+candidates.Count);return false;}
  if(diagnostic!=null)diagnostic[2]=3;
  var connected=new List<Entity>();for(int i=0;i<candidates.Count;i++)if(component.Contains(links[i].Item1)&&component.Contains(links[i].Item2))connected.Add(candidates[i]);
  bool first=CoversRoadBySurvivors(a,connected,true),second=CoversRoadBySurvivors(b,connected,true),bridge=false;
  foreach(var e in connected)if(CoversNeighbourJoint(e,a,joint)&&CoversNeighbourJoint(e,b,joint)){bridge=true;break;}
  if(diagnostic!=null){if(first)diagnostic[0]|=128;if(second)diagnostic[0]|=256;if(bridge)diagnostic[0]|=512;}
  bool accepted=NetworkReplacementPolicy.RetainedJoint(true,true,first&&second,bridge);
  Trace.Event(accepted?"construction.deletion.groundJoint":"construction.failure.joint.ground","joint="+joint+" connected=true first="+first+" second="+second+" bridge="+bridge+" candidates="+connected.Count);
  return accepted;
 }
}
}


