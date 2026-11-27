using Game;
using Game.Tools;
using Game.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ApartmentPlan {
public partial class PlanSystem {
 readonly PlanPickGrid pickGrid=new PlanPickGrid();
 int indexedCount=-1;
 void RebuildPickIndex(){
  pickGrid.Clear();indexedCount=Saved.Count;
  for(int i=0;i<Saved.Count;i++){
   var plan=Saved[i];plan.HitBoundary=plan.Summary?.Boundary??StoredBoundary(plan);
   plan.BoundaryEditable=Layout.Valid(plan.HitBoundary);
   if(plan.Shapes.Count==0)continue;
   var low=plan.Shapes[0].A;var high=low;
   foreach(var shape in plan.Shapes){
    low=Unity.Mathematics.math.min(low,Unity.Mathematics.math.min(shape.A,shape.B));
    high=Unity.Mathematics.math.max(high,Unity.Mathematics.math.max(shape.A,shape.B));
   }
   plan.BoundsMin=low;plan.BoundsMax=high;
   if(plan.HitBoundary.Count<3)continue;
   var bounds=VillaGeometry.Bounds(plan.HitBoundary);
   pickGrid.Add(i,bounds);
  }
 }
 internal System.Collections.Generic.List<int> NearbyPlans(Ray ray,float range){
  if(Saved.Count!=indexedCount)RebuildPickIndex();
  var a=ray.origin;var b=ray.GetPoint(range);
  return pickGrid.Query(new Point(a.x,a.z),new Point(b.x,b.z));
 }
}
// Saved plans are metadata, not fake buildings. Pick their actual overlay
// footprint without inserting a prefab into the game's selection search tree.
public partial class PlanSelection:GameSystemBase {
 Vector2 pressPosition;int pressPlan=-1;
 protected override void OnUpdate(){
  var s=World.GetExistingSystemManaged<PlanSystem>();if(s==null||!s.Ready)return;s.HoverPlan=-1;
  var input=Game.Input.InputManager.instance;
  if(Mod.Options==null||!Mod.Options.ShowSaved||s.Focused||input.mouseOverUI||input.overlayActive||input.hasInputFieldFocus||!Application.isFocused||Mouse.current==null||!(World.GetExistingSystemManaged<ToolSystem>()?.activeTool is DefaultToolSystem)){pressPlan=-2;return;}
  var camera=World.GetExistingSystemManaged<CameraUpdateSystem>()?.activeCamera;if(camera==null)return;
  var ray=camera.ScreenPointToRay(Mouse.current.position.ReadValue());double smallest=double.MaxValue;
  foreach(int index in s.NearbyPlans(ray,Mod.Options.ViewDistance)){var plan=s.Saved[index];if(plan.Shapes.Count==0||plan.Info.Number==s.EditingPlan&&s.Open)continue;
   float height=plan.Shapes[0].A.y;if(!new Plane(Vector3.up,new Vector3(0,height,0)).Raycast(ray,out float distance)||distance>Mod.Options.ViewDistance)continue;
   // Follow the terrain-projected overlay on ordinary slopes rather than
   // keeping the original flat drafting elevation for the selection hit.
   var terrain=World.GetOrCreateSystemManaged<Game.Simulation.TerrainSystem>().GetHeightData();
   for(int i=0;i<4;i++){var sample=ray.GetPoint(distance);var p=new Unity.Mathematics.float3(sample.x,sample.y,sample.z);float ground=Game.Simulation.TerrainUtils.SampleHeight(ref terrain,p);if(!Unity.Mathematics.math.isfinite(ground)||Mathf.Abs(ray.direction.y)<.05f)break;float next=(ground-ray.origin.y)/ray.direction.y;if(next<0||next>Mod.Options.ViewDistance)break;distance=next;}
   var position=ray.GetPoint(distance);var point=new Point(position.x,position.z);
   var boundary=plan.HitBoundary;
   if(boundary.Count>=3&&Layout.Inside(point,boundary)&&!(plan.Summary?.Exclusions?.Exists(z=>z.Kind==5&&Layout.Inside(point,z.Points))??false)&&plan.Info.Area<smallest){smallest=plan.Info.Area;s.HoverPlan=plan.Info.Number;}
  }
  if(Mouse.current.leftButton.wasPressedThisFrame){pressPosition=Mouse.current.position.ReadValue();pressPlan=s.HoverPlan;}
  if(Mouse.current.leftButton.wasReleasedThisFrame&&Vector2.Distance(pressPosition,Mouse.current.position.ReadValue())<5&&pressPlan==s.HoverPlan){var selected=World.GetExistingSystemManaged<ToolSystem>()?.selected??Unity.Entities.Entity.Null;if(selected!=Unity.Entities.Entity.Null){if(s.SelectedPlan>=0){s.SelectedPlan=-1;s.Focused=false;s.Revision++;}return;}if(s.HoverPlan>=0)s.Request("inspectPlan",s.HoverPlan.ToString());else if(s.SelectedPlan>=0)s.Request("closeInspector");}
 }
}
}
