using Game.Tools;
using Unity.Entities;
namespace ApartmentPlan {
public partial class PlanSystem {
 // Compound selection is metadata-only: never select a representative building.
 void SelectCompoundInfo(){World.GetOrCreateSystemManaged<ToolSystem>().selected=Entity.Null;}
 void CloseCompoundInfo(){SelectedPlan=-1;Focused=false;}
 void TickCompoundInfo(){
  if(SelectedPlan<0)return;
  var tools=World.GetExistingSystemManaged<ToolSystem>();
  if(Open||SelectedStored==null||tools!=null&&tools.selected!=Entity.Null){CloseCompoundInfo();Revision++;}
 }
}
}
