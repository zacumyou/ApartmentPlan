using System;
using System.Collections.Generic;
using System.Linq;

namespace ApartmentPlan {
// The workflow partial is linked directly from src. Only its engine-facing collaborators
// and scheduling are replaced here; completion is advanced explicitly by the scenario.
public partial class PlanSystem {
 sealed class OptionsStub {
  internal int PlanMode;
  internal bool AutoCalculate,ParkingInOpenSpace;
  internal double EntranceLength=8;
  internal void ApplyAndSave(){}
 }
 sealed class OptionsHost {internal OptionsStub Options=new OptionsStub();}
 readonly OptionsHost Mod=new OptionsHost();
 static class Trace {internal const bool LocalTower=false;internal static void Event(string k,string v,bool detail){}}
 readonly LayoutDebounce layoutDebounce=new LayoutDebounce();
 bool Villa=>Mod.Options.PlanMode==1;
 bool NativeAuditAvailable,LayoutPending,LayoutOutdated,NativeCommitted,LandscapeOpen;
 int EditingPlan=-1,SelectedPlan=-1,HoverPlan=-1,ZoneKind=4,ReplacementIndex,Revision;
 float Elevation;double EntranceLength;
 string Mode="boundary",Status;string RoadHint=>"AP.RoadStart";
 Plan Draft,compoundFullDraft,treePlan;string treeKey;
 Point? Entrance;
 List<Point> Boundary=new List<Point>(),Entrances=new List<Point>(),ZonePoints=new List<Point>(),boundaryRedo=new List<Point>();
 List<Action> entranceRedo=new List<Action>();
 List<ManualRoad> Roads=new List<ManualRoad>();
 List<ManualZone> Zones=new List<ManualZone>();
 List<Parcel> Ramps=new List<Parcel>();
 List<VillaConnection> VillaConnections=new List<VillaConnection>();
 HashSet<string> Selected=new HashSet<string>();
 List<int> Preview=new List<int>(),PlannedTrees=new List<int>();
 void ClearRoadEdit()=>Roads.Clear();
 void CancelLayout(){LayoutPending=false;layoutDebounce.Cancel();}
 void ScheduleLayout()=>layoutDebounce.Schedule(0);
 void CalculateLayout(Action rejected=null){LayoutOutdated=true;LayoutPending=true;}
 void FinishCalculation(){CancelLayout();LayoutOutdated=false;Draft=new Plan();}
 void LoadDefaultModeAssets()=>Selected.Add("test-house");
 void DetectVillaConnections(){if(Entrances.Count==0){Entrances.Add(Boundary[0]);VillaConnections.Add(new VillaConnection());}Entrance=Entrances[0];}
 void ClearInputHistory(){boundaryRedo.Clear();entranceRedo.Clear();}
 void CancelCompoundReplacement(){} void ClearForcedBuilding(){} void Activate(){NativeAuditAvailable=false;} void ResetDeletionDialog(){} void RenderDraft(){}
 static void Expect(bool ok,string action){if(!ok)throw new Exception(action);}
 static int Scenarios(){
  int runs=0;
  foreach(int start in new[]{0,1,2})
  foreach(int via in new[]{0,1,2}.Where(v=>v!=start))
  foreach(bool auto in new[]{false,true})
  foreach(string stop in new[]{"boundary","entrance","preview","backEntrance","backBoundary"})
  foreach(string removal in new[]{"none","suspended","active","unrelated"}){
   var p=new PlanSystem();p.Mod.Options.PlanMode=start;p.Mod.Options.AutoCalculate=auto;p.LoadDefaultModeAssets();
   p.Boundary.AddRange(new[]{new Point(0,0),new Point(80,0),new Point(80,80)});
   p.Elevation=27;p.EditingPlan=81;p.Mod.Options.ParkingInOpenSpace=true;
   if(stop!="boundary"){
    p.EnterEntranceStage();
    if(p.Entrances.Count==0){p.Entrances.Add(p.Boundary[0]);p.Entrance=p.Entrances[0];}
    p.CalculateLayout();p.ReviewLayout();Expect(p.Mode=="entrance","pending review advanced");
    if(!auto)p.ChangeAutoCalculation();
    p.ChangeAutoCalculation();Expect(!p.LayoutPending&&p.LayoutOutdated,"cancel lost stale flag");
    if(stop!="entrance"){
     p.CalculateLayout();p.FinishCalculation();p.ReviewLayout();Expect(p.Mode=="preview","ready review blocked");
     if(stop.StartsWith("back"))p.EnterEntranceStage();
     if(stop=="backBoundary")p.EditBoundaryStage();
    }
   }
   var expected=p.Mode;p.boundaryRedo.Add(new Point(9,0));
   if(auto&&!p.Mod.Options.AutoCalculate)p.ChangeAutoCalculation();
   if(p.LayoutPending)Expect(!p.CanReviewLayout,"pending tab accepted layout review");
   bool sharesSite=p.Draft!=null;
   p.SwitchPlanMode(via);
   Expect(p.compoundFullDraft==null&&p.treePlan==null&&p.treeKey==null&&!p.LandscapeOpen&&p.ReplacementIndex<0,"new tab kept transient tools");
   Expect(p.Mode==(sharesSite?"entrance":"boundary")&&p.Boundary.Count==(sharesSite?3:0)&&p.Draft==null&&!p.LayoutPending&&!p.NativeAuditAvailable,"incorrect site transfer");
   if(sharesSite)Expect(p.Elevation==27&&p.Entrances.Count==1&&p.EditingPlan<0,"lost site elevation or entrance, or inherited save identity");
   p.EditingPlan=82;p.ZonePoints.Add(new Point(3,4));p.Elevation=11;
   if(removal=="suspended")p.ForgetDemolishedPlan(81);
   if(removal=="active")p.ForgetDemolishedPlan(82);
   if(removal=="unrelated")p.ForgetDemolishedPlan(93);
   int third=3-start-via;p.SwitchPlanMode(third);p.ChangeAutoCalculation();p.SwitchPlanMode(start);
   if(removal=="suspended")Expect(p.Mode=="boundary"&&p.EditingPlan<0&&p.Boundary.Count==0&&p.Draft==null,"deleted tab revived");
   else{
    Expect(p.Mode==expected&&p.Elevation==27&&p.EditingPlan==81&&p.boundaryRedo.Count==1&&p.Mod.Options.ParkingInOpenSpace,"round trip lost editor state: "+stop);
    if(stop=="boundary")Expect(!p.LayoutOutdated&&!p.LayoutPending,"unconfirmed polygon calculated");
    else {p.CalculateLayout();p.FinishCalculation();if(p.Mode=="edit")p.EnterEntranceStage();p.ReviewLayout();Expect(p.Mode=="preview","restored workflow blocked");}
   }
   if(p.EditingPlan==81&&p.Draft!=null){
    p.Entrances.Clear();p.Entrance=p.Villa?p.Boundary[0]:(Point?)null;p.Rebuild();
    Expect(p.Draft==null&&!p.LayoutOutdated&&!p.CanReviewLayout,"last entrance removed but result survived");
    if(!p.Mod.Options.AutoCalculate)p.ChangeAutoCalculation();
    Expect(!p.LayoutPending,"empty villa fallback started calculation");
   }
   p.SwitchPlanMode(via);
   Expect(removal=="active"?p.EditingPlan<0&&p.ZonePoints.Count==0:p.EditingPlan==82&&p.ZonePoints.Count==1&&p.Elevation==11,"other draft deletion isolation");
   runs++;
  }
  return runs;
 }
 public static void VerifyWorkflow(){Console.WriteLine("PASS "+Scenarios()+" multi-tab planning sequences");}
}
}

