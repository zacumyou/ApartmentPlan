using Game.SceneFlow;
using Game.Tools;
using Game.UI;
namespace ApartmentPlan {
public partial class PlanSystem {
 bool compoundDialogOpen;int compoundDialogRun=-1;
 void ConfirmCompoundDemolition(bool force=false){
  var tool=World.GetExistingSystemManaged<ConstructionTool>();
  if(tool!=null&&compoundDialogRun!=tool.RunGeneration)compoundDialogOpen=false;
  if(compoundDialogOpen||World.GetExistingSystemManaged<ToolSystem>()?.activeTool!=tool||tool==null||!tool.Demolishing||(force?!tool.CanForceDemolition:!tool.Valid))return;
  int run=tool.RunGeneration,id=tool.DemolitionPlan;var stored=Saved.Find(p=>p.Info.Number==id);if(stored==null)return;var entity=stored.Entity;
  compoundDialogOpen=true;compoundDialogRun=run;
  try{GameManager.instance.userInterface.appBindings.ShowConfirmationDialog(new ConfirmationDialog("AP.DemolishDialogTitle",force?"AP.ForceDemolishMessage":"AP.DemolishDialogMessage","Common.DIALOG_ACTION[Yes]","Common.DIALOG_ACTION[No]"),answer=>{
   if(compoundDialogRun!=run)return;
   compoundDialogOpen=false;
   if(World.GetExistingSystemManaged<ToolSystem>()?.activeTool!=tool||tool.RunGeneration!=run||!tool.Demolishing||tool.DemolitionPlan!=id||Saved.Find(p=>p.Info.Number==id)?.Entity!=entity)return;
   Request("nativeDemolitionAnswer",run+"|"+id+"|"+answer+"|"+(force?1:0));
  });}catch{compoundDialogOpen=false;throw;}
 }
 void ApplyCompoundDemolitionAnswer(string value){
  var parts=value.Split('|');if(parts.Length!=4||!int.TryParse(parts[0],out int run)||!int.TryParse(parts[1],out int id)||!int.TryParse(parts[2],out int answer))return;
  var tool=World.GetExistingSystemManaged<ConstructionTool>();
  if(tool==null||World.GetExistingSystemManaged<ToolSystem>()?.activeTool!=tool||!tool.Demolishing||tool.RunGeneration!=run||tool.DemolitionPlan!=id)return;
  Trace.Event("plan.demolition.confirmed","id="+id+" answer="+answer);
  if(answer==0){if(parts[3]=="1")tool.RequestForceDemolition();else tool.Apply();}else if(parts[3]!="1")Activate(); // Force-delete cancellation keeps the blocked preview and retry icon.
 }
}
}
