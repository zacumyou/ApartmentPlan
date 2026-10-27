using System;
using Colossal.Mathematics;
using Game;
using Game.Rendering;
using Unity.Mathematics;
using UnityEngine;
namespace ApartmentPlan {
public partial class PlanOverlay:GameSystemBase {

 readonly System.Collections.Generic.Dictionary<string,float> treeDiameters=new System.Collections.Generic.Dictionary<string,float>(StringComparer.Ordinal);
 float nextObservation;readonly System.Collections.Generic.List<StoredPlan> visiblePlans=new System.Collections.Generic.List<StoredPlan>();
 readonly Color[] colors=new Color[11];string palette="";float3 cameraPosition;bool hasCamera;
 protected override void OnUpdate(){var s=World.GetExistingSystemManaged<PlanSystem>();var o=Mod.Options;if(s==null||!s.Ready||o==null)return;
  if(UnityEngine.Time.unscaledTime>=nextObservation){nextObservation=UnityEngine.Time.unscaledTime+5;Trace.Sample("overlay.state","open="+s.Open+" draft="+s.Preview.Count+" saved="+s.Saved.Count+" showSaved="+o.ShowSaved+" editAlpha="+o.EditOpacity+" savedAlpha="+o.SavedOpacity);}
  var construction=World.GetExistingSystemManaged<ConstructionTool>();bool warnings=construction?.ShowWarnings==true;
  if(!warnings&&s.Preview.Count==0&&(!o.ShowSaved||s.Saved.Count==0)&&(!s.Open||s.Boundary.Count==0))return;
  bool drawDraft=s.Open&&(!s.NativeCommitted||s.Mode=="edit")&&World.GetExistingSystemManaged<Game.Tools.ToolSystem>()?.activeTool is PlanTool&&s.Draft!=null&&s.Preview.Count>0&&(s.EditingPlan<0||s.Draft.Parcels.Exists(r=>r.Kind==0));
  visiblePlans.Clear();foreach(var stored in s.Saved){bool editing=s.Open&&stored.Info.Number==s.EditingPlan;if((!o.ShowSaved&&!editing)||(editing&&drawDraft))continue;if(!s.IsBuilt(stored)||stored.Info.Number==s.HoverPlan||stored.Info.Number==s.SelectedPlan)visiblePlans.Add(stored);}
  if(!drawDraft&&visiblePlans.Count==0&&!warnings)return;
  var strings=o.Colors;string key=string.Join("|",strings);if(key!=palette){palette=key;for(int i=0;i<colors.Length;i++)if(!ColorUtility.TryParseHtmlString(strings[i],out colors[i]))colors[i]=Color.white;colors[7]=Color.white;}
  var camera=Camera.main;hasCamera=camera!=null;if(hasCamera)cameraPosition=camera.transform.position;
  treeDiameters.Clear();foreach(var species in s.Trees)if(!treeDiameters.ContainsKey(species.Id))treeDiameters.Add(species.Id,(float)Math.Max(2,species.Radius*2));
  var overlay=World.GetOrCreateSystemManaged<OverlayRenderSystem>();var buffer=overlay.GetBuffer(out var writer);writer.Complete();
  foreach(var p in visiblePlans){
   bool editing=s.Open&&p.Info.Number==s.EditingPlan;if(!o.ShowSaved&&!editing)continue;if(editing&&drawDraft)continue;
   bool active=p.Info.Number==s.HoverPlan||p.Info.Number==s.SelectedPlan;
   bool built=s.IsBuilt(p);
   if(!built){foreach(var shape in p.Shapes)if(p.Edit?.Draft?.Villa!=true||shape.Kind!=0)Draw(buffer,shape,active?Mathf.Max(.9f,o.SavedOpacity):o.SavedOpacity,o.ViewDistance);if(p.Edit?.Draft?.Villa==true)DrawVillaLots(buffer,p.Edit.Draft,p.Shapes.Count>0?p.Shapes[0].A.y:s.Elevation,o.ViewDistance);DrawTrees(buffer,p.TreePreview,s,o.ViewDistance);}
   if(active)foreach(var shape in p.Shapes)if(shape.Kind==7&&Mathf.Abs(shape.Width-.5f)<.001f){var edge=shape;edge.Width=1.2f;Draw(buffer,edge,1,o.ViewDistance);}
  }
  if(drawDraft){foreach(var shape in s.Preview)if(!s.Draft.Villa||shape.Kind!=0)Draw(buffer,shape,o.EditOpacity,o.ViewDistance);if(s.Draft.Villa)DrawVillaLots(buffer,s.Draft,s.Elevation,o.ViewDistance);
   DrawTrees(buffer,s.PlannedTrees,s,o.ViewDistance);
  }
  if(warnings)construction.DrawWarnings(buffer,cameraPosition,hasCamera,o.ViewDistance);
  overlay.AddBufferWriter(default);
 }
 void DrawVillaLots(OverlayRenderSystem.Buffer buffer,Plan plan,float elevation,float distance){
  foreach(var lot in plan.Parcels){if(lot.Kind!=0||!lot.PlacementForward.HasValue)continue;
   var center=new Point(lot.X+lot.W/2,lot.Y+lot.D/2);var world=plan.World(center);
   if(hasCamera&&math.distance(new float3((float)world.X,elevation,(float)world.Y),cameraPosition)>distance)continue;
   var full=VillaGeometry.Box(center,lot.PlacementForward.Value,lot.W,lot.D);
   var adjusted=lot.PlacementOutline??full;
   float3 W(Point q){var w=plan.World(q);return new float3((float)w.X,elevation+.03f,(float)w.Y);}
   void Fill(Point[] q,float alpha){var color=colors[0];color.a=alpha;buffer.DrawLine(color,color,0,OverlayRenderSystem.StyleFlags.Projected,new Line3.Segment(W((q[0]+q[1])*.5),W((q[2]+q[3])*.5)),(float)(q[1]-q[0]).Length,new float2(0));}
   Fill(full,.32f);Fill(adjusted,.12f);
   var border=colors[0];border.a=1;
   for(int i=0;i<adjusted.Length;i++){var a=adjusted[i];var delta=adjusted[(i+1)%adjusted.Length]-a;double length=delta.Length;
    for(double t=0;t<length;t+=1.2){var b=a+delta*(Math.Min(length,t+.35)/length);var start=a+delta*(t/length);buffer.DrawLine(border,border,0,OverlayRenderSystem.StyleFlags.Projected,new Line3.Segment(W(start),W(b)),.22f,new float2(0));}
   }
  }
 }
 void DrawTrees(OverlayRenderSystem.Buffer buffer,System.Collections.Generic.List<PlannedTree> trees,PlanSystem s,float distance){
  foreach(var t in trees){var point=new float3((float)t.Position.X,s.Elevation,(float)t.Position.Y);if(hasCamera&&math.distance(point,cameraPosition)>distance)continue;
   float diameter=treeDiameters.TryGetValue(t.Species,out var knownDiameter)?knownDiameter:4;
   buffer.DrawCircle(new Color(.1f,.3f,.16f,.9f),new Color(.19f,.46f,.23f,.35f),.25f,OverlayRenderSystem.StyleFlags.Projected,new float2(0,1),point,diameter);
  }
 }
 void Draw(OverlayRenderSystem.Buffer buffer,PlanShape shape,float opacity,float distance){if(hasCamera){float3 center=(shape.A+shape.B)*.5f;if(math.distance(center,cameraPosition)>distance+math.distance(shape.A,shape.B))return;}if(shape.Kind<0||shape.Kind>=colors.Length)return;var color=colors[shape.Kind];color.a=math.clamp(opacity,0,1);buffer.DrawLine(color,color,0,OverlayRenderSystem.StyleFlags.Projected,new Line3.Segment(shape.A,shape.B),shape.Width,new float2(0));}
}
}




