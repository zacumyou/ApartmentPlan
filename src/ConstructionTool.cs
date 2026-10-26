using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Mathematics;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
namespace ApartmentPlan {
// Native preview -> native error checks -> explicit Apply. No prefab access flag
// patches and no direct cloning of city entities or suppression of notifications.
public partial class ConstructionTool:ToolBaseSystem {
 public override string toolID=>"ApartmentPlan.Construction";
 public override PrefabBase GetPrefab()=>null;
 public override bool TrySetPrefab(PrefabBase prefab)=>false;
 readonly List<Entity> definitions=new List<Entity>();
 readonly List<Entity> appliedBuildings=new List<Entity>();
 readonly List<Entity> appliedBays=new List<Entity>();
 readonly List<Tuple<Entity,float3,quaternion>> expectedBays=new List<Tuple<Entity,float3,quaternion>>();
 readonly List<Tuple<Entity,float3,quaternion>> expected=new List<Tuple<Entity,float3,quaternion>>();
 readonly List<Tuple<Entity,float3,float3,float>> bayDefinitions=new List<Tuple<Entity,float3,float3,float>>();
 EntityQuery temps;int frame,attachmentPass,surfacePass;bool submitted,applyRequested,committed;
 internal int RunGeneration;bool demolitionPrompted;
 internal bool Busy=>!committed&&(forceDemolitionStage>0||villaStage==1||villaStage==2||!submitted||previewWork!=null||applyRequested||previewRefreshAt>0);
 internal int Progress {get;private set;}
 void SetProgress(int value){value=Math.Max(Progress,Math.Min(100,value));if(value==Progress)return;Progress=value;if(Plans!=null)Plans.Revision++;}
 void ResetProgress(){Progress=0;if(Plans!=null)Plans.Revision++;}
 internal bool Valid;internal string Report="";
 internal readonly Dictionary<string,int> Metrics=new Dictionary<string,int>();internal string Notice="";
 PlanSystem Plans=>World.GetExistingSystemManaged<PlanSystem>();
 protected override void OnCreate(){base.OnCreate();temps=GetEntityQuery(ComponentType.ReadOnly<Temp>(),ComponentType.Exclude<Deleted>());}
 protected override void OnStartRunning(){base.OnStartRunning();RunGeneration++;forceDemolitionStage=0;preexistingRoadCurves.Clear();ResetProgress();demolitionPrompted=false;villaStage=0;roadWaitFrames=roadStableFrames=0;roadSignature=-1;stagedRoads.Clear();frame=0;attachmentPass=0;surfacePass=0;submitted=false;applyRequested=false;committed=false;Valid=false;Report="";Notice="";Metrics.Clear();expectedPavement.Clear();appliedPavement.Clear();pavementPrefab=Entity.Null;expectedTrees.Clear();appliedTrees.Clear();expected.Clear();expectedBays.Clear();bayDefinitions.Clear();appliedBuildings.Clear();appliedBays.Clear();OwnedEntities.Clear();previewRefreshAt=0;StartDemolitionRequest();}
 protected override void OnStopRunning(){RunGeneration++;forceDemolitionStage=0;previewRefreshAt=0;ClearDefinitions();applyMode=ApplyMode.Clear;Valid=false;base.OnStopRunning();}
 internal void Apply(){if(Valid&&!committed){ResetProgress();applyRequested=true;}}
 void ClearDefinitions(){deferDefinitions=false;previewWork?.Dispose();previewWork=null;validation=null;foreach(var e in definitions)if(EntityManager.Exists(e))EntityManager.DestroyEntity(e);definitions.Clear();submittedConnections.Clear();submittedCourses.Clear();}
 void Status(string key,string detail=""){var s=Plans;if(s==null)return;if(key=="AP.NativeBlocked"&&s.Status!=key)s.Sound(PlanSound.Fail);s.Status=key;Report=detail;s.Revision++;Trace.Event("construction."+key,detail);}
 FrameWork previewWork;ValidationResult validation;
 bool AdvanceWork(){
  if(!previewWork.Step())return false;
  Trace.Event("construction.work","slices="+previewWork.Slices+" cpuMs="+previewWork.CpuMs.ToString("F1")+" maxSliceMs="+previewWork.MaxSliceMs.ToString("F1"));previewWork=null;return true;
 }
 protected override JobHandle OnUpdate(JobHandle deps){
  applyMode=ApplyMode.None;var s=Plans;if(s==null||!s.Ready)return deps;
  try{
   if(forceDemolitionStage>0){if(!deps.IsCompleted)return deps;deps.Complete();AdvanceForceDemolition();return deps;}
   if(villaStage==1){if(!deps.IsCompleted)return deps;deps.Complete();AdvanceRoadStage();return deps;}
   if(committed){if(frame<120)frame++;if(frame==120){if(!deps.IsCompleted)return deps;deps.Complete();frame++;if(Demolishing)AuditDemolition();else Audit();}return deps;}
   if(previewRefreshAt>0){if(UnityEngine.Time.realtimeSinceStartupAsDouble<previewRefreshAt||!deps.IsCompleted)return deps;deps.Complete();if(AdvanceLandscapePreview())return deps;}
   if(!submitted){
    if(frame++==0){if(!Demolishing)EnablePreviewAnarchy();applyMode=ApplyMode.Clear;return deps;}
    if(!deps.IsCompleted)return deps;deps.Complete();
    if(Demolishing){if(previewWork==null)previewWork=new FrameWork(SubmitDemolition());if(!AdvanceWork())return deps;submitted=true;frame=0;Status("AP.NativeChecking");return deps;}
    if(s.Draft==null)throw new InvalidOperationException("Draft unavailable");
    if(previewWork==null){validation=new ValidationResult();previewWork=new FrameWork(PreparePreview(s,validation));Status("AP.NativeChecking");}
    if(!AdvanceWork())return deps;
    submitted=true;frame=8;Valid=validation.Valid;if(villaStage==2){villaStage=3;applyRequested=Valid;if(!Valid)Notice="AP.RoadStagePartial";}if(Valid)SetProgress(100);Status(Valid?"AP.NativeReady":"AP.NativeBlocked",validation.Detail);return deps;
   }
   if(!Demolishing&&!applyRequested)return deps;
   if(Demolishing&&++frame<8)return deps;
   if(Demolishing&&frame!=8&&!applyRequested)return deps;
   if(!deps.IsCompleted){if(Demolishing)frame=7;return deps;}deps.Complete();
   List<Entity> buildings;string detail;
   if(Demolishing){buildings=new List<Entity>();Valid=ValidateDemolition(out detail);}
   else{
    if(previewWork==null){Valid=false;validation=new ValidationResult();previewWork=new FrameWork(ValidateSteps(validation));Status("AP.NativeChecking");}
    if(!AdvanceWork())return deps;
    buildings=validation.Buildings;detail=validation.Detail;Valid=validation.Valid;
   }
   if(Valid)SetProgress(100);
   Status(Valid?(Demolishing?"AP.DemolitionReady":"AP.NativeReady"):"AP.NativeBlocked",detail);
   if(Demolishing&&Valid&&!demolitionPrompted){demolitionPrompted=true;s.Request("nativeConfirmDemolition");}
   if(applyRequested){applyRequested=false;if(!Valid)return deps;if(!Demolishing&&(s.Draft?.Villa==true||s.Draft?.Tower==true)&&villaStage==0&&Plans?.Draft?.VillaExistingRoadsOnly!=true){CaptureOwnership();stagedRoads.AddRange(OwnedEntities);CheckpointRoads();ClearDefinitions();applyMode=ApplyMode.Apply;villaStage=1;frame=0;Valid=false;Status("AP.RoadStageWaiting");return deps;}if(!Demolishing){appliedBuildings.AddRange(buildings);CaptureOwnership();}ClearDefinitions();applyMode=ApplyMode.Apply;committed=true;frame=0;Valid=false;if(Demolishing){s.NativeAuditAvailable=false;s.Request("nativeScheduleDemolition",demolitionPlan.ToString());Status("AP.DemolitionApplied");s.Sound(PlanSound.Demolish);}else{s.NativeCommitted=true;s.NativeAuditAvailable=true;s.Request("nativeRecordCommit");Status("AP.NativeApplied");s.Sound(PlanSound.Build);}}
  }catch(Exception error){forceDemolitionStage=0;if(villaStage==1||villaStage==2)villaStage=4;Valid=false;applyRequested=false;previewRefreshAt=0;ClearDefinitions();applyMode=ApplyMode.Clear;submitted=true;frame=9;Notice=stagedRoads.Count>0?"AP.RoadStagePartial":"AP.Native.Details";Trace.Failure("construction.failed",error);Status("AP.NativeBlocked",error.Message);}
  return deps;
 }
 IEnumerable<int> PreparePreview(PlanSystem s,ValidationResult result){
  foreach(var step in SubmitSteps(s))yield return step;
  SetProgress(65);
  for(int i=0;i<8;i++)yield return 1;
  while(true){
   if(TrimLotSurfaces()){if(++surfacePass>6)throw new InvalidOperationException("Lot surfaces kept regenerating; turn off lot tiles and recreate preview");for(int i=0;i<8;i++)yield return 1;continue;}
   foreach(var step in AttachParkingSteps(s))yield return step;
   if(!parkingChanged)break;
   if(++attachmentPass>4)throw new InvalidOperationException("Parking attachment preview did not stabilize");
   for(int i=0;i<8;i++)yield return 1;
  }
  SetProgress(80);
  foreach(var step in ValidateSteps(result))yield return step;
 }
 float3 Ground(Point p){var terrain=World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData();var v=new float3((float)p.X,0,(float)p.Y);v.y=TerrainUtils.SampleHeight(ref terrain,v);if(!math.all(math.isfinite(v)))throw new InvalidOperationException("Invalid terrain sample");return v;}
 bool deferDefinitions;
 Entity Definition(Entity prefab,bool publish=true){publish=publish&&!deferDefinitions;var e=publish?EntityManager.CreateEntity(typeof(CreationDefinition),typeof(Updated)):EntityManager.CreateEntity(typeof(CreationDefinition));definitions.Add(e);EntityManager.SetComponentData(e,new CreationDefinition{m_Prefab=prefab,m_RandomSeed=12345+definitions.Count*7919});return e;}
 IEnumerable<int> SubmitSteps(PlanSystem s){
  if(s.Draft.RequiredAccessFailures>0)throw new InvalidOperationException("Required pedestrian access cannot be routed inside the site");
  foreach(var role in new[]{"main","aisle","pedestrian","turnaround"}){var selected=s.Network(role);if(selected!=null)Trace.Event("construction.network.selected","role="+role+" prefab="+selected.Id+" width="+selected.Width+" road="+!selected.Path+" car="+selected.Car+" walk="+selected.Walk);}
  var graph=ConstructionCurves.Create(s.Draft);if(graph.Count>1500)throw new InvalidOperationException("Network segment budget exceeded");
  yield return 1;var connections=new SpatialLookup<ConnectionCandidate>(32);
  foreach(var step in ConnectionSnapshotSteps(connections))yield return step;
  if(villaStage<2){
  SetProgress(5);var networkDefinitions=new List<Entity>();
  foreach(var edge in graph){var n=s.Network(edge.Use);if(n==null)throw new InvalidOperationException("Missing network: "+edge.Use);var a=Ground(edge.Curve.A);var b=Ground(edge.Curve.D);var curve=new Bezier4x3(a,Ground(edge.Curve.B),Ground(edge.Curve.C),b);var e=Definition(n.Entity,false);networkDefinitions.Add(e);
   var course=new NetCourse{m_Curve=curve,m_Length=MathUtils.Length(curve),m_FixedIndex=-1,m_Elevation=float2.zero,
    m_StartPosition=Position(a,curve.b-curve.a,true,n.Path,connections),m_EndPosition=Position(b,curve.d-curve.c,false,n.Path,connections)};
   EntityManager.AddComponentData(e,course);
   RememberNetworkCourse(e,course);SetProgress(5+20*networkDefinitions.Count/Math.Max(1,graph.Count));yield return 0;
  }
  // Publish the connected network as one graph: partial native course batches
  // can change junction splitting. Subsequent independent objects are paced.
  using(var publish=new NativeArray<Entity>(networkDefinitions.ToArray(),Allocator.Temp))EntityManager.AddComponent<Updated>(publish);
  yield return 1;
  }
  if((s.Draft.Villa||s.Draft.Tower)&&villaStage==0&&Plans?.Draft?.VillaExistingRoadsOnly!=true)yield break;
  int objectStart=definitions.Count;deferDefinitions=true;
  SetProgress(25);foreach(var step in SubmitPavementSteps(s,graph))yield return step;SetProgress(35);
  foreach(var step in SubmitTreesSteps(s))yield return step;SetProgress(45);
  int totalBuildings=s.Draft.Parcels.Count(p=>p.Kind==0||s.Draft.Tower&&p.Kind==6&&!string.IsNullOrEmpty(p.AssetId));
  foreach(var parcel in s.Draft.Parcels.Where(p=>p.Kind==0||s.Draft.Tower&&p.Kind==6&&!string.IsNullOrEmpty(p.AssetId))){
   // Imported prefabs may have a different registered PrefabID type. Retain the
   // canonical entity discovered by the catalog instead of inventing an ID.
   var asset=s.Assets.Find(a=>a.Id==parcel.AssetId);if(asset==null||!s.BuildingPrefabs.TryGetValue(parcel.AssetId,out var entity)||!EntityManager.Exists(entity)||!EntityManager.HasComponent<BuildingData>(entity))throw new InvalidOperationException("Missing building: "+parcel.AssetId);
   var center=new Point(parcel.X+parcel.W/2,parcel.Y+parcel.D/2);
   // Installed BuildingUtils.CalculateFrontPosition uses local +Z.
   // Choose only rotations preserving the actual prefab lot size.
   Point forward=BuildingPlacement.Forward(parcel,asset);
   var f=s.Draft.Right*forward.X+s.Draft.Forward*forward.Y;var rotation=quaternion.LookRotationSafe(new float3((float)f.X,0,(float)f.Y),math.up());var position=Ground(s.Draft.World(center));
   var definition=Definition(entity);EntityManager.AddComponentData(definition,new ObjectDefinition{m_Position=position,m_Rotation=rotation,m_Probability=100,m_PrefabSubIndex=-1,m_Scale=1,m_Intensity=1,m_ParentMesh=-1});SubmitBuildingChildren(entity,position,rotation);expected.Add(Tuple.Create(entity,position,rotation));SetProgress(45+15*expected.Count/Math.Max(1,totalBuildings));yield return 1;
  }
  Entity bayEntity=Entity.Null;if(s.Draft.Parcels.Any(p=>p.Kind==2&&p.ParkingSpaces>0)&&!s.ResolveParkingDecal(out bayEntity))throw new InvalidOperationException("Loaded parking decal unavailable: "+PlanSystem.ParkingDecalId);
  Trace.Event("construction.parkingDecal",PlanSystem.ParkingDecalId+" width="+s.DecalWidth+" depth="+s.DecalDepth);
  foreach(var row in s.Draft.Parcels.Where(p=>p.Kind==2&&p.ParkingSpaces>0)){
   if(row.ParkingEntrySide==0)throw new InvalidOperationException("Parking entry side unavailable");
   for(int i=0;i<row.ParkingSpaces;i++){
    var center=row.ParkingAlongX?new Point(row.X+(i+.5)*row.W/row.ParkingSpaces,row.Y+row.D/2):new Point(row.X+row.W/2,row.Y+(i+.5)*row.D/row.ParkingSpaces);
    // CSKRParkingDecalClosed1x opens toward -Z; its stopper is on +Z.
    var local=row.ParkingAlongX?new Point(0,-row.ParkingEntrySide):new Point(-row.ParkingEntrySide,0);
    var f=s.Draft.Right*local.X+s.Draft.Forward*local.Y;var rotation=quaternion.LookRotationSafe(new float3((float)f.X,0,(float)f.Y),math.up());var position=Ground(s.Draft.World(center));
    expectedBays.Add(Tuple.Create(bayEntity,position,rotation));
    bayDefinitions.Add(Tuple.Create(Entity.Null,position,-new float3((float)f.X,0,(float)f.Y),(float)(row.ParkingAlongX?row.D:row.W)));yield return 0;
   }
  }
  // Publish all buildings and their child networks together. Incremental publication
  // repeatedly rebuilds the same real roads while the neighbourhood is incomplete.
  deferDefinitions=false;
  using(var publish=new NativeArray<Entity>(definitions.Skip(objectStart).ToArray(),Allocator.Temp))EntityManager.AddComponent<Updated>(publish);
  yield return 1;
  Trace.Event("construction.submitted","networks="+graph.Count+" buildings="+expected.Count+" bays="+expectedBays.Count);
 }
 bool parkingChanged;
 IEnumerable<int> AttachParkingSteps(PlanSystem s){
  // A decal's parking lane searches its Attached parent's SubLane buffer. Merely
  // drawing a car lane nearby cannot produce a native parking connection.
  // Resolve generated edges only after the native preview exists; all remain Temp
  // until a single explicit Apply. Recheck after regeneration before validation.
  var aisle=s.Network("aisle");if(aisle==null)throw new InvalidOperationException("Parking aisle unavailable");
  var aisleTypes=new[]{aisle,s.Network("aisleWide"),s.Network("parkingOneWay"),s.Network("parkingTwoWay")}.Where(n=>n!=null).GroupBy(n=>n.Entity).ToDictionary(g=>g.Key,g=>g.First().Width);
  parkingChanged=false;temps.CompleteDependency();
  var candidates=new SpatialLookup<ConnectionCandidate>(16);
  using(var entities=temps.ToEntityArray(Allocator.Temp))foreach(var edge in entities){
   if(!EntityManager.HasComponent<Game.Net.Edge>(edge)||!EntityManager.HasComponent<Game.Net.Curve>(edge)||!EntityManager.HasComponent<PrefabRef>(edge)||(EntityManager.GetComponentData<Temp>(edge).m_Flags&TempFlags.Delete)!=0)continue;
   if(!aisleTypes.TryGetValue(EntityManager.GetComponentData<PrefabRef>(edge).m_Prefab,out var width))continue;
   IndexCurve(candidates,new ConnectionCandidate{Entity=edge,Curve=EntityManager.GetComponentData<Game.Net.Curve>(edge).m_Bezier,Width=(float)width},(float)(width*.5+Mod.Options.ParkingPavingMargin+2));
  }
  for(int i=0;i<bayDefinitions.Count;i++){
   SetProgress(65+13*i/Math.Max(1,bayDefinitions.Count));yield return i%4==0?1:0;var bay=bayDefinitions[i];
   var port=bay.Item2+bay.Item3*(bay.Item4*.5f);Entity parent=Entity.Null;float best=float.MaxValue;
   foreach(var candidate in candidates.At(port.x,port.z)){
    var curve=candidate.Curve;MathUtils.Distance(curve,port,out float t);var point=MathUtils.Position(curve,t);float d=math.distance(point.xz,port.xz);
    if(d>candidate.Width*.5f+Mod.Options.ParkingPavingMargin+2||d>=best||math.dot(point.xz-bay.Item2.xz,bay.Item3.xz)<=0||math.abs(math.dot(math.normalizesafe(MathUtils.Tangent(curve,t).xz),bay.Item3.xz))>.6f)continue;
    best=d;parent=candidate.Entity;
   }
   if(parent==Entity.Null)throw new InvalidOperationException("Parking bay has no matching native aisle edge");
   if(bay.Item1!=Entity.Null){
    if(EntityManager.GetComponentData<CreationDefinition>(bay.Item1).m_Attached!=parent)throw new InvalidOperationException("Native parking parent changed during preview; recreate the preview");
    continue;
   }
   // Submit each bay only once, with its parent already known. Updating an
   // existing creation definition without a full Clear can leave duplicate Temps.
   var target=expectedBays[i];var definition=Definition(target.Item1);
   var creation=EntityManager.GetComponentData<CreationDefinition>(definition);creation.m_Attached=parent;creation.m_Flags|=CreationFlags.Attach;EntityManager.SetComponentData(definition,creation);
   EntityManager.AddComponentData(definition,new ObjectDefinition{m_Position=target.Item2,m_Rotation=target.Item3,m_Probability=100,m_PrefabSubIndex=-1,m_Scale=1,m_Intensity=1,m_ParentMesh=-1});
   bayDefinitions[i]=Tuple.Create(definition,bay.Item2,bay.Item3,bay.Item4);parkingChanged=true;
  }
  SetProgress(78);
  if(parkingChanged)Trace.Event("construction.parking.attach","definitions="+bayDefinitions.Count+" pass="+attachmentPass);

 }
 CoursePos Position(float3 p,float3 tangent,bool start,bool path,SpatialLookup<ConnectionCandidate> connections){
  Entity connected=Entity.Null;float split=0,best=1.5f;
  // Explicitly join nearby existing networks at nodes or edge positions; generation
  // handles the native split. Never alter road prefabs or utility graphs manually.
  foreach(var candidate in connections.At(p.x,p.z)){if(candidate.Path!=path)continue;var c=candidate.Curve;MathUtils.Distance(c,p,out float t);var point=MathUtils.Position(c,t);float distance=math.distance(point,p);if(distance>=best)continue;best=distance;connected=candidate.Entity;split=t;}
  if(connected!=Entity.Null){var edge=EntityManager.GetComponentData<Game.Net.Edge>(connected);if(split<.01f){connected=edge.m_Start;p=EntityManager.GetComponentData<Game.Net.Node>(connected).m_Position;}else if(split>.99f){connected=edge.m_End;p=EntityManager.GetComponentData<Game.Net.Node>(connected).m_Position;}}
  return new CoursePos{m_Entity=connected,m_SplitPosition=split,m_Position=p,m_Rotation=Game.Net.NetUtils.GetNodeRotation(tangent),m_CourseDelta=start?0:1,m_Elevation=float2.zero,m_Flags=start?CoursePosFlags.IsFirst:CoursePosFlags.IsLast,m_ParentMesh=-1};
 }
 void TraceErrors(Entity e){
  var names=new List<string>();
  if(EntityManager.HasBuffer<Game.Notifications.IconElement>(e))foreach(var item in EntityManager.GetBuffer<Game.Notifications.IconElement>(e,true)){
   if(!EntityManager.HasComponent<PrefabRef>(item.m_Icon))continue;
   var p=EntityManager.GetComponentData<PrefabRef>(item.m_Icon).m_Prefab;
   if(EntityManager.HasComponent<ToolErrorData>(p))names.Add(EntityManager.GetComponentData<ToolErrorData>(p).m_Error.ToString());
  }
  string prefab="";var ps=World.GetOrCreateSystemManaged<PrefabSystem>();if(EntityManager.HasComponent<PrefabRef>(e)&&ps.TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab,out var value))prefab=value?.name??"";
  string geometry="";
  if(EntityManager.HasComponent<Game.Net.Curve>(e)){var c=EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier;geometry=" length="+MathUtils.Length(c)+" a="+c.a+" b="+c.b+" c="+c.c+" d="+c.d;}
  if(EntityManager.HasComponent<Game.Net.Composition>(e)){var comp=EntityManager.GetComponentData<Game.Net.Composition>(e);foreach(var n in new[]{comp.m_StartNode,comp.m_EndNode})if(EntityManager.HasComponent<NetCompositionData>(n))geometry+=" nodeFlags="+EntityManager.GetComponentData<NetCompositionData>(n).m_Flags.m_General;}
  Trace.Event("construction.native.error","entity="+e+" prefab="+prefab+" reasons="+string.Join(",",names)+" stage="+villaStage+geometry);
 }
 bool Live(Entity e)=>e!=Entity.Null&&EntityManager.Exists(e)&&!EntityManager.HasComponent<Deleted>(e)&&!EntityManager.HasComponent<Temp>(e);
 bool RealLane(Entity e)=>Live(e)&&!EntityManager.HasComponent<Game.Net.SlaveLane>(e)&&(!EntityManager.HasComponent<PrefabRef>(e)||!EntityManager.HasComponent<NetLaneData>(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab)||(EntityManager.GetComponentData<NetLaneData>(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab).m_Flags&LaneFlags.Virtual)==0);
 bool ParkingConnected(Entity bay,bool live,out int count,out int links){
  count=links=0;if(!EntityManager.HasBuffer<Game.Net.SubLane>(bay))return false;
  foreach(var sub in EntityManager.GetBuffer<Game.Net.SubLane>(bay,true)){
   var lane=sub.m_SubLane;if(!EntityManager.Exists(lane)||EntityManager.HasComponent<Deleted>(lane)||!EntityManager.HasComponent<Game.Net.ParkingLane>(lane)||EntityManager.HasComponent<Game.Net.SlaveLane>(lane)||(live&&!Live(lane)))continue;
   if(EntityManager.HasComponent<PrefabRef>(lane)){var prefab=EntityManager.GetComponentData<PrefabRef>(lane).m_Prefab;if(EntityManager.HasComponent<NetLaneData>(prefab)&&(EntityManager.GetComponentData<NetLaneData>(prefab).m_Flags&LaneFlags.Virtual)!=0)continue;}
   count++;if(!EntityManager.HasComponent<Game.Net.LaneConnection>(lane))continue;var car=EntityManager.GetComponentData<Game.Net.LaneConnection>(lane).m_StartLane;
   if(EntityManager.Exists(car)&&!EntityManager.HasComponent<Deleted>(car)&&EntityManager.HasComponent<Game.Net.CarLane>(car)&&(!live||Live(car)))links++;
  }
  return count>0&&links==count;
 }
 internal void Audit(){using(Trace.Batch()){var watch=System.Diagnostics.Stopwatch.StartNew();try{AuditCore();}finally{Trace.Event("construction.audit.performance","ms="+watch.ElapsedMilliseconds+" buildings="+appliedBuildings.Count+" bays="+appliedBays.Count);}}}
 void AuditCore(){
  AuditPedestrianConnections();
  int live=0,road=0,power=0,water=0,sewage=0,powerDemand=0,powerSupplied=0,waterDemand=0,waterSupplied=0,pedestrianWarnings=0;
  foreach(var e in appliedBuildings){if(!Live(e)||!EntityManager.HasComponent<Game.Buildings.Building>(e))continue;live++;var b=EntityManager.GetComponentData<Game.Buildings.Building>(e);if(Live(b.m_RoadEdge))road++;
   var ec=EntityManager.HasComponent<Game.Buildings.ElectricityConsumer>(e)?EntityManager.GetComponentData<Game.Buildings.ElectricityConsumer>(e):default;
   var wc=EntityManager.HasComponent<Game.Buildings.WaterConsumer>(e)?EntityManager.GetComponentData<Game.Buildings.WaterConsumer>(e):default;
   if(ec.electricityConnected)power++;if(wc.waterConnected)water++;if(wc.sewageConnected)sewage++;
   powerDemand+=ec.m_WantedConsumption;powerSupplied+=ec.m_FulfilledConsumption;waterDemand+=wc.m_WantedConsumption;waterSupplied+=wc.m_FulfilledFresh;
   bool pedestrianWarning=false;
   if(EntityManager.HasBuffer<Game.Notifications.IconElement>(e))foreach(var icon in EntityManager.GetBuffer<Game.Notifications.IconElement>(e,true))if(EntityManager.HasComponent<PrefabRef>(icon.m_Icon)&&World.GetOrCreateSystemManaged<PrefabSystem>().TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(icon.m_Icon).m_Prefab,out var notification)){
    pedestrianWarning|=notification.name=="No Pedestrian Access";
   }
   if(pedestrianWarning)pedestrianWarnings++;

  }
  int parking=0,connected=0,usableBays=0;
  foreach(var bay in appliedBays)if(Live(bay)&&EntityManager.HasBuffer<Game.Net.SubLane>(bay)){
   int bayLanes=0,bayLinks=0;
   var lanes=EntityManager.GetBuffer<Game.Net.SubLane>(bay,true);foreach(var sub in lanes){var lane=sub.m_SubLane;if(!RealLane(lane)||!EntityManager.HasComponent<Game.Net.ParkingLane>(lane))continue;parking++;bayLanes++;
    TraceParkingLane(bay,lane);
    if(EntityManager.HasComponent<Game.Net.LaneConnection>(lane)){var link=EntityManager.GetComponentData<Game.Net.LaneConnection>(lane);if(Live(link.m_StartLane)&&EntityManager.HasComponent<Game.Net.CarLane>(link.m_StartLane)){connected++;bayLinks++;}}
   }
   if(bayLanes>0&&bayLinks==bayLanes)usableBays++;
   Trace.Event("construction.parking.audit",$"bay={bay} realLanes={bayLanes} liveCarLinks={bayLinks}");
  }
  int missingJunctions=AuditVehicleJunctions(out int junctions);
  int treesLive=appliedTrees.Count(e=>Live(e)&&EntityManager.HasComponent<Game.Objects.Tree>(e));
  Trace.Event("construction.trees.audit","live="+treesLive+" expected="+expectedTrees.Count);
  Status("AP.NativeAudit",$"Buildings {live}/{expected.Count}, road {road}, electricity {power}, water {water}, sewage {sewage}; parking bays {usableBays}/{expectedBays.Count}, real lanes {parking}, live car links {connected}; trees {treesLive}/{expectedTrees.Count}. Vehicle junctions {junctions-missingJunctions}/{junctions}. Lane connectivity is not proof of move-ins.");
  Metrics.Clear();Metrics["CarJunctions"]=junctions-missingJunctions;Metrics["Buildings"]=live;Metrics["Frontage"]=road;Metrics["Bays"]=appliedBays.Count;Metrics["ParkingLinks"]=usableBays;Metrics["PedestrianWarnings"]=pedestrianWarnings;Metrics["Power"]=power;Metrics["Water"]=water;Metrics["Sewage"]=sewage;Metrics["PowerDemand"]=powerDemand;Metrics["PowerSupplied"]=powerSupplied;Metrics["WaterDemand"]=waterDemand;Metrics["WaterSupplied"]=waterSupplied;Notice=missingJunctions>0?"AP.Native.CarJunctionMissing":pedestrianWarnings>0?"AP.Native.PedestrianWarning":"AP.Native.SupplyPending";
 }
 string DescribeConnection(Entity e){
  if(!Live(e))return e+" unavailable";
  string text=e.ToString();
  if(EntityManager.HasComponent<PrefabRef>(e)&&World.GetOrCreateSystemManaged<PrefabSystem>().TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab,out var prefab))text+=" prefab="+prefab.name;
  if(EntityManager.HasComponent<Owner>(e))text+=" owner="+EntityManager.GetComponentData<Owner>(e).m_Owner;
  if(EntityManager.HasComponent<Game.Objects.Transform>(e))text+=" pos="+EntityManager.GetComponentData<Game.Objects.Transform>(e).m_Position;
  if(EntityManager.HasComponent<Game.Net.Curve>(e)){var c=EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier;text+=" curve="+c.a+"->"+c.d;}
  return text;
 }
}
}
