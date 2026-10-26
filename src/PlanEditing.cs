using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Colossal.Serialization.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Unity.Entities;
namespace ApartmentPlan {
public partial class PlanSystem {
 internal int SelectedPlan=-1,EditingPlan=-1,HoverPlan=-1;
 const int MaxEditBytes=2000000;
 internal StoredPlan SelectedStored=>Saved.Find(p=>p.Info.Number==SelectedPlan);
 byte[] CaptureEdit(){
  var settings=new Dictionary<string,object>();foreach(var p in typeof(Settings).GetProperties(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly))
   if(p.CanRead&&p.CanWrite&&(p.PropertyType.IsPrimitive||p.Name=="MainNetwork"||p.Name=="AisleNetwork"||p.Name=="WalkNetwork"||p.Name=="TreeSpeciesJson"||p.Name.StartsWith("Tower",StringComparison.Ordinal)))settings[p.Name]=p.GetValue(Mod.Options);
  var data=new PlanEditData{VillaConnections=VillaConnections,Boundary=Boundary,Entrances=Entrances,Zones=Zones,Ramps=Ramps,Roads=Roads,Draft=Draft,Selected=Selected.ToArray(),Elevation=Elevation,NativeCommitted=NativeCommitted,Settings=settings};
  var bytes=PlanEditCodec.Encode(data);if(bytes.Length>MaxEditBytes)throw new InvalidOperationException("Plan edit data exceeds budget");return bytes;
 }
 PlanEditData ReadEdit(Entity e){
  if(!EntityManager.HasBuffer<PlanEditByte>(e))return null;
  var buffer=EntityManager.GetBuffer<PlanEditByte>(e,true);if(buffer.Length>MaxEditBytes)return null;
  var bytes=new byte[buffer.Length];for(int i=0;i<bytes.Length;i++){if(buffer[i].Value<0||buffer[i].Value>255)return null;bytes[i]=(byte)buffer[i].Value;}
  try{var d=PlanEditCodec.Decode(bytes);return d?.Version==1&&d.Boundary!=null&&Layout.Valid(d.Boundary)&&d.Entrances!=null&&d.Draft!=null?d:null;}catch(Exception ex){Trace.Failure("restore.edit",ex);return null;}
 }
 internal void InspectPlan(int id){if(!Saved.Any(p=>p.Info.Number==id))return;NativeAuditAvailable=false;SelectedPlan=id;Open=false;Focused=false;Exit();SelectCompoundInfo();Revision++;}
 void EditStored(){
  RestoreCompoundDraft();CancelLayout();LayoutOutdated=false;
  var stored=SelectedStored;if(stored==null)return;Catalog();var original=ReadEdit(stored.Entity);var d=original?.Pending??original;
  var boundary=d?.Boundary??StoredBoundary(stored);if(!Layout.Valid(boundary)){Status="AP.LegacyUneditable";return;}
  Boundary.Clear();Boundary.AddRange(boundary);Entrances.Clear();VillaConnections.Clear();Zones.Clear();Ramps.Clear();ClearRoadEdit();ZonePoints.Clear();Preview.Clear();ReplacementIndex=-1;
  NativeAuditAvailable=false;EditingPlan=stored.Info.Number;SelectedPlan=-1;NativeCommitted=false;
  Elevation=d?.Elevation??stored.Shapes[0].A.y;
  if(d!=null){
   VillaConnections.AddRange(d.VillaConnections??new List<VillaConnection>());Entrances.AddRange(d.Entrances);Zones.AddRange(d.Zones??new List<ManualZone>());Ramps.AddRange(d.Ramps??new List<Parcel>());Roads.AddRange(d.Roads??new List<ManualRoad>());
   Selected.Clear();foreach(var id in d.Selected??new string[0])if(Assets.Any(a=>a.Id==id))Selected.Add(id);
   Mod.Options.ParkingInOpenSpace=false;Mod.Options.PlanMode=0;Mod.Options.GrassSurfaces=false;Mod.Options.VillaTreeDensity=30;Mod.Options.TreePlacementMode=0;Mod.Options.BuildingPattern=0;Mod.Options.AllowMainTJunctions=true;
   foreach(var pair in d.Settings??new Dictionary<string,object>()){var property=typeof(Settings).GetProperty(pair.Key);if(property?.CanWrite==true&&(property.PropertyType.IsPrimitive||pair.Key=="MainNetwork"||pair.Key=="AisleNetwork"||pair.Key=="WalkNetwork"||pair.Key=="TreeSpeciesJson"))try{property.SetValue(Mod.Options,Convert.ChangeType(pair.Value,property.PropertyType,System.Globalization.CultureInfo.InvariantCulture));}catch{}}
   string savedAisle=Mod.Options.AisleNetwork;
   ApplyNetworkPreferences();Draft=d.Draft;RenderDraft();Mode=IsBuilt(stored)?"edit":"preview";
   LayoutOutdated=!NativeCommitted&&savedAisle!=Mod.Options.AisleNetwork;
   Status=LayoutOutdated?"AP.NetworkRecalculate":"AP.EditingSaved";
  }else{Draft=null;Mode="entrance";Status="AP.LegacyEdit";}
  Entrance=Entrances.Count>0?(Point?)Entrances[0]:null;EntranceLength=Mod.Options.EntranceLength;Open=true;Activate();Revision++;
 }
 internal bool IsBuilt(StoredPlan p)=>WasConstructed(p.Entity)||p.Edit?.NativeCommitted==true;
 bool WasConstructed(Entity e)=>EntityManager.HasComponent<PlanConstructionState>(e)&&EntityManager.GetComponentData<PlanConstructionState>(e).Applied!=0;
 void RecordConstruction(){
  var stored=Saved.Find(p=>p.Info.Number==EditingPlan);if(stored==null)return;
  if(EntityManager.HasComponent<PlanConstructionState>(stored.Entity))EntityManager.SetComponentData(stored.Entity,new PlanConstructionState{Applied=1});else EntityManager.AddComponentData(stored.Entity,new PlanConstructionState{Applied=1});
  var tool=World.GetExistingSystemManaged<ConstructionTool>();if(tool!=null){if(!EntityManager.HasBuffer<PlanOwnedEntity>(stored.Entity))EntityManager.AddBuffer<PlanOwnedEntity>(stored.Entity);var owned=EntityManager.GetBuffer<PlanOwnedEntity>(stored.Entity);var known=new HashSet<Entity>();if(CompoundApplying){for(int i=0;i<owned.Length;i++)known.Add(owned[i].Entity);}else owned.Clear();foreach(var entity in tool.OwnedEntities)if(known.Add(entity))owned.Add(new PlanOwnedEntity{Entity=entity});tool.SaveVerifiedRoads(stored.Entity);}
  if((Draft.Villa?Mod.Options.VillaTileLots:Mod.Options.TileBuildingLots)&&tool!=null)foreach(var entity in tool.OwnedEntities)if(EntityManager.Exists(entity)&&EntityManager.HasComponent<Game.Buildings.Building>(entity)&&!(Draft.Tower&&EntityManager.HasComponent<Game.Prefabs.PrefabRef>(entity)&&Draft.Parcels.Where(r=>r.Kind==6).Any(r=>BuildingPrefabs.TryGetValue(r.AssetId,out var parking)&&parking==EntityManager.GetComponentData<Game.Prefabs.PrefabRef>(entity).m_Prefab))&&!EntityManager.HasComponent<PlanTileLot>(entity))EntityManager.AddComponent<PlanTileLot>(entity);
  if(GrassEnabled&&!EntityManager.HasComponent<PlanGrassWork>(stored.Entity))EntityManager.AddComponentData(stored.Entity,new PlanGrassWork{State=0});
  FinishCompoundAddition(stored);
  LandscapeOpen=false;Open=false;Focused=false;Preview.Clear();SelectedPlan=-1;EditingPlan=-1;Exit();
  Trace.Event("plan.construction.recorded","id="+stored.Info.Number);
  QueueStoredAudit(stored.Info.Number);
 }
 void BuildStored(){
  var stored=SelectedStored;if(stored==null||WasConstructed(stored.Entity)||stored.Edit?.NativeCommitted==true)return;
  EditStored();if(EditingPlan!=stored.Info.Number||!Open)return;if(Draft!=null)BeginConstruction();else Status="AP.LegacyBuild";
 }
 void BeginConstruction(){
  if(RoadPoints.Count>0){Status="AP.RoadPending";return;}
  EnsurePlanningCatalog();
  if(!catalogReady||CatalogLoading){Status="AP.CatalogLoading";return;}
  var usedAssets=new HashSet<string>(Draft?.Parcels.Where(p=>p.Kind==0).Select(p=>p.AssetId)??Enumerable.Empty<string>());
  foreach(var a in Assets.Where(a=>usedAssets.Contains(a.Id)))EnsureAccessProfile(a);
  if(LayoutPending){Status="AP.Calculating";return;}if(LayoutOutdated)return;
  if(NativeCommitted){Status="AP.AlreadyBuilt";return;}
  if(CompoundEditing&&(Draft?.Villa==true||Draft?.Tower==true)){Status="AP.AdditionsSlabOnly";return;}
  if(!World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>().Available){Status="AP.RoadPackMissing";return;}
  if(networkRevision!=World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>().Revision){CatalogNetworks();Status="AP.CatalogLoading";return;}
  if(LayoutOutdated)return;
  if(Network("main")==null||Network("aisle")==null||!ApartmentAisle(Network("aisle"))){Status="AP.NetworkUnavailable";return;}
  if(Draft?.Villa==true&&(Draft.RequiredAccessFailures>0)){Status="AP.Villa.NoConnection";return;}
  if(Draft==null||!Draft.Parcels.Exists(p=>p.Kind==0)){Status="AP.NeedBuildings";return;}
  if(Draft.Parcels.Any(p=>p.Kind==2&&p.ParkingSpaces>0)&&!ResolveParkingDecal(out _)){Status="AP.ParkingDecalMissing";return;}
  if(Draft.Parcels.Any(p=>p.Kind==2&&p.ParkingSpaces>0&&(Math.Abs((p.ParkingAlongX?p.W:p.D)/p.ParkingSpaces-ParkingPitch)>.01||(p.ParkingAlongX?p.D:p.W)+.01<DecalDepth))){Status="AP.ParkingDecalRecalculate";return;}
  frontageDetails.Clear();
  int missingFronts=FrontageNetwork.Missing(Draft,Assets,Network("aisle").Width,Mod.Options.ParkingPavingMargin,frontageDetails);
  foreach(var detail in frontageDetails)Trace.Event("construction.frontage.missing",detail);
  Trace.Event("construction.frontage.network","missing="+missingFronts+" network="+Network("aisle").Id);
  if(missingFronts>0){Status="AP.FrontageRecalculate";return;}
  if(Draft.ParkingAccessFailures>0){Status="AP.ParkingAccessIncomplete";return;}
  if(Draft.RequiredAccessFailures>0){Status="AP.AccessIncomplete";return;}
  CatalogTrees();Save(true);if(EditingPlan<0)return;if(CompoundEditing){BeginCompoundReplacement();return;}
  Mode="preview";Status="AP.NativeChecking";World.GetOrCreateSystemManaged<Game.Tools.ToolSystem>().activeTool=World.GetOrCreateSystemManaged<ConstructionTool>();
 }
 internal static List<Point> StoredBoundary(StoredPlan p)=>p.Shapes.Where(s=>s.Kind==7&&Math.Abs(s.Width-.5f)<.001f).Select(s=>new Point(s.A.x,s.A.z)).ToList();
 internal object StoredSummary(StoredPlan p)=>p==null?null:new{name=p.Edit?.Name,canDelete=CanDeleteTarget(p.Info.Number),deleteReason=DeletionReason(p.Info.Number),parked=p.Info.Number==occupancyPlan?ParkedCount:null,occupiedHomes=p.Info.Number==occupancyPlan?OccupiedHomes:null,parkingCapacity=p.Info.Number==occupancyPlan?LiveParkingCapacity:null,homeCapacity=p.Info.Number==occupancyPlan?LiveHomeCapacity:null,owned=OwnedObjects(p).Count,id=p.Info.Number,area=p.Info.Area,far=p.Info.Far,homes=p.Info.Homes,built=WasConstructed(p.Entity)||p.Edit?.NativeCommitted==true,buildings=p.Edit?.AppliedPlans!=null?p.Edit.AppliedPlans.Sum(plan=>plan.Parcels.Count(r=>r.Kind==0)):p.Shapes.Count(s=>s.Kind==0),parking=p.Edit?.AppliedPlans!=null?p.Edit.AppliedPlans.Sum(plan=>plan.Parcels.Sum(r=>r.ParkingSpaces)):p.Edit?.Draft?.Parcels.Sum(r=>r.ParkingSpaces),editable=Layout.Valid(StoredBoundary(p)),legacy=p.Edit==null};
}
}




