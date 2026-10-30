using System.Linq;
using Game.SceneFlow;
using Game.Tools;
using Game.UI;
using Unity.Entities;
namespace ApartmentPlan {
public partial class PlanSystem {
 string NewPlanName(int number){
  var names=Selected.OrderBy(x=>x,System.StringComparer.Ordinal).Select(id=>{
   string name;return GameManager.instance.localizationManager.activeDictionary.TryGetValue("Assets.NAME["+id+"]",out name)&&!string.IsNullOrWhiteSpace(name)?name:id;
  }).Distinct();
  return "계획도_"+System.DateTime.Now.ToString("yyyyMMdd")+"_"+number+"_"+new string(string.Join("+",names).Where(c=>!char.IsWhiteSpace(c)).ToArray());
 }
 System.Collections.Generic.List<System.Tuple<int,Entity>> deleteAllSnapshot;
 bool CanDeleteAllPlans=>Ready&&!LayoutPending&&demolitionCountdown<=0&&!deletionDialogOpen&&!NativeBusy&&Saved.Any(p=>!p.Hidden);
 void ConfirmAllPlanDeletion(){
  if(!CanDeleteAllPlans)return;
  // Leave a failed/idle native preview before evaluating the plan-only policy.
  if(World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool)Activate();
  var snapshot=Saved.Where(p=>!p.Hidden&&CanDeleteTarget(p.Info.Number)).Select(p=>System.Tuple.Create(p.Info.Number,p.Entity)).ToList();if(snapshot.Count==0){Status="AP.DeleteBuilt";Revision++;return;}
  int generation=++deletionDialogGeneration;deleteAllSnapshot=snapshot;deletionDialogOpen=true;Revision++;
  GameManager.instance.userInterface.appBindings.ShowConfirmationDialog(new ConfirmationDialog("AP.DeleteAllPlans","AP.DeleteAllPlansMessage","Common.DIALOG_ACTION[Yes]","Common.DIALOG_ACTION[No]"),answer=>{
   if(generation!=deletionDialogGeneration)return;deletionDialogOpen=false;Revision++;
   if(answer==0)Request("deleteAllPlansConfirmed",generation.ToString());else ResetDeletionDialog();
  });
 }
 void ApplyAllPlanDeletion(string value){
  if(DeletionBusy||!int.TryParse(value,out var generation)||generation!=deletionDialogGeneration||deleteAllSnapshot==null)return;
  var targets=deleteAllSnapshot;deleteAllSnapshot=null;ResetDeletionDialog();
  foreach(var target in targets){var plan=Saved.Find(p=>p.Info.Number==target.Item1&&p.Entity==target.Item2);if(plan!=null&&CanDeleteTarget(target.Item1))Command("delete",target.Item1.ToString());}
  Revision++;
 }
 // Never dispatch demolition or modify any constructed entity from this path.
 void RemoveSavedListing(StoredPlan plan){
  if(HasOwnedObjects(plan)||IsBuilt(plan)){
   var state=new PlanListState{Hidden=1};
   if(EntityManager.HasComponent<PlanListState>(plan.Entity))EntityManager.SetComponentData(plan.Entity,state);
   else EntityManager.AddComponentData(plan.Entity,state);
   plan.Hidden=true;
  }else{EntityManager.DestroyEntity(plan.Entity);Saved.Remove(plan);}
 }
 const int DraftDeletion=-2;
 int deletionDialogGeneration,deletePlanId=-1;Entity deletePlanEntity;bool deletionDialogOpen;byte[] deleteDraftSnapshot;
 bool DeletionBusy=>!Ready||LayoutPending||demolitionCountdown>0||World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool;
 int CurrentDeletionTarget=>PlanDeletionPolicy.Target(Open,EditingPlan,SelectedPlan);
 bool CanDeleteTarget(int id){
  var stored=id>=0?Saved.Find(p=>p.Info.Number==id):null;
  bool draft=id==DraftDeletion&&Open&&EditingPlan<0;
  return PlanDeletionPolicy.Allowed(Ready,LayoutPending,World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool,demolitionCountdown>0,deletionDialogOpen,
   draft?(Boundary.Count>0||Draft!=null):stored!=null&&!stored.Hidden,false,draft&&NativeCommitted);
 }
 string DeletionReason(int id){
  if(!Ready)return "AP.DeleteNotReady";
  if(LayoutPending)return "AP.DeleteCalculating";
  if(demolitionCountdown>0)return "AP.DeleteDemolition";
  if(World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool)return "AP.DeletePreview";
  if(deletionDialogOpen)return "AP.DeleteDialogOpen";
  var stored=id>=0?Saved.Find(p=>p.Info.Number==id):null;
  if(id==DraftDeletion&&NativeCommitted)return "AP.DeleteBuilt";
  return CanDeleteTarget(id)?"AP.DeletePlanHint":"AP.DeleteNoTarget";
 }
 bool CanDeleteCurrent=>CanDeleteTarget(CurrentDeletionTarget);
 void ResetDeletionDialog(){deletionDialogGeneration++;deletionDialogOpen=false;deletePlanId=-1;deletePlanEntity=Entity.Null;deleteDraftSnapshot=null;Revision++;}
 void ConfirmPlanDeletion(string value){
  int id=CurrentDeletionTarget;if(!string.IsNullOrEmpty(value)&&!int.TryParse(value,out id))return;
  if(!NativeBusy&&demolitionCountdown<=0&&World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool)Activate();
  if(!CanDeleteTarget(id))return;
  var stored=id>=0?Saved.Find(p=>p.Info.Number==id):null;
  int generation=++deletionDialogGeneration;deletePlanId=id;deletePlanEntity=stored?.Entity??Entity.Null;deleteDraftSnapshot=id==DraftDeletion?CaptureEdit():null;deletionDialogOpen=true;Revision++;
  try{
   GameManager.instance.userInterface.appBindings.ShowConfirmationDialog(new ConfirmationDialog("AP.DeleteDialogTitle",id==DraftDeletion?"AP.DeleteDraftMessage":"AP.DeleteDialogMessage","Common.DIALOG_ACTION[Yes]","Common.DIALOG_ACTION[No]"),answer=>{
    if(generation!=deletionDialogGeneration)return;
    deletionDialogOpen=false;Revision++;Trace.Event("plan.delete.dialog.answer","id="+id+" answer="+answer);
    // Defer ECS changes to the normal command queue, never the UI callback.
    if(answer==0)Request("deleteConfirmed",generation.ToString());else ResetDeletionDialog();
   });
   Trace.Event("plan.delete.dialog.open","id="+id+" entity="+deletePlanEntity);
  }catch{ResetDeletionDialog();throw;}
 }
 void ApplyConfirmedPlanDeletion(string value){
  if(DeletionBusy||!int.TryParse(value,out int generation)||generation!=deletionDialogGeneration||deletePlanId==-1)return;
  int id=deletePlanId;var entity=deletePlanEntity;var snapshot=deleteDraftSnapshot;
  bool allowed=CanDeleteTarget(id);ResetDeletionDialog();if(!allowed)return;
  if(id==DraftDeletion){
   // A different draft or edits made while the dialog was open need fresh approval.
   if(CurrentDeletionTarget!=DraftDeletion||snapshot==null||!snapshot.SequenceEqual(CaptureEdit()))return;
   RoadDraft.Clear();Command("new","");Trace.Event("plan.draft.discarded");return;
  }
  var stored=Saved.Find(p=>p.Info.Number==id);
  // A reused numeric plan ID or a city reload cannot redirect an old approval.
  if(stored==null||stored.Entity!=entity||!EntityManager.Exists(entity))return;
  Command("delete",id.ToString());
 }
}
}
