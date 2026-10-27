using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using Game.Tools;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace ApartmentPlan {
public sealed class StoredPlan {internal bool TreePreviewReady;internal readonly List<PlannedTree> TreePreview=new List<PlannedTree>();public Entity Entity;public SavedPlan Info;public PlanEditData Edit;public List<PlanShape> Shapes=new List<PlanShape>();}
public partial class PlanSystem:GameSystemBase {
 string limitStatus;double limitCurrent,limitMaximum;
 object LimitInfo=>!LayoutPending&&Status==limitStatus?new{current=limitCurrent,maximum=limitMaximum}:null;
 void SetLimit(string status,double current,double maximum){Status=limitStatus=status;limitCurrent=current;limitMaximum=maximum;}

 internal bool Open,Focused,Ready;internal string Mode="boundary",Status="AP.Draw";internal int Revision,CatalogRevision;
 internal readonly List<Point> Boundary=new List<Point>();internal readonly List<Asset> Assets=new List<Asset>();internal readonly HashSet<string> Selected=new HashSet<string>();
 internal readonly List<StoredPlan> Saved=new List<StoredPlan>();internal readonly List<PlanShape> Preview=new List<PlanShape>();internal readonly List<Parcel> Ramps=new List<Parcel>();
 internal readonly List<Point> Entrances=new List<Point>();
 internal readonly List<ManualZone> Zones=new List<ManualZone>();
 internal readonly List<Point> ZonePoints=new List<Point>();internal int ZoneKind=4,ReplacementIndex=-1;
 internal bool NativeCommitted,NativeAuditAvailable;int demolitionCountdown;string pendingDemolition;
 internal readonly Dictionary<string,Entity> BuildingPrefabs=new Dictionary<string,Entity>();
 internal Plan Draft;internal Point? Entrance;internal double EntranceLength;internal float Elevation;
 readonly List<Tuple<string,string>> commands=new List<Tuple<string,string>>();EntityQuery savedQuery;bool catalogReady;

 protected override void OnCreate(){base.OnCreate();Trace.Event("system.create");savedQuery=GetEntityQuery(ComponentType.ReadOnly<SavedPlan>(),ComponentType.ReadOnly<PlanShape>());}
 internal void Request(string command,string value=""){Trace.Event("command.request",command+" value="+Trace.Clean(value)+" ready="+Ready+" queued="+commands.Count);if(commands.Count<128)commands.Add(Tuple.Create(command,value));else Trace.Event("command.dropped","queue full: "+command);}
 internal void ClearTransient(){closePlanDialog=false;closePlanGeneration++;CancelCompoundReplacement();compoundFullDraft=null;ClearRoadMigration();ClearOccupancy();diagnosticCopyState=0;frontageDetails.Clear();CancelCatalog();accessProfiles.Clear();compoundDialogOpen=false;compoundDialogRun=-1;storedAudits.Clear();storedAuditTicks=0;LandscapeOpen=false;treePlan=null;treeKey=null;PlannedTrees.Clear();Trees.Clear();TreePrefabs.Clear();populationChecked=-1000;HouseholdAverage=0;parkingDecalEntity=Entity.Null;CancelLayout();ResetDeletionDialog();Trace.Event("state.clear","savedCache="+Saved.Count+" draft="+(Draft!=null)+" pending="+commands.Count);demolitionCountdown=0;pendingDemolition=null;NativeCommitted=false;NativeAuditAvailable=false;SelectedPlan=EditingPlan=HoverPlan=-1;networkRevision=-1;Networks.Clear();commands.Clear();Boundary.Clear();VillaConnections.Clear();Ramps.Clear();ClearRoadEdit();Entrances.Clear();Zones.Clear();ZonePoints.Clear();ReplacementIndex=-1;Preview.Clear();Saved.Clear();Assets.Clear();BuildingPrefabs.Clear();Selected.Clear();Draft=null;Entrance=null;Open=false;Focused=false;Ready=false;catalogReady=false;Mode="boundary";Status="AP.Draw";Elevation=0;CatalogRevision++;Revision++;}
 protected override void OnGamePreload(Purpose purpose,GameMode mode){Trace.Event("load.preload","purpose="+purpose+" mode="+mode);base.OnGamePreload(purpose,mode);ClearTransient();}
 protected override void OnGameLoadingComplete(Purpose purpose,GameMode mode){Trace.Event("load.complete.begin","purpose="+purpose+" mode="+mode);base.OnGameLoadingComplete(purpose,mode);diagnosticShareGeneration++;diagnosticShareDialog=false;pendingReport=null;Ready=mode==GameMode.Game;if(Ready)Catalog();EntranceLength=Mod.Options?.EntranceLength??15;LoadSaved();foreach(var p in Saved)if(IsBuilt(p))QueueStoredAudit(p.Info.Number);Revision++;Trace.Event("load.complete.end","ready="+Ready+" plans="+Saved.Count);}
 void LoadSaved(){Trace.Event("restore.begin");Saved.Clear();using(var entities=savedQuery.ToEntityArray(Allocator.Temp)){foreach(var e in entities){var info=EntityManager.GetComponentData<SavedPlan>(e);if(info.Version!=1&&info.Version!=2){Trace.Event("restore.unsupported","version="+info.Version);Mod.Log.Warn("Unsupported plan version: "+info.Version);continue;}var b=EntityManager.GetBuffer<PlanShape>(e,true);var stored=new StoredPlan{Entity=e,Info=info};for(int i=0;i<b.Length&&i<PlanLimits.TotalShapes;i++){var s=b[i];if(math.all(math.isfinite(s.A))&&math.all(math.isfinite(s.B))&&math.isfinite(s.Width)&&s.Width>0&&s.Width<1000&&s.Kind>=0&&s.Kind<=10)stored.Shapes.Add(s);}stored.Edit=ReadEdit(e);Saved.Add(stored);Trace.Event("restore.plan","id="+info.Number+" accepted="+stored.Shapes.Count+" source="+b.Length);}}Trace.Event("restore.end","plans="+Saved.Count);}
 void ReadPedestrianAccess(Entity prefab,float3 position,quaternion rotation,List<Point> points,int depth){
  if(depth>8||!EntityManager.HasBuffer<Game.Prefabs.SubObject>(prefab))return;
  foreach(var sub in EntityManager.GetBuffer<Game.Prefabs.SubObject>(prefab,true)){
   if(sub.m_Probability<=0)continue;var p=position+math.rotate(rotation,sub.m_Position);var r=math.mul(rotation,sub.m_Rotation);
   if(EntityManager.HasComponent<RouteConnectionData>(sub.m_Prefab)){
    var data=EntityManager.GetComponentData<RouteConnectionData>(sub.m_Prefab);
    if(data.m_AccessConnectionType==RouteConnectionType.Pedestrian||data.m_RouteConnectionType==RouteConnectionType.Pedestrian){var point=new Point(p.x,p.z);if(points.All(q=>(q-point).Length>.5))points.Add(point);}
   }
   ReadPedestrianAccess(sub.m_Prefab,p,r,points,depth+1);
  }
 }
 protected override void OnUpdate(){PollDiagnosticSharing();if(!Ready||Mod.Options==null){if(commands.Count>0)Trace.Sample("command.blocked","ready="+Ready+" settings="+(Mod.Options!=null)+" pending="+commands.Count);return;}TickCompoundReplacement();TickCompoundInfo();TickCatalog();TickStoredAudit();TickOccupancy();PollLayout();if(Open&&!CatalogLoading){if(Draft!=null)UpdatePopulationEstimate();if(!LayoutPending)RefreshPlannedTrees();}if(catalogReady&&!catalogFailed&&!CatalogLoading&&!(World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool)&&networkRevision!=World.GetOrCreateSystemManaged<InvisibleRoadPrefabs>().Revision){CatalogNetworks();}if(demolitionCountdown>0&&--demolitionCountdown==0)Request("nativeDemolitionDone",pendingDemolition);if(commands.Count==0)return;var work=commands.ToArray();commands.Clear();foreach(var cmd in work){try{var watch=System.Diagnostics.Stopwatch.StartNew();Trace.Event("command.begin",cmd.Item1+" mode="+Mode);Command(cmd.Item1,cmd.Item2);Trace.Event("command.end",cmd.Item1+" open="+Open+" mode="+Mode+" diagnostics="+Draft?.Diagnostics+" status="+Status+" points="+Boundary.Count+" selected="+Selected.Count+" saved="+Saved.Count+" ms="+watch.ElapsedMilliseconds);}catch(Exception e){Trace.Failure("command.failed."+cmd.Item1,e);Status="AP.Error";Mod.Log.Error(e,"Plan command failed: "+cmd.Item1);}Revision++;}}
 void Activate(){EnsurePlanningCatalog();Trace.Event("tool.activate.request");var tools=World.GetOrCreateSystemManaged<ToolSystem>();tools.activeTool=World.GetOrCreateSystemManaged<PlanTool>();}
 void Exit(){Trace.Event("tool.exit.request");var tools=World.GetOrCreateSystemManaged<ToolSystem>();if(tools.activeTool is PlanTool||tools.activeTool is ConstructionTool)tools.activeTool=World.GetOrCreateSystemManaged<DefaultToolSystem>();Focused=false;}
 internal bool NativeBusy=>World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool tool&&tool.Busy;
 bool HasCurrentNativeReport=>NativeAuditAvailable||World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool;
 void CommandCore(string command,string value){
  if(command=="closePlanAnswer"){ApplyClosePlanAnswer(value);return;}
  if(closePlanDialog)return;
  if(replacementPlan>=0&&!new[]{"nativeDemolishApply","nativeConfirmDemolition","nativeForceDemolition","nativeDemolitionAnswer","nativeScheduleDemolition","nativeDemolitionDone","nativeCancel","close","copyDiagnostics","shareDiagnostics","shareDiagnosticsAnswer"}.Contains(command))return;
  if(replacementPlan>=0&&(demolitionCountdown>0||replacementPreviewFrames>0)&&(command=="nativeCancel"||command=="close"))return;
  if(command=="shareDiagnostics"){ConfirmDiagnosticSharing();return;}
  if(command=="shareDiagnosticsAnswer"){ApplyDiagnosticSharingAnswer(value);return;}
  if(CompoundApplying&&!new[]{"nativeApply","nativeCancel","close","copyDiagnostics","nativeRecordCommit","nativeAudit","nativeGrassRefresh"}.Contains(command))return;
  if(NativeBusy&&!new[]{"nativeSaveLandscape","nativeRecordCommit","nativeScheduleDemolition","nativeDemolitionDone","nativeGrassRefresh"}.Contains(command))return;
  if((command=="close"||command=="menu"&&Open)&&Open&&!NativeCommitted&&Boundary.Count>=3&&Mode!="boundary"){ConfirmClosePlan();return;}
  if(command=="deleteAllPlans"){ConfirmAllPlanDeletion();return;}
  if(command=="deleteAllPlansConfirmed"){ApplyAllPlanDeletion(value);return;}
  if(command=="copyDiagnostics"){CopyDiagnostics();return;}diagnosticCopyState=0;
  if(RoadDraft.Points.Count>0&&!new[]{"finish","road","roadShape","roadCancel","undo","snap","close","menu","confirmDelete","deleteConfirmed","delete"}.Contains(command)){Status="AP.RoadPending";return;}
  if((!catalogReady||CatalogLoading)&&!command.StartsWith("native",StringComparison.Ordinal)&&!new[]{"menu","close","landscapeClose","closeInspector","inspectPlan","auditSaved","nativeScheduleDemolition","nativeDemolitionDone","nativeGrassRefresh"}.Contains(command)){Catalog();Status="AP.CatalogLoading";return;}
  if(LandscapeCommand(command,value))return;
  if(TowerCommand(command,value)||VillaCommand(command,value))return;
  if((command=="confirmDelete"||command=="deleteConfirmed"||command=="delete")&&DeletionBusy)return;
  if(LayoutPending&&new[]{"save","nativePreview","nativeApply","editSaved","buildSaved","zone","road","pickBuilding","undo","finish","finishEntrances","roadShape","roadCancel"}.Contains(command)){Status="AP.Calculating";return;}
  if(World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool && !command.StartsWith("native") && command!="menu" && command!="close" && command!="confirmDelete" && command!="deleteConfirmed" && command!="delete")Activate();
  switch(command){
   case "scanRoadMigration":ScanRoadMigration();break;
   case "applyRoadMigration":ApplyRoadMigration();break;
   case "fill":ToggleFill();break;
   case "fillRadius":if(int.TryParse(value,out int fillSize))FillRadius=Math.Max(50,Math.Min(600,fillSize));break;
   case "nativeGrassRefresh":Revision++;break;
   case "nativeSaveLandscape":if(World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool&&int.TryParse(value,out int landscapePlan)&&EditingPlan==landscapePlan&&!NativeCommitted)Save(true);break;
   case "menu":if(!Open){modeDrafts.Clear();EnsurePlanningCatalog();goto case "new";}LandscapeOpen=false;Open=false;SelectedPlan=-1;Exit();break;
   case "close":CancelCompoundReplacement();LandscapeOpen=false;Open=false;SelectedPlan=-1;Exit();RestoreCompoundDraft();break;
   case "nativePreview":BeginConstruction();break;
   case "nativeRecordCommit":RecordConstruction();break;
   case "demolishSaved":BeginDemolition();break;
   case "nativeDemolishApply":case "nativeConfirmDemolition":ConfirmCompoundDemolition();break;
   case "nativeForceDemolition":ConfirmCompoundDemolition(true);break;
   case "nativeDemolitionAnswer":ApplyCompoundDemolitionAnswer(value);break;
   case "nativeScheduleDemolition":pendingDemolition=value;demolitionCountdown=120;break;
   case "nativeDemolitionDone":CompleteDemolition(value);break;
   case "buildSaved":BuildStored();break;
   case "nativeApply":if(LayoutOutdated){Status="AP.CalculationNeeded";break;}World.GetOrCreateSystemManaged<ConstructionTool>().Apply();break;
   case "nativeCancel":CancelCompoundReplacement();Activate();RestoreCompoundDraft();break;
   case "auditSaved":if(SelectedStored!=null)World.GetOrCreateSystemManaged<ConstructionTool>().AuditStored(SelectedStored);break;
   case "nativeAudit":if(NativeAuditAvailable)World.GetOrCreateSystemManaged<ConstructionTool>().Audit();break;
   case "network":SelectNetwork(value);break;
   case "inspectPlan":if(int.TryParse(value,out int inspectId))InspectPlan(inspectId);break;
   case "closeInspector":CloseCompoundInfo();break;
   case "editSaved":EditStored();break;
   case "grassSurfaces":Mod.Options.GrassSurfaces=!Mod.Options.GrassSurfaces;Mod.Options.ApplyAndSave();Revision++;break;
   case "lotTiles":if(Villa)Mod.Options.VillaTileLots=!Mod.Options.VillaTileLots;else Mod.Options.TileBuildingLots=!Mod.Options.TileBuildingLots;Mod.Options.ApplyAndSave();Revision++;break;
   case "surfaceRadius":if(float.TryParse(value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float corner)&&corner>=0&&corner<=4){Mod.Options.SurfaceCornerRadius=corner;Mod.Options.ApplyAndSave();Revision++;}break;
   case "mainSetback":if(int.TryParse(value,out int mainGap)&&mainGap>=0&&mainGap<=20){Mod.Options.MainParkingSetback=mainGap;Mod.Options.ApplyAndSave();Rebuild();}break;
   case "boundaryMargin":if(int.TryParse(value,out int edgeGap)&&edgeGap>=0&&edgeGap<=20){Mod.Options.BoundaryGreenMargin=edgeGap;Mod.Options.ApplyAndSave();Rebuild();}break;
   case "curveLength":if(int.TryParse(value,out int lengthPercent)&&lengthPercent>=25&&lengthPercent<=100){Mod.Options.MainRoadCurveLengthPercent=lengthPercent;Mod.Options.ApplyAndSave();Rebuild();}break;
   case "curveRadius":if(int.TryParse(value,out int radius)&&radius>=8&&radius<=200){Mod.Options.MainRoadCurveRadius=radius;Mod.Options.ApplyAndSave();Rebuild();}break;
   case "parkingGreen":Mod.Options.ParkingGreenIslands=!Mod.Options.ParkingGreenIslands;Mod.Options.ApplyAndSave();Rebuild();break;
   case "parkingGreenWidth":if(int.TryParse(value,out int greenWidth)&&greenWidth>=0&&greenWidth<=6){Mod.Options.ParkingIslandWidth=greenWidth;Mod.Options.ApplyAndSave();Rebuild();}break;
   case "lineOverlap":if(float.TryParse(value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float overlap)&&overlap>=0&&overlap<=.3f){Mod.Options.ParkingLineOverlap=overlap;Mod.Options.ApplyAndSave();Rebuild();}break;
   case "decalGap":if(float.TryParse(value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float gap)&&gap>=.1f&&gap<=3){Mod.Options.ParkingDecalGap=gap;Mod.Options.ApplyAndSave();Rebuild();}break;
   case "new":Mod.Options.ParkingInOpenSpace=false;compoundFullDraft=null;Open=true;LandscapeOpen=false;LayoutOutdated=false;ResetDeletionDialog();CancelLayout();SelectedPlan=EditingPlan=-1;NativeCommitted=false;NativeAuditAvailable=false;Boundary.Clear();VillaConnections.Clear();Ramps.Clear();ClearRoadEdit();Entrances.Clear();Zones.Clear();ZonePoints.Clear();ReplacementIndex=-1;Draft=null;Entrance=null;Preview.Clear();EntranceLength=Mod.Options.EntranceLength;Mode="boundary";Status="AP.Draw";Activate();break;
   case "edit":if(Boundary.Count>=3){Mode="edit";Status="AP.EditBoundary";Activate();}break;

   case "snap":switch(value){case "All":bool enabled=!(Mod.Options.SnapRoads&&Mod.Options.SnapBuildings&&Mod.Options.SnapLots&&Mod.Options.SnapGrid&&Mod.Options.SnapDirections&&Mod.Options.SnapGuides);Mod.Options.SnapRoads=Mod.Options.SnapBuildings=Mod.Options.SnapLots=Mod.Options.SnapGrid=Mod.Options.SnapDirections=Mod.Options.SnapGuides=enabled;break;case "Roads":Mod.Options.SnapRoads=!Mod.Options.SnapRoads;break;case "Buildings":Mod.Options.SnapBuildings=!Mod.Options.SnapBuildings;break;case "Lots":Mod.Options.SnapLots=!Mod.Options.SnapLots;break;case "Grid":Mod.Options.SnapGrid=!Mod.Options.SnapGrid;break;case "Directions":Mod.Options.SnapDirections=!Mod.Options.SnapDirections;break;case "Guides":Mod.Options.SnapGuides=!Mod.Options.SnapGuides;break;}Mod.Options.ApplyAndSave();break;
   case "finishEntrances":if(Mode=="entrance"&&Entrances.Count>0&&(Draft!=null||!Mod.Options.AutoCalculate)){Mode="preview";}break;
   case "finish":if(Mode=="road"){FinishRoad();break;}if(Mode=="entrance"){if(Entrances.Count>0&&(Draft!=null||!Mod.Options.AutoCalculate))Mode="preview";break;}if(Mode=="zone"){FinishZone();break;}if(!Layout.Valid(Boundary)){Status="AP.InvalidBoundary";break;}Mode="entrance";Status="AP.PickEntrance";if(Villa){DetectVillaConnections();Rebuild();}Activate();break;
   case "entrance":if(Layout.Valid(Boundary)){Mode="entrance";Status="AP.PickEntrance";Activate();}break;
   case "zone":if(Layout.Valid(Boundary)&&int.TryParse(value,out int kind)&&(kind==3||kind==4||kind==5)){ZoneKind=kind;ZonePoints.Clear();Mode="zone";Status="AP.DrawZone";Activate();}break;
   case "removeZone":if(Zones.Count>0){Zones.RemoveAt(Zones.Count-1);Rebuild();}break;
   case "pickBuilding":if(Draft!=null){Mode="replace";ReplacementIndex=-1;Status="AP.PickBuilding";Activate();}break;
   case "road":if(Layout.Valid(Boundary)&&Entrance.HasValue&&!NativeCommitted){if(Mode=="road")FinishRoad();else{Mode="road";Status=RoadHint;}Activate();}break;
   case "roadShape":if(Mode=="road"&&!NativeCommitted){RoadDraft.Shape(value=="curve");Status=RoadHint;}break;
   case "roadCancel":RoadDraft.Clear();Mode="preview";Status="AP.Ready";break;
   case "undo":if(Villa&&Mode=="entrance"){UndoVillaEntrance();break;}if(Mode=="road"){UndoRoad();break;}if(Mode=="zone"){if(ZonePoints.Count>0)ZonePoints.RemoveAt(ZonePoints.Count-1);else{Mode="preview";Status="AP.Ready";}}else if(Mode=="replace"){ReplacementIndex=-1;Mode="preview";Status="AP.Ready";}else if(Mode=="entrance"&&Entrances.Count>0){Entrances.RemoveAt(Entrances.Count-1);Entrance=Entrances.Count>0?(Point?)Entrances[0]:null;Rebuild();}else if(Mode=="boundary"&&Boundary.Count>0)Boundary.RemoveAt(Boundary.Count-1);else if(Mode=="edit"){CancelLayout();Mode="boundary";Entrance=null;Entrances.Clear();Zones.Clear();Draft=null;Preview.Clear();Ramps.Clear();ClearRoadEdit();Status="AP.Draw";}else if(Roads.Count>0&&!NativeCommitted){Roads.RemoveAt(Roads.Count-1);RoadPoints.Clear();Rebuild();}else if(Ramps.Count>0){Ramps.RemoveAt(Ramps.Count-1);Rebuild();}break;
   case "select":if(Mode=="replace"&&ReplacementIndex>=0){ReplaceBuilding(value);break;}if(Assets.Any(a=>a.Id==value)){if(!Selected.Add(value))Selected.Remove(value);else RememberBuildingAsset(value);SaveSelectedAssets();Rebuild();}break;
   case "clearAssets":Selected.Clear();SaveSelectedAssets();Rebuild();break;
   case "asset":var edit=JsonConvert.DeserializeObject<AssetEdit>(value);var asset=edit==null?null:Assets.Find(a=>a.Id==edit.Id);if(asset!=null&&edit.Height>=1&&edit.Height<=1000&&edit.Footprint>=0&&edit.Footprint<=asset.Width*asset.Depth&&edit.Homes>=0&&edit.Homes<=10000){asset.Height=edit.Height;asset.Footprint=edit.Footprint;asset.Homes=edit.Homes;SaveAssetCorrection(edit);CatalogRevision++;Rebuild();}else Status="AP.InvalidAsset";break;
   case "entranceLength":if(int.TryParse(value,out int entranceLength)&&entranceLength>=3&&entranceLength<=99){EntranceLength=entranceLength;SaveEntranceLength();Rebuild();}break;
   case "longer":EntranceLength=Math.Min(99,EntranceLength+2);SaveEntranceLength();Rebuild();break;
   case "shorter":EntranceLength=Math.Max(3,EntranceLength-2);SaveEntranceLength();Rebuild();break;
   case "buildingPattern":if(int.TryParse(value,out int pattern)&&pattern>=0&&pattern<=3){Mod.Options.BuildingPattern=pattern;Mod.Options.ApplyAndSave();Rebuild();}break;
   case "mainTJunctions":Mod.Options.AllowMainTJunctions=!Mod.Options.AllowMainTJunctions;Mod.Options.ApplyAndSave();Rebuild();break;
   case "cornerMode":Mod.Options.MainRoadCornerMode=(Mod.Options.MainRoadCornerMode+1)%3;Mod.Options.ApplyAndSave();Rebuild();break;
   case "buildingRoad":Mod.Options.RequireBuildingRoad=true;Mod.Options.ApplyAndSave();Rebuild();break;
   case "parkingInOpenSpace":Mod.Options.ParkingInOpenSpace=!Mod.Options.ParkingInOpenSpace;Mod.Options.ApplyAndSave();Rebuild();break;
   case "frontParking":Mod.Options.RequireFrontParking=!Mod.Options.RequireFrontParking;Mod.Options.ApplyAndSave();Rebuild();break;
   case "autoCalculate":ToggleAutoCalculate();break;
   case "rebuild":CalculateLayout();break;
   case "save":Save();break;
   case "confirmDelete":ConfirmPlanDeletion(value);break;
   case "deleteConfirmed":ApplyConfirmedPlanDeletion(value);break;
   case "delete":if(int.TryParse(value,out int id)){var old=Saved.Find(s=>s.Info.Number==id);if(old!=null&&CanDeleteTarget(id)){EntityManager.DestroyEntity(old.Entity);Saved.Remove(old);if(SelectedPlan==id)SelectedPlan=-1;if(HoverPlan==id)HoverPlan=-1;if(EditingPlan==id){RoadDraft.Clear();Command("new","");}Trace.Event("plan.deleted","id="+id);Mod.Log.Info("Plan deleted: "+id);}}break;
  }
 }
 void SaveEntranceLength(){Mod.Options.EntranceLength=(int)EntranceLength;Mod.Options.ApplyAndSave();}
 void RestoreAssetPreferences(){
  try{foreach(var id in JsonConvert.DeserializeObject<string[]>(Villa?(string.IsNullOrEmpty(Mod.Options.VillaSelectedAssetsJson)?"[]":Mod.Options.VillaSelectedAssetsJson):(Tower?Mod.Options.TowerSelectedAssetsJson:Mod.Options.SelectedAssetsJson))??new string[0])if(Assets.Any(a=>a.Id==id))Selected.Add(id);
   foreach(var e in JsonConvert.DeserializeObject<List<AssetEdit>>(Mod.Options.AssetCorrectionsJson)??new List<AssetEdit>()){
    var a=Assets.Find(v=>v.Id==e.Id);if(a==null||double.IsNaN(e.Height)||double.IsNaN(e.Footprint)||e.Height<1||e.Height>1000||e.Footprint<0||e.Footprint>a.Width*a.Depth||e.Homes<0||e.Homes>10000)continue;
    a.Height=e.Height;a.Footprint=e.Footprint;a.Homes=e.Homes;
   }
  }catch(Exception e){Trace.Failure("preferences.restore",e);}
 }
 void RememberBuildingAsset(string id){
  string[] previous;try{previous=JsonConvert.DeserializeObject<string[]>(Mod.Options.RecentBuildingAssetsJson??"[]")??new string[0];}catch(JsonException){previous=new string[0];}
  Mod.Options.RecentBuildingAssetsJson=JsonConvert.SerializeObject(new[]{id}.Concat(previous.Where(x=>!string.IsNullOrEmpty(x)&&x!=id)).Distinct().Take(256));
 }
 void SaveSelectedAssets(){
  var previous=JsonConvert.DeserializeObject<string[]>(Villa?(string.IsNullOrEmpty(Mod.Options.VillaSelectedAssetsJson)?"[]":Mod.Options.VillaSelectedAssetsJson):(Tower?Mod.Options.TowerSelectedAssetsJson:Mod.Options.SelectedAssetsJson))??new string[0];
  var json=JsonConvert.SerializeObject(previous.Where(id=>!Assets.Any(a=>a.Id==id)).Concat(Selected).Distinct().OrderBy(id=>id));if(Villa)Mod.Options.VillaSelectedAssetsJson=json;else if(Tower)Mod.Options.TowerSelectedAssetsJson=json;else Mod.Options.SelectedAssetsJson=json;Mod.Options.ApplyAndSave();
 }
 void SaveAssetCorrection(AssetEdit edit){
  var edits=JsonConvert.DeserializeObject<List<AssetEdit>>(Mod.Options.AssetCorrectionsJson)??new List<AssetEdit>();edits.RemoveAll(e=>e.Id==edit.Id);edits.Add(edit);Mod.Options.AssetCorrectionsJson=JsonConvert.SerializeObject(edits);Mod.Options.ApplyAndSave();
 }
 sealed class AssetEdit {public string Id{get;set;}public double Height{get;set;}public double Footprint{get;set;}public int Homes{get;set;}}
 void ClickCore(float3 position){Trace.Event("world.click","mode="+Mode+" position="+position+" ready="+Ready+" focused="+Focused);if(!Ready||Focused||LayoutPending||CatalogLoading||!catalogReady){Trace.Event("world.click.blocked");return;}var p=new Point(position.x,position.z);Elevation=position.y;
  if(Mode=="boundary"){if(Boundary.Count>=3&&(Boundary[0]-p).Length<=.05){Request("finish");return;}if(Boundary.Count<128&&(Boundary.Count==0||(Boundary[Boundary.Count-1]-p).Length>=1)){Boundary.Add(p);Revision++;Trace.Event("boundary.point","count="+Boundary.Count);}return;}
  if(Mode=="road"){ClickRoad(p);return;}
  if(Mode=="zone"){if(ZonePoints.Count>=3&&(p-ZonePoints[0]).Length<.1){FinishZone();}else if(Layout.Inside(p,Boundary)&&Layout.CanAppend(ZonePoints,p)){ZonePoints.Add(p);Status="AP.DrawZone";}else Status="AP.ZoneFit";Revision++;return;}
  if(Mode=="replace"&&Draft!=null){var local=Draft.Local(p);ReplacementIndex=Draft.Parcels.FindIndex(r=>r.Kind==0&&local.X>=r.X&&local.X<=r.X+r.W&&local.Y>=r.Y&&local.Y<=r.Y+r.D);Status=ReplacementIndex>=0?"AP.ChooseReplacement":"AP.PickBuilding";Revision++;return;}
  if(Mode=="entrance"){
   if(Villa){AddVillaConnection(p);return;}
   var snapped=SiteFrame.Nearest(Boundary,p,out var inward);
   if(Entrances.Any(e=>(e-snapped).Length<5)){Status="AP.ExistingEntrance";Revision++;return;}
   if((snapped-p).Length>16||Entrances.Count>=16){Status="AP.EntranceFit";Revision++;return;}
   Entrances.Add(snapped);Entrance=Entrances[0];Rebuild(()=>{Entrances.RemoveAt(Entrances.Count-1);Entrance=Entrances.Count>0?(Point?)Entrances[0]:null;});Mode="entrance";
  }
  
  Trace.Event("state.changed","mode="+Mode+" diagnostics="+Draft?.Diagnostics+" status="+Status+" shapes="+Preview.Count);Revision++;
 }
 internal void BoundaryEdited(){if(Villa)DetectVillaConnections();Rebuild();Revision++;}
 void RenderDraft(){treeKey=null;Preview.Clear();foreach(var item in Draft.Parcels){var r=item.CompactBounds??item;var a=Draft.World(new Point(r.X+r.W*.5,r.Y));var b=Draft.World(new Point(r.X+r.W*.5,r.Y+r.D));double displayWidth=r.W;if(item.PlacementOutline?.Length==4){var q=item.PlacementOutline;a=Draft.World((q[0]+q[1])*.5);b=Draft.World((q[2]+q[3])*.5);displayWidth=(q[1]-q[0]).Length;}Preview.Add(new PlanShape{A=new float3((float)a.X,Elevation,(float)a.Y),B=new float3((float)b.X,Elevation,(float)b.Y),Width=(float)displayWidth,Kind=item.Kind==1&&item.InvisibleRoad?10:item.Kind});}
   int marks=0;foreach(var route in Draft.Routes.Where(r=>r.Use=="main")){
    double distance=0;
    for(int i=1;i<route.Points.Count;i++){
     var start=route.Points[i-1];var delta=route.Points[i]-start;double length=delta.Length;if(length<.001)continue;
     for(double offset=0;offset<length-.0001&&marks<500;){
      double phase=(distance+offset)%6;bool on=phase<3;double end=Math.Min(length,offset+(on?3:6)-phase);if(end-offset<.0001){offset+=.001;continue;}
      if(on){var a=Draft.World(start+delta*(offset/length));var b=Draft.World(start+delta*(end/length));Preview.Add(new PlanShape{A=new float3((float)a.X,Elevation,(float)a.Y),B=new float3((float)b.X,Elevation,(float)b.Y),Width=.12f,Kind=7});marks++;}offset=end;
     }distance+=length;
    }
   }
   if(Mod.Options.ShowParkingLines){int lineBudget=2000;foreach(var r in Draft.Parcels.Where(r=>r.Kind==2&&r.ParkingSpaces>0)){if(lineBudget<r.ParkingSpaces+1)break;for(int n=0;n<=r.ParkingSpaces;n++){double offset=Math.Min(r.ParkingAlongX?r.W:r.D,n*(r.ParkingAlongX?r.W:r.D)/r.ParkingSpaces);var a=Draft.World(r.ParkingAlongX?new Point(r.X+offset,r.Y):new Point(r.X,r.Y+offset));var b=Draft.World(r.ParkingAlongX?new Point(r.X+offset,r.Y+r.D):new Point(r.X+r.W,r.Y+offset));Preview.Add(new PlanShape{A=new float3((float)a.X,Elevation,(float)a.Y),B=new float3((float)b.X,Elevation,(float)b.Y),Width=.12f,Kind=7});lineBudget--;}}}
 }
 void FinishZone(){
  if(!SiteFrame.ContainsPolygon(Boundary,ZonePoints)){Status="AP.ZoneFit";return;}
  if(Zones.Count>=PlanLimits.Zones){SetLimit("AP.LimitZones",Zones.Count+1,PlanLimits.Zones);return;}
  var points=ZonePoints.ToList();Zones.Add(new ManualZone{Kind=ZoneKind,Points=points});if(!Entrance.HasValue){ZonePoints.Clear();Mode="entrance";Status="AP.PickEntrance";return;}Rebuild(()=>{Zones.RemoveAt(Zones.Count-1);ZonePoints.Clear();ZonePoints.AddRange(points);Mode="zone";});
  ZonePoints.Clear();Mode="preview";
 }
 void ReplaceBuilding(string id){
  var asset=Assets.Find(a=>a.Id==id);if(asset!=null)EnsureAccessProfile(asset);if(Draft==null||asset==null||ReplacementIndex<0||ReplacementIndex>=Draft.Parcels.Count)return;
  var old=Draft.Parcels[ReplacementIndex];bool horizontal=old.W>=old.D;double w=horizontal?Math.Max(asset.Width,asset.Depth):Math.Min(asset.Width,asset.Depth),d=horizontal?Math.Min(asset.Width,asset.Depth):Math.Max(asset.Width,asset.Depth);
  if(w>old.W+.001||d>old.D+.001){Status="AP.ReplacementFit";return;}
  var replacement=new Parcel{Kind=0,Frontage=old.Frontage,Access=old.Access,X=old.X+(old.W-w)/2,Y=old.Y+(old.D-d)/2,W=w,D=d,AssetId=id,Homes=asset.Homes,FloorArea=Math.Min(asset.Width*asset.Depth,asset.Footprint>0?asset.Footprint:asset.Width*asset.Depth*Mod.Options.FootprintPercent/100.0)*Math.Max(1,Math.Floor(asset.Height/Mod.Options.FloorHeight))};
  ReplacementRules.KeepFront(old,replacement);
  if(Mod.Options.RequireFrontParking&&!ParkingFrontRules.Served(replacement,Draft.Parcels,Mod.Options.FrontParkingDistance,Mod.Options.MinFrontParkingSpaces,Mod.Options.FrontParkingCoverage/100.0)){Status="AP.ReplacementParking";return;}

  double nextFloor=Draft.Parcels.Where(r=>r.Kind==0).Sum(r=>r.FloorArea)-old.FloorArea+replacement.FloorArea;
  if(Mod.Options.RespectTargetFar&&nextFloor>Draft.Area*Mod.Options.TargetFar/100+.001){Status="AP.ReplacementFar";return;}
  // Keep the vacated lot footprint as paved strips; other blocks/roads stay fixed.
  Draft.Parcels.AddRange(ReplacementRules.Vacated(old,replacement));
  RememberBuildingAsset(id);Mod.Options.ApplyAndSave();
  Draft.Parcels[ReplacementIndex]=replacement;Draft.Homes+=replacement.Homes-old.Homes;Draft.Far=Draft.Parcels.Where(r=>r.Kind==0).Sum(r=>r.FloorArea)/Draft.Area*100;Draft.Shortfall[0]=Math.Max(0,Draft.Homes*Mod.Options.ParkingPerHome-Draft.Parcels.Sum(r=>r.ParkingSpaces));RenderDraft();Status="AP.Replaced";Trace.Event("building.replaced",id);
 }
 void Save(bool keepDraft=false){if(!catalogReady||CatalogLoading||RoadPoints.Count>0||LayoutPending||LayoutOutdated)return;Trace.Event("save.begin","shapes="+Preview.Count+" plans="+Saved.Count);if(Draft==null||!Draft.Parcels.Any(p=>p.Kind==0)){Status="AP.NeedBuildings";return;}if(Draft.Villa&&(Draft.RequiredAccessFailures>0)){Status="AP.Villa.NoConnection";return;}if(Draft.ParkingAccessFailures>0){Status="AP.ParkingAccessIncomplete";return;}if(EditingPlan<0&&Saved.Count>=PlanLimits.SavedPlans){SetLimit("AP.LimitSavedPlans",Saved.Count+1,PlanLimits.SavedPlans);return;}int totalShapes=Saved.Where(s=>s.Info.Number!=EditingPlan).Sum(s=>s.Shapes.Count)+Preview.Count+Boundary.Count;if(totalShapes>PlanLimits.TotalShapes){SetLimit("AP.LimitTotalShapes",totalShapes,PlanLimits.TotalShapes);return;}
  if(SaveCompoundPending())return;
  var editBytes=CaptureEdit();var existing=Saved.Find(p=>p.Info.Number==EditingPlan);int number=existing?.Info.Number??(Saved.Count==0?1:Saved.Max(s=>s.Info.Number)+1);var namedEdit=PlanEditCodec.Decode(editBytes);namedEdit.Name=existing?.Edit?.Name??NewPlanName(number);editBytes=PlanEditCodec.Encode(namedEdit);Entity e=existing?.Entity??EntityManager.CreateEntity();var info=new SavedPlan{Version=2,Number=number,Area=(float)Draft.Area,Far=(float)Draft.Far,Homes=Draft.Homes};if(existing==null)EntityManager.AddComponentData(e,info);else EntityManager.SetComponentData(e,info);if(!EntityManager.HasBuffer<PlanEditByte>(e))EntityManager.AddBuffer<PlanEditByte>(e);var editBuffer=EntityManager.GetBuffer<PlanEditByte>(e);editBuffer.Clear();foreach(var v in editBytes)editBuffer.Add(new PlanEditByte{Value=v});if(!EntityManager.HasBuffer<PlanShape>(e))EntityManager.AddBuffer<PlanShape>(e);var buf=EntityManager.GetBuffer<PlanShape>(e);buf.Clear();foreach(var s in Preview)buf.Add(s);
  for(int i=0;i<Boundary.Count;i++){var a=Boundary[i];var b=Boundary[(i+1)%Boundary.Count];buf.Add(new PlanShape{A=new float3((float)a.X,Elevation,(float)a.Y),B=new float3((float)b.X,Elevation,(float)b.Y),Width=.5f,Kind=7});}
  LoadSaved();if(keepDraft){EditingPlan=number;return;}Draft=null;Preview.Clear();Boundary.Clear();Entrance=null;Entrances.Clear();Zones.Clear();ZonePoints.Clear();Ramps.Clear();Mode="boundary";Status="AP.Saved";EditingPlan=-1;InspectPlan(number);Trace.Event("save.complete","id="+number+" area="+info.Area+" far="+info.Far+" shapes="+Saved.Find(p=>p.Info.Number==number).Shapes.Count);Mod.Log.Info("Plan saved to city metadata: "+number);
 }
 internal string State()=>JsonConvert.SerializeObject(new{recentNetworks=Mod.Options.RecentNetworksJson,autoCalculate=Mod.Options.AutoCalculate,layoutOutdated=LayoutOutdated,parkingInOpenSpace=Mod.Options.ParkingInOpenSpace,compoundEditing=CompoundEditing,canForceDemolition=World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool forceTool&&forceTool.CanForceDemolition,canDeleteAllPlans=CanDeleteAllPlans,migration=MigrationState(),fillRadius=FillRadius,canFill=CanFill,recentBuildingAssets=Mod.Options.RecentBuildingAssetsJson,entranceClose=Mode=="entrance"&&!Villa&&EntranceSpacing.HasClosePair(Entrances),tower=TowerState(),villaCentralRoad=Mod.Options.VillaCentralRoad,villaExistingRoadsOnly=Draft?.VillaExistingRoadsOnly??false,villaRoadDensity=Draft?.VillaRoadDensity??0,villaFollowBoundary=Mod.Options.VillaFollowBoundary,planMode=Mod.Options.PlanMode,villaMesh=Mod.Options.VillaMesh,villaGap=Mod.Options.VillaGap,villaReduction=Mod.Options.VillaReduction,villaMainSpacing=Mod.Options.VillaMainSpacing,villaRows=Mod.Options.VillaRows,villaMaxBuildings=Mod.Options.VillaMaxBuildings,villaBoundary=Mod.Options.VillaBoundary,villaSetback=Mod.Options.VillaSetback,villaConnectionDistance=Mod.Options.VillaConnectionDistance,villaConnections=VillaConnections.Select(c=>new{c.Side,c.Junction,c.Disabled}),limit=LimitInfo,grassSurfaces=Mod.Options.GrassSurfaces,nativeProgress=World.GetExistingSystemManaged<ConstructionTool>()?.Progress??0,nativeBusy=NativeBusy||replacementPlan>=0&&(demolitionCountdown>0||replacementPreviewFrames>0),buildingPattern=Mod.Options.BuildingPattern,mainTJunctions=Mod.Options.AllowMainTJunctions,layoutReasons=LayoutPending?null:layoutReasons,statusError=StatusError,diagnosticCopyState,diagnosticShareStatus,diagnosticShareBusy=diagnosticShareDialog||diagnosticShareTask!=null,catalogLoading=CatalogLoading,catalogCount=catalogProcessed,lotTiles=Villa?Mod.Options.VillaTileLots:Mod.Options.TileBuildingLots,surfaceRadius=Mod.Options.SurfaceCornerRadius,landscape=LandscapeState(),canDelete=CanDeleteCurrent,deleteReason=DeletionReason(CurrentDeletionTarget),mainSetback=Mod.Options.MainParkingSetback,boundaryMargin=Mod.Options.BoundaryGreenMargin,population=EstimatedPopulation,householdAverage=HouseholdAverage,calculating=LayoutPending,roadCurved=RoadCurved,roadPoints=RoadPoints.Count,pendingRoads=RoadDraft.Roads.Count,roads=Roads.Count,nativeDemolition=World.GetExistingSystemManaged<ConstructionTool>()?.Demolishing??false,inspector=StoredSummary(SelectedStored),editingPlan=EditingPlan,curveRadius=Mod.Options.MainRoadCurveRadius,curveLength=Mod.Options.MainRoadCurveLengthPercent,lineOverlap=Mod.Options.ParkingLineOverlap,parkingPitch=ParkingPitch,decalGap=Mod.Options.ParkingDecalGap,parkingGreen=Mod.Options.ParkingGreenIslands,parkingGreenWidth=Mod.Options.ParkingIslandWidth,networks=Networks,mainNetwork=Mod.Options.MainNetwork,aisleNetwork=Villa?"Alley":Mod.Options.AisleNetwork,walkNetwork=Mod.Options.WalkNetwork,nativeActive=World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool,nativeValid=World.GetExistingSystemManaged<ConstructionTool>()?.Valid??false,nativeReport=HasCurrentNativeReport?World.GetExistingSystemManaged<ConstructionTool>()?.Report??"":"",nativeMetrics=HasCurrentNativeReport?World.GetExistingSystemManaged<ConstructionTool>()?.Metrics:null,nativeNotice=HasCurrentNativeReport?World.GetExistingSystemManaged<ConstructionTool>()?.Notice??"":"",nativeCommitted=NativeCommitted,nativeAuditAvailable=NativeAuditAvailable,cornerMode=Mod.Options.MainRoadCornerMode,requireBuildingRoad=true,parkingAccessFailures=Draft?.ParkingAccessFailures??0,requireFrontParking=Mod.Options.RequireFrontParking,open=Open,mode=Mode,status=LayoutPending?"AP.Calculating":Status,points=Boundary.Count,zonePoints=ZonePoints.Count,zones=Zones.Count,entrances=Entrances.Count,replacement=ReplacementIndex>=0&&Draft!=null?Draft.Parcels[ReplacementIndex].AssetId:null,entrance=EntranceLength,selected=Selected.ToArray(),snaps=new[]{Mod.Options.SnapRoads,Mod.Options.SnapBuildings,Mod.Options.SnapLots,Mod.Options.SnapGrid,Mod.Options.SnapDirections,Mod.Options.SnapGuides,Mod.Options.SnapClose},area=Draft?.Area??(Boundary.Count>=3?System.Math.Abs(Layout.SignedArea(Boundary)):0),far=Draft?.Far??0,homes=Draft?.Homes??0,shortfall=Draft?.Shortfall,counts=Draft?.Parcels.GroupBy(p=>p.Kind==1&&p.InvisibleRoad?10:p.Kind).ToDictionary(g=>g.Key,g=>g.Count()),accessFailures=Draft?.RequiredAccessFailures??0,areas=Draft?.Parcels.GroupBy(p=>p.Kind==1&&p.InvisibleRoad?10:p.Kind).ToDictionary(g=>g.Key,g=>g.Sum(p=>p.Area)),saved=Saved.Select(StoredSummary),colors=Mod.Options?.Colors,canSave=!CompoundApplying&&catalogReady&&!CatalogLoading&&RoadPoints.Count==0&&!LayoutPending&&!LayoutOutdated&&Status!="AP.NetworkUnavailable"&&Draft?.Parcels.Any(p=>p.Kind==0)==true&&Draft.ParkingAccessFailures==0&&(!Draft.Villa||Draft.RequiredAccessFailures==0)});
}
}








































