using System.Collections.Generic;
using Game.Common;
using Game.Tools;
using Game.Prefabs;
using Colossal.Mathematics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace ApartmentPlan {
public partial class ConstructionTool {
 readonly Dictionary<Entity,int[]> jointDiagnosticsByEntity=new Dictionary<Entity,int[]>();
 readonly HashSet<Entity> submittedConnections=new HashSet<Entity>();
 struct SubmittedCourse {internal Entity Prefab;internal int Seed;internal Bezier4x3 Curve;}
 readonly List<SubmittedCourse> submittedCourses=new List<SubmittedCourse>();
 void RememberNetworkCourse(Entity definition,NetCourse course){
  AddConnectionEdges(course.m_StartPosition.m_Entity,submittedConnections);
  AddConnectionEdges(course.m_EndPosition.m_Entity,submittedConnections);
  var creation=EntityManager.GetComponentData<CreationDefinition>(definition);
  submittedCourses.Add(new SubmittedCourse{Prefab=creation.m_Prefab,Seed=creation.m_RandomSeed,Curve=course.m_Curve});
 }
 // CourseSplitSystem overwrites the submitted entity with the FIRST piece only.
 // Later pieces are new definitions with seed+courseIndex. Recover their endpoints
 // only with matching prefab, bounded seed range AND complete curve containment.
 void AddSplitConnections(HashSet<Entity> touched){
  using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CreationDefinition>(),ComponentType.ReadOnly<NetCourse>(),ComponentType.Exclude<Deleted>()))
  using(var es=q.ToEntityArray(Allocator.Temp))foreach(var e in es){
   var creation=EntityManager.GetComponentData<CreationDefinition>(e);
   var course=EntityManager.GetComponentData<NetCourse>(e);
   foreach(var source in submittedCourses){
    if(!NetworkReplacementPolicy.MatchesSplitIdentity(source.Seed,creation.m_RandomSeed,source.Prefab==creation.m_Prefab))continue;
    bool covered=true;for(int i=0;i<=8;i++)if(MathUtils.Distance(source.Curve.xz,MathUtils.Position(course.m_Curve.xz,i/8f),out _)>.25f){covered=false;break;}
    if(!covered)continue;
    AddConnectionEdges(course.m_StartPosition.m_Entity,touched);AddConnectionEdges(course.m_EndPosition.m_Entity,touched);break;
   }
  }
 }
 HashSet<Entity> NetworkReplacementRoots(NativeArray<Entity> entities){
  jointDiagnosticsByEntity.Clear();
  var touched=new HashSet<Entity>(submittedConnections);
  AddSplitConnections(touched);
  // Stage-one definitions are gone, but the committed road identities remain.
  // These are anchors, not blanket permission to delete the roads.
  var staged=new HashSet<Entity>();
  if(villaStage>=2)foreach(var road in stagedRoads)if(Live(road)&&EntityManager.HasComponent<Game.Net.Edge>(road)){staged.Add(road);touched.Add(road);}
  foreach(var definition in definitions)if(EntityManager.Exists(definition)&&EntityManager.HasComponent<NetCourse>(definition)){
   var course=EntityManager.GetComponentData<NetCourse>(definition);
   AddConnectionEdges(course.m_StartPosition.m_Entity,touched);
   AddConnectionEdges(course.m_EndPosition.m_Entity,touched);
  }
  var roots=new HashSet<Entity>();var nodes=new HashSet<Entity>();
  foreach(var e in entities){var t=EntityManager.GetComponentData<Temp>(e);
   if(!touched.Contains(t.m_Original)||!EntityManager.HasComponent<Game.Net.Edge>(e)||!EntityManager.HasComponent<Game.Net.Edge>(t.m_Original))continue;
   if(staged.Contains(t.m_Original)){
    bool retained=(t.m_Flags&TempFlags.Delete)==0&&EntityManager.HasComponent<PrefabRef>(e)&&EntityManager.HasComponent<PrefabRef>(t.m_Original)&&EntityManager.GetComponentData<PrefabRef>(e).m_Prefab==EntityManager.GetComponentData<PrefabRef>(t.m_Original).m_Prefab&&EntityManager.HasComponent<Game.Net.Curve>(e)&&EntityManager.HasComponent<Game.Net.Curve>(t.m_Original)&&CoversRemovedRoad(e,t.m_Original);
    if(!NetworkReplacementPolicy.SeedRetainedRoad(roots,staged,t.m_Original,retained))continue;
   }else roots.Add(t.m_Original);
   var edge=EntityManager.GetComponentData<Game.Net.Edge>(t.m_Original);nodes.Add(edge.m_Start);nodes.Add(edge.m_End);
  }
  // Connecting at a junction also regenerates markings/props on the next road.
  // Permit those children only when the original road survives in full with the
  // same prefab, and shares an original node with an explicitly touched road.
  var touchedNodes=new HashSet<Entity>();
  foreach(var road in touched)if(EntityManager.HasComponent<Game.Net.Edge>(road)){var edge=EntityManager.GetComponentData<Game.Net.Edge>(road);touchedNodes.Add(edge.m_Start);touchedNodes.Add(edge.m_End);}
  foreach(var e in entities){var t=EntityManager.GetComponentData<Temp>(e);var original=t.m_Original;
   if(original==Entity.Null||(t.m_Flags&TempFlags.Delete)!=0||!EntityManager.HasComponent<Game.Net.Edge>(e)||!EntityManager.HasComponent<Game.Net.Edge>(original)||!EntityManager.HasComponent<Game.Net.Curve>(e)||!EntityManager.HasComponent<Game.Net.Curve>(original)||!EntityManager.HasComponent<PrefabRef>(e)||!EntityManager.HasComponent<PrefabRef>(original))continue;
   var edge=EntityManager.GetComponentData<Game.Net.Edge>(original);
   bool adjacent=touchedNodes.Contains(edge.m_Start)||touchedNodes.Contains(edge.m_End);
   bool same=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab==EntityManager.GetComponentData<PrefabRef>(original).m_Prefab;
   if(!NetworkReplacementPolicy.RetainedNeighbour(adjacent,same,CoversRemovedRoad(e,original)))continue;
   roots.Add(original);nodes.Add(edge.m_Start);nodes.Add(edge.m_End);
   Trace.Event("construction.deletion.retainedNeighbour","original="+original+" preview="+e+" fullCurveRetained=true");
  }
  // A fully split connection road may have NO Temp carrying its original ID.
  // Keep connection evidence separate from deletion permission: an anchor alone
  // never authorizes deletion, only a geometrically proved native merge does.
  AddCombinedNetworkRoots(entities,roots,touched);
  AddRetainedJointRoots(entities,roots,touched);

  Trace.Event("construction.deletion.scope","submittedConnections="+submittedConnections.Count+" courses="+submittedCourses.Count+" staged="+staged.Count+" touched="+touched.Count+" roots="+roots.Count);
  // Permit a junction node only if native preview retains a node at that position.
  foreach(var node in nodes){
   if(!EntityManager.Exists(node)||!EntityManager.HasComponent<Game.Net.Node>(node))continue;
   var position=EntityManager.GetComponentData<Game.Net.Node>(node).m_Position;
   foreach(var e in entities){if(!EntityManager.HasComponent<Game.Net.Node>(e))continue;
    var t=EntityManager.GetComponentData<Temp>(e);if((t.m_Flags&TempFlags.Delete)!=0)continue;
    if(math.distancesq(EntityManager.GetComponentData<Game.Net.Node>(e).m_Position,position)>.01f)continue;
    roots.Add(node);break;
   }
  }
  return roots;
 }
 // Handle joint-only reduction: no deleted edge Temp exists to seed ExpandMerges.
 // Require full 3D road preservation and one same-prefab survivor spanning BOTH
 // original arms. Merely preserving two disconnected halves is insufficient.
 void AddRetainedJointRoots(NativeArray<Entity> entities,HashSet<Entity> roots,HashSet<Entity> anchors){
  var survivors=new List<Entity>();
  foreach(var e in entities){var t=EntityManager.GetComponentData<Temp>(e);
   if((t.m_Flags&TempFlags.Delete)==0&&EntityManager.HasComponent<Game.Net.Edge>(e)&&EntityManager.HasComponent<Game.Net.Curve>(e)&&EntityManager.HasComponent<PrefabRef>(e))survivors.Add(e);
  }
  var coverage=new Dictionary<Entity,bool>();var seen=new HashSet<Entity>();int jointDiagnostics=0;
  foreach(var e in entities){var t=EntityManager.GetComponentData<Temp>(e);var joint=t.m_Original;
   if((t.m_Flags&TempFlags.Delete)==0||joint==Entity.Null||roots.Contains(joint)||!seen.Add(joint)||!EntityManager.HasComponent<Game.Net.Node>(joint)||!EntityManager.HasBuffer<Game.Net.ConnectedEdge>(joint))continue;
   var roads=new HashSet<Entity>();foreach(var link in EntityManager.GetBuffer<Game.Net.ConnectedEdge>(joint,true))if(Live(link.m_Edge))roads.Add(link.m_Edge);
   var diagnostic=new int[]{0,survivors.Count,0};jointDiagnosticsByEntity[joint]=diagnostic;
   if(roads.Count!=2)continue;diagnostic[0]|=1;var pair=new List<Entity>(roads);var a=pair[0];var b=pair[1];
   bool direct=anchors.Contains(a)||anchors.Contains(b);
   bool scoped=NetworkReplacementPolicy.JointScope(direct,roots.Contains(a)||roots.Contains(b));
   if(direct)diagnostic[0]|=2;if(scoped)diagnostic[0]|=4;
   if(!scoped){if(jointDiagnostics++<24)Trace.Event("construction.failure.joint.scope","joint="+joint+" direct=false verifiedRoad=false");continue;}
   if(!EntityManager.HasComponent<PrefabRef>(a)||!EntityManager.HasComponent<PrefabRef>(b))continue;
   diagnostic[0]|=8;
   var prefab=EntityManager.GetComponentData<PrefabRef>(a).m_Prefab;
   if(EntityManager.GetComponentData<PrefabRef>(b).m_Prefab!=prefab){if(jointDiagnostics++<24)Trace.Event("construction.failure.joint.prefab","joint="+joint);continue;}
   diagnostic[0]|=16;
   bool full=true;foreach(var road in pair){if(!coverage.TryGetValue(road,out bool retained)){retained=CoversRoadBySurvivors(road,survivors);coverage[road]=retained;}full&=retained;}
   if(coverage[a])diagnostic[0]|=32;if(coverage[b])diagnostic[0]|=64;
   if(!full&&GroundJointPreserved(a,b,joint,survivors)){roots.Add(joint);continue;}
   if(!full){if(jointDiagnostics++<24)Trace.Event("construction.failure.joint.coverage","joint="+joint+" direct="+direct+" first="+coverage[a]+" second="+coverage[b]+" survivors="+survivors.Count);continue;}bool bridged=false;
   foreach(var survivor in survivors)if(EntityManager.GetComponentData<PrefabRef>(survivor).m_Prefab==prefab&&CoversNeighbourJoint(survivor,a,joint)&&CoversNeighbourJoint(survivor,b,joint)){bridged=true;break;}
   if(!NetworkReplacementPolicy.RetainedJoint(true,scoped,full,bridged)){if(jointDiagnostics++<24)Trace.Event("construction.failure.joint.bridge","joint="+joint+" fullCoverage=true");continue;}
   roots.Add(joint);Trace.Event("construction.deletion.retainedJoint","joint="+joint+" bothRoadsPreserved=true bridged=true");
  }
 }
 // NodeReductionSystem combines collinear edges: one survivor receives Combine,
 // while its neighbour AND their degree-two joint become Delete|Hidden.
 // Those originals need not occur in our own CoursePos endpoints.
 void AddCombinedNetworkRoots(NativeArray<Entity> entities,HashSet<Entity> roots,HashSet<Entity> anchors){
  var combined=new List<Entity>();var removed=new List<Entity>();var deletedNodes=new HashSet<Entity>();
  foreach(var e in entities){var t=EntityManager.GetComponentData<Temp>(e);
   if(EntityManager.HasComponent<Game.Net.Node>(e)&&(t.m_Flags&TempFlags.Delete)!=0)deletedNodes.Add(t.m_Original);
   if(!EntityManager.HasComponent<Game.Net.Edge>(e)||!EntityManager.HasComponent<Game.Net.Curve>(e)||!EntityManager.HasComponent<PrefabRef>(e))continue;
   if((t.m_Flags&TempFlags.Delete)!=0){if(t.m_Original!=Entity.Null)removed.Add(e);}
   else combined.Add(e); // Building-stage regeneration may retain Original without Combine.
  }
  var neighbourCoverage=new Dictionary<Entity,bool>();
  var candidates=new List<NetworkReplacementPolicy.Merge<Entity>>();var unique=new HashSet<NetworkReplacementPolicy.Merge<Entity>>();
  foreach(var deleted in removed){var original=EntityManager.GetComponentData<Temp>(deleted).m_Original;
   if(!EntityManager.HasComponent<Game.Net.Edge>(original))continue;
   var edge=EntityManager.GetComponentData<Game.Net.Edge>(original);
   foreach(var joint in new[]{edge.m_Start,edge.m_End}){
    if(!deletedNodes.Contains(joint)||!EntityManager.HasBuffer<Game.Net.ConnectedEdge>(joint))continue;
    var neighbours=EntityManager.GetBuffer<Game.Net.ConnectedEdge>(joint,true);Entity other=Entity.Null;int degree=0;
    foreach(var n in neighbours)if(Live(n.m_Edge)){degree++;if(n.m_Edge!=original)other=n.m_Edge;}
    if(degree!=2||other==Entity.Null)continue;
    foreach(var survivor in combined){var st=EntityManager.GetComponentData<Temp>(survivor);
     bool same=EntityManager.GetComponentData<PrefabRef>(survivor).m_Prefab==EntityManager.GetComponentData<PrefabRef>(deleted).m_Prefab;
     bool covered=same&&CoversRemovedRoad(survivor,deleted);
     // Native reduction can keep a NEW split edge; it then has no Original and
     // no Combine flag. Prove that it also contains a piece of the scoped neighbour.
     bool neighbourPreserved=false;
     if(covered&&st.m_Original==other){
      if(!neighbourCoverage.TryGetValue(other,out neighbourPreserved)){neighbourPreserved=CoversRoadBySurvivors(other,combined);neighbourCoverage[other]=neighbourPreserved;}
     }
     bool newSurvivor=covered&&NetworkReplacementPolicy.VerifiedSurvivor(st.m_Original==Entity.Null,st.m_Original==other,
      CoversNeighbourJoint(survivor,other,joint),neighbourPreserved);
     if(covered)Trace.Event("construction.deletion.mergeEvidence","removed="+original+" joint="+joint+" neighbour="+other+" survivor="+survivor+" original="+st.m_Original+" neighbourPreserved="+neighbourPreserved+" verified="+newSurvivor);
     if(covered){var candidate=new NetworkReplacementPolicy.Merge<Entity>{Removed=original,Joint=joint,Neighbour=other,Survivor=st.m_Original,Verified=true,NewSurvivorVerified=newSurvivor};if(unique.Add(candidate))candidates.Add(candidate);}
    }
   }
  }
  NetworkReplacementPolicy.ExpandMerges(roots,candidates,anchors);
 }

 // A connection may split the long neighbour while merging its short end into
 // the removed edge. Require coverage by all surviving pieces, not one piece
 // that cannot possibly contain the whole original neighbour after that split.
 bool CoversRoadBySurvivors(Entity original,List<Entity> survivors,bool groundProjection=false){
  if(!EntityManager.HasComponent<Game.Net.Curve>(original)||!EntityManager.HasComponent<PrefabRef>(original))return false;
  var source=EntityManager.GetComponentData<Game.Net.Curve>(original).m_Bezier;
  var prefab=EntityManager.GetComponentData<PrefabRef>(original).m_Prefab;
  var curves=new List<Bezier4x3>();
  foreach(var e in survivors)if(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab==prefab)curves.Add(EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier);
  return RoadCurveCoverage.Covers(source,curves,groundProjection);
 }

 bool CoversNeighbourJoint(Entity survivor,Entity neighbour,Entity joint){
  if(!EntityManager.HasComponent<Game.Net.Curve>(neighbour)||!EntityManager.HasComponent<Game.Net.Edge>(neighbour))return false;
  var edge=EntityManager.GetComponentData<Game.Net.Edge>(neighbour);var curve=EntityManager.GetComponentData<Game.Net.Curve>(neighbour).m_Bezier.xz;
  var target=EntityManager.GetComponentData<Game.Net.Curve>(survivor).m_Bezier.xz;
  // A split survivor need only retain the joint's local connection, not 10%
  // of a potentially hundreds-of-metres-long neighbouring edge.
  float run=math.min(.1f,1f/math.max(1f,MathUtils.Length(EntityManager.GetComponentData<Game.Net.Curve>(neighbour).m_Bezier)));
  for(int i=0;i<=4;i++){float t=i*run/4;if(edge.m_End==joint)t=1-t;else if(edge.m_Start!=joint)return false;
   if(MathUtils.Distance(target,MathUtils.Position(curve,t),out _)>.25f)return false;
  }return true;
 }

 bool CoversRemovedRoad(Entity survivor,Entity removed){
  var target=EntityManager.GetComponentData<Game.Net.Curve>(survivor).m_Bezier.xz;
  var source=EntityManager.GetComponentData<Game.Net.Curve>(removed).m_Bezier.xz;
  // Native joining already checks tangent/prefab/elevation compatibility. Check
  // horizontal coverage independently; terrain slope may be refitted by native code.
  for(int i=0;i<=8;i++)if(MathUtils.Distance(target,MathUtils.Position(source,i/8f),out _)>.25f)return false;
  return true;
 }
 void AddConnectionEdges(Entity e,HashSet<Entity> edges){
  if(e==Entity.Null||!EntityManager.Exists(e))return;
  if(EntityManager.HasComponent<Game.Net.Edge>(e)){edges.Add(e);return;}
  if(EntityManager.HasComponent<Game.Net.Node>(e)&&EntityManager.HasBuffer<Game.Net.ConnectedEdge>(e))
   foreach(var edge in EntityManager.GetBuffer<Game.Net.ConnectedEdge>(e,true))edges.Add(edge.m_Edge);
 }
 bool IsNetworkReplacement(Entity original,HashSet<Entity> roots)=>NetworkReplacementPolicy.Allows(original,roots,
  e=>e!=Entity.Null&&EntityManager.Exists(e),e=>EntityManager.HasComponent<Game.Buildings.Building>(e),
  e=>EntityManager.HasComponent<Owner>(e)?EntityManager.GetComponentData<Owner>(e).m_Owner:Entity.Null);
}
}


