using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public partial class PlanSystem {
 sealed class TabDraft {
  internal PlanEditData Plan;
  internal string Stage;
  internal List<Point> ZonePoints,BoundaryRedo;
  internal List<Action> EntranceRedo;
  internal int ZoneKind;
  internal bool ParkingInOpenSpace;
 }
 readonly Dictionary<int,TabDraft> modeDrafts=new Dictionary<int,TabDraft>();
 internal bool CanReviewLayout=>Entrances.Count>0&&Selected.Count>0&&Draft!=null&&!LayoutPending&&!LayoutOutdated;
 void ResetPlanningInputs(){
  CancelLayout();ClearInputHistory();ResetDeletionDialog();LayoutOutdated=false;LandscapeOpen=false;
  compoundFullDraft=null;EditingPlan=-1;NativeCommitted=false;
  Boundary.Clear();VillaConnections.Clear();Ramps.Clear();ClearRoadEdit();Entrances.Clear();Zones.Clear();ZonePoints.Clear();
  Elevation=0;ClearForcedBuilding();ReplacementIndex=-1;Draft=null;Entrance=null;Preview.Clear();treePlan=null;treeKey=null;PlannedTrees.Clear();
  EntranceLength=Mod.Options.EntranceLength;Mode="boundary";
 }
 void ForgetCachedPlan(int id){
  foreach(var tab in modeDrafts.Where(entry=>entry.Value.Plan.EditingNumber==id).Select(entry=>entry.Key).ToArray())modeDrafts.Remove(tab);
 }
 void ForgetDemolishedPlan(int id){
  ForgetCachedPlan(id);
  if(EditingPlan==id)ResetPlanningInputs();
  if(SelectedPlan==id)SelectedPlan=-1;
  if(HoverPlan==id)HoverPlan=-1;
 }
 void EditBoundaryStage(){
  if(Boundary.Count<3)return;
  CancelCompoundReplacement();NativeAuditAvailable=false;Mode="edit";Status="AP.EditBoundary";Activate();
 }
 void SwitchPlanMode(int mode){
   // Switching tabs preserves each unfinished draft, including connection exclusions.
   modeDrafts[Mod.Options.PlanMode]=new TabDraft{Stage=Mode,ZoneKind=ZoneKind,ZonePoints=ZonePoints.ToList(),BoundaryRedo=boundaryRedo.ToList(),EntranceRedo=entranceRedo.ToList(),ParkingInOpenSpace=Mod.Options.ParkingInOpenSpace,Plan=new PlanEditData{Elevation=Elevation,Boundary=Boundary.ToList(),Entrances=Entrances.ToList(),Zones=Zones.ToList(),Ramps=Ramps.ToList(),Roads=Roads.ToList(),Draft=Draft,Selected=Selected.ToArray(),VillaConnections=VillaConnections.ToList(),NativeCommitted=NativeCommitted,EditingNumber=EditingPlan}};
   Trace.Event("plan.mode.changed","from="+Mod.Options.PlanMode+" to="+mode,Trace.LocalTower);
   Mod.Options.PlanMode=mode;ResetPlanningInputs();Mod.Options.ParkingInOpenSpace=false;Status="AP.Draw";SelectedPlan=-1;Activate();Selected.Clear();
   if(modeDrafts.TryGetValue(mode,out var tab)){
    modeDrafts.Remove(mode);
    var d=tab.Plan;
    Mode=tab.Stage;Elevation=d.Elevation;ZoneKind=tab.ZoneKind;ZonePoints.AddRange(tab.ZonePoints);
    boundaryRedo.AddRange(tab.BoundaryRedo);entranceRedo.AddRange(tab.EntranceRedo);
    Mod.Options.ParkingInOpenSpace=tab.ParkingInOpenSpace;
    Boundary.AddRange(d.Boundary);Entrances.AddRange(d.Entrances);Zones.AddRange(d.Zones);Ramps.AddRange(d.Ramps);Roads.AddRange(d.Roads);VillaConnections.AddRange(d.VillaConnections);foreach(var id in d.Selected)Selected.Add(id);Draft=d.Draft;NativeCommitted=d.NativeCommitted;EditingPlan=d.EditingNumber;
    Entrance=Entrances.Count>0?(Point?)Entrances[0]:null;Status=Mode=="entrance"?"AP.PickEntrance":Mode=="edit"?"AP.EditBoundary":Mode=="zone"?"AP.DrawZone":Mode=="road"?RoadHint:Mode=="replace"?"AP.PickBuilding":"AP.Draw";if(!Entrance.HasValue)Draft=null;
    if(Draft!=null)RenderDraft();
   }else LoadDefaultModeAssets();
   if(Entrance.HasValue&&Layout.Valid(Boundary)&&!NativeCommitted)Rebuild();Mod.Options.ApplyAndSave();Revision++;
 }
 void EnterEntranceStage(){
    if(!Layout.Valid(Boundary)){Status="AP.InvalidBoundary";return;}
    bool confirmBoundary=Mode=="boundary"||Mode=="fill"||Mode=="edit"&&VillaConnections.Count==0;
    Mode="entrance";Status="AP.PickEntrance";
    if(Villa&&confirmBoundary){DetectVillaConnections();Rebuild();}
    Activate();
 }
 void ReviewLayout(){if(Mode=="entrance"&&CanReviewLayout)Mode="preview";}
 void Rebuild(Action rejected=null){
  if(Entrances.Count==0||!Layout.Valid(Boundary)){
   CancelLayout();Draft=null;Preview.Clear();LayoutOutdated=false;
   Status=Mode=="entrance"?"AP.PickEntrance":"AP.Draw";Revision++;return;
  }
  if(!Mod.Options.AutoCalculate){
   // Deferred edits stay in the input model. A later failure must not roll back
   // just one edit while silently accepting the others against an older plan.
   CancelLayout();LayoutOutdated=true;Status="AP.CalculationNeeded";Revision++;return;
  }
  // Transactional edits retain their immediate rollback callback. Ordinary
  // changes are sampled only after the user stops editing for one second.
  if(rejected!=null){CalculateLayout(rejected);return;}
  CancelLayout();LayoutOutdated=true;ScheduleLayout();Status="AP.LayoutWaiting";Revision++;
 }
 void ChangeAutoCalculation(){
  Mod.Options.AutoCalculate=!Mod.Options.AutoCalculate;Mod.Options.ApplyAndSave();
  if(!Mod.Options.AutoCalculate){
   if(LayoutPending||layoutDebounce.Pending){CancelLayout();LayoutOutdated=true;Status="AP.CalculationNeeded";}
  }else if(!NativeCommitted&&Layout.Valid(Boundary)&&Entrances.Count>0)CalculateLayout();
  Revision++;
 }
}
}
