using System;
namespace ApartmentPlan {
public partial class PlanSystem {
 void PickBuilding(Point world){
  if(!Ready||CatalogLoading||LayoutPending||Draft==null||Mode!="replace")return;
  ClearForcedBuilding();
  var local=Draft.Local(world);var mouse=UnityEngine.InputSystem.Mouse.current?.position.ReadValue()??UnityEngine.Vector2.zero;
  ReplacementScreenX=mouse.x/Math.Max(1,UnityEngine.Screen.width);ReplacementScreenY=1-mouse.y/Math.Max(1,UnityEngine.Screen.height);
  ReplacementIndex=Draft.Parcels.FindIndex(r=>r.Kind==0&&local.X>=r.X&&local.X<=r.X+r.W&&local.Y>=r.Y&&local.Y<=r.Y+r.D);
  Status=ReplacementIndex>=0?"AP.ChooseReplacement":"AP.PickBuilding";Revision++;
 }
}
}
