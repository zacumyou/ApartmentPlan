using Game.SceneFlow;
using Game.UI;
namespace ApartmentPlan {
public partial class PlanSystem {
 bool closePlanDialog;int closePlanGeneration;
 void ConfirmClosePlan(){
  if(closePlanDialog)return;closePlanDialog=true;int generation=++closePlanGeneration;
  try{GameManager.instance.userInterface.appBindings.ShowConfirmationDialog(new ConfirmationDialog("AP.ClosePlanTitle","AP.ClosePlanMessage","Common.DIALOG_ACTION[Yes]","Common.DIALOG_ACTION[No]"),answer=>Request("closePlanAnswer",generation+"|"+answer));}
  catch{closePlanDialog=false;throw;}
 }
 void ApplyClosePlanAnswer(string value){
  var parts=value.Split('|');if(parts.Length!=2||!int.TryParse(parts[0],out int generation)||generation!=closePlanGeneration||!closePlanDialog)return;
  closePlanDialog=false;if(parts[1]!="0")return;
  CancelCompoundReplacement();Exit();RestoreCompoundDraft();CancelLayout();LayoutOutdated=false;
  LandscapeOpen=false;Open=false;SelectedPlan=EditingPlan=-1;NativeCommitted=false;NativeAuditAvailable=false;
  Boundary.Clear();Entrances.Clear();Entrance=null;Zones.Clear();ZonePoints.Clear();Ramps.Clear();ClearRoadEdit();VillaConnections.Clear();
  Draft=null;Preview.Clear();Mode="boundary";Revision++;
 }
}
}
