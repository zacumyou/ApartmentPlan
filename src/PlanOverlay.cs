using System;
using Colossal.Mathematics;
using Game;
using Game.Rendering;
using Unity.Mathematics;
using UnityEngine;
namespace ApartmentPlan {
public partial class PlanOverlay:GameSystemBase {

 readonly System.Collections.Generic.Dictionary<string,float> treeDiameters=new System.Collections.Generic.Dictionary<string,float>(StringComparer.Ordinal);
 readonly LocalPerformanceLog performance=new LocalPerformanceLog();
 double writerWaitMs,labelCpuMs;int treeCatalogCount=-1;
 bool measuring;int testedCount,culledCount,lodCount,submittedCount;
 protected override void OnUpdate(){
  BeginLabels();var now=UnityEngine.Time.realtimeSinceStartupAsDouble;measuring=performance.Enabled(now);
  if(!measuring){try{DrawOverlay();}finally{EndLabels();}return;}
  writerWaitMs=labelCpuMs=0;testedCount=culledCount=lodCount=submittedCount=0;long start=System.Diagnostics.Stopwatch.GetTimestamp();
  try{DrawOverlay();}finally{EndLabels();performance.Add(now,(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency,UnityEngine.Time.unscaledDeltaTime*1000,testedCount,culledCount,lodCount,submittedCount,writerWaitMs,labelCpuMs);}
 }
 readonly Plane[] viewPlanes=new Plane[6];
 float3 cameraForward;float pixelsPerUnit;bool orthographic;
 // Projected overlays follow terrain; retain a generous vertical envelope.
 bool InView(float3 a,float3 b,float width,float distance){
  if(measuring)testedCount++;if(!hasCamera)return true;var center=(a+b)*.5f;var size=math.abs(b-a)+new float3(Math.Max(0,width),256,Math.Max(0,width));
  var radius=math.length(size)*.5f;if(math.lengthsq(center-cameraPosition)>(distance+radius)*(distance+radius)){if(measuring)culledCount++;return false;}
  bool visible=GeometryUtility.TestPlanesAABB(viewPlanes,new Bounds((Vector3)center,(Vector3)size));if(measuring&&!visible)culledCount++;return visible;
 }
 bool DetailVisible(float size,float3 center,float minimumPixels){bool visible=!hasCamera||OverlayDetailPolicy.Visible(size,math.dot(center-cameraPosition,cameraForward),pixelsPerUnit,orthographic,minimumPixels);if(measuring&&!visible)lodCount++;return visible;}
 float nextObservation;readonly System.Collections.Generic.List<StoredPlan> visiblePlans=new System.Collections.Generic.List<StoredPlan>();
 readonly Color[] colors=new Color[11];string palette="";float3 cameraPosition;bool hasCamera;
 void DrawOverlay(){var s=World.GetExistingSystemManaged<PlanSystem>();var o=Mod.Options;if(s==null||!s.Ready||o==null)return;
  if(UnityEngine.Time.unscaledTime>=nextObservation){nextObservation=UnityEngine.Time.unscaledTime+5;Trace.Sample("overlay.state","open="+s.Open+" draft="+s.Preview.Count+" saved="+s.Saved.Count+" showSaved="+o.ShowSaved+" editAlpha="+o.EditOpacity+" savedAlpha="+o.SavedOpacity);}
  var construction=World.GetExistingSystemManaged<ConstructionTool>();bool warnings=construction?.ShowWarnings==true;
  if(!warnings&&s.Preview.Count==0&&(!o.ShowSaved||s.Saved.Count==0)&&(!s.Open||s.Boundary.Count==0))return;
  bool drawDraft=s.Open&&(!s.NativeCommitted||s.Mode=="edit")&&World.GetExistingSystemManaged<Game.Tools.ToolSystem>()?.activeTool is PlanTool&&s.Draft!=null&&s.Preview.Count>0&&(s.EditingPlan<0||s.Draft.Parcels.Exists(r=>r.Kind==0));
  visiblePlans.Clear();foreach(var stored in s.Saved){bool editing=s.Open&&stored.Info.Number==s.EditingPlan;if(stored.Hidden&&!editing)continue;if((!o.ShowSaved&&!editing)||(editing&&drawDraft))continue;if(!s.IsBuilt(stored)||stored.Info.Number==s.HoverPlan||stored.Info.Number==s.SelectedPlan)visiblePlans.Add(stored);}
  if(!drawDraft&&visiblePlans.Count==0&&!warnings&&!(s.Open&&s.Mode=="road"))return;
  var strings=o.Colors;string key=string.Join("|",strings);if(key!=palette){palette=key;for(int i=0;i<colors.Length;i++)if(!ColorUtility.TryParseHtmlString(strings[i],out colors[i]))colors[i]=Color.white;colors[7]=Color.white;}
  var camera=Camera.main;hasCamera=camera!=null;if(hasCamera){cameraPosition=camera.transform.position;cameraForward=camera.transform.forward;orthographic=camera.orthographic;
   pixelsPerUnit=camera.pixelHeight/(2f*(orthographic?Mathf.Max(.001f,camera.orthographicSize):Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)));
   GeometryUtility.CalculateFrustumPlanes(camera,viewPlanes);
  }
  if(treeCatalogCount!=s.Trees.Count){treeCatalogCount=s.Trees.Count;treeDiameters.Clear();foreach(var species in s.Trees)if(!treeDiameters.ContainsKey(species.Id))treeDiameters.Add(species.Id,(float)Math.Max(2,species.Radius*2));}
  var overlay=World.GetOrCreateSystemManaged<OverlayRenderSystem>();var buffer=overlay.GetBuffer(out var writer);long waitStart=measuring?System.Diagnostics.Stopwatch.GetTimestamp():0;writer.Complete();if(measuring)writerWaitMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-waitStart)*1000.0/System.Diagnostics.Stopwatch.Frequency;
  foreach(var p in visiblePlans){
   bool editing=s.Open&&p.Info.Number==s.EditingPlan;if(!o.ShowSaved&&!editing)continue;if(editing&&drawDraft)continue;
   bool active=p.Info.Number==s.HoverPlan||p.Info.Number==s.SelectedPlan;
   bool built=s.IsBuilt(p);
   if(!built){foreach(var shape in p.Shapes)if(p.Edit?.Draft?.Villa!=true||shape.Kind!=0)Draw(buffer,shape,active?Mathf.Max(.9f,o.SavedOpacity):o.SavedOpacity,o.ViewDistance);if(p.Edit?.Draft?.Villa==true)DrawVillaLots(buffer,p.Edit.Draft,p.Shapes.Count>0?p.Shapes[0].A.y:s.Elevation,o.ViewDistance);DrawTrees(buffer,p.TreePreview,s,o.ViewDistance);DrawLabels(overlay,s,p.Edit?.Draft,camera,o.SavedOpacity);}
   if(active)foreach(var shape in p.Shapes)if(shape.Kind==7&&Mathf.Abs(shape.Width-.5f)<.001f){var edge=shape;edge.Width=1.2f;Draw(buffer,edge,1,o.ViewDistance);}
  }
  if(drawDraft){foreach(var shape in s.Preview)if(!s.Draft.Villa||shape.Kind!=0)Draw(buffer,shape,o.EditOpacity,o.ViewDistance);if(s.Draft.Villa)DrawVillaLots(buffer,s.Draft,s.Elevation,o.ViewDistance);
   DrawTrees(buffer,s.PlannedTrees,s,o.ViewDistance);DrawLabels(overlay,s,s.Draft,camera,o.EditOpacity);
  }
  if(s.Open&&World.GetExistingSystemManaged<Game.Tools.ToolSystem>()?.activeTool is PlanTool planTool){int previewCount=planTool.DrawRoadPreview(buffer,s);if(measuring)submittedCount+=previewCount;}
  if(warnings)construction.DrawWarnings(buffer,cameraPosition,hasCamera,o.ViewDistance);
  overlay.AddBufferWriter(default);
 }
 void DrawVillaLots(OverlayRenderSystem.Buffer buffer,Plan plan,float elevation,float distance){
  foreach(var lot in plan.Parcels){if(lot.Kind!=0||!lot.PlacementForward.HasValue)continue;
   var center=new Point(lot.X+lot.W/2,lot.Y+lot.D/2);var world=plan.World(center);
   if(!InView(new float3((float)world.X,elevation,(float)world.Y),new float3((float)world.X,elevation,(float)world.Y),(float)Math.Sqrt(lot.W*lot.W+lot.D*lot.D),distance))continue;
   var full=VillaGeometry.Box(center,lot.PlacementForward.Value,lot.W,lot.D);
   var adjusted=lot.PlacementOutline??full;
   float3 W(Point q){var w=plan.World(q);return new float3((float)w.X,elevation+.03f,(float)w.Y);}
   void Fill(Point[] q,float alpha){var color=colors[0];color.a=alpha;if(measuring)submittedCount++;buffer.DrawLine(color,color,0,OverlayRenderSystem.StyleFlags.Projected,new Line3.Segment(W((q[0]+q[1])*.5),W((q[2]+q[3])*.5)),(float)(q[1]-q[0]).Length,new float2(0));}
   Fill(full,.32f);Fill(adjusted,.12f);
   var border=colors[0];border.a=1;
   for(int i=0;i<adjusted.Length;i++){var a=adjusted[i];var delta=adjusted[(i+1)%adjusted.Length]-a;double length=delta.Length;
    for(double t=0;t<length;t+=1.2){var b=a+delta*(Math.Min(length,t+.35)/length);var start=a+delta*(t/length);if(measuring)submittedCount++;buffer.DrawLine(border,border,0,OverlayRenderSystem.StyleFlags.Projected,new Line3.Segment(W(start),W(b)),.22f,new float2(0));}
   }
  }
 }
 void DrawTrees(OverlayRenderSystem.Buffer buffer,System.Collections.Generic.List<PlannedTree> trees,PlanSystem s,float distance){
  int index=0;foreach(var t in trees){int ordinal=index++;var point=new float3((float)t.Position.X,s.Elevation,(float)t.Position.Y);
   float diameter=treeDiameters.TryGetValue(t.Species,out var knownDiameter)?knownDiameter:4;
   if(!s.LandscapeOpen&&hasCamera&&!OverlayDetailPolicy.Visible(diameter,math.dot(point-cameraPosition,cameraForward),pixelsPerUnit,orthographic,24)&&ordinal%OverlayDetailPolicy.TreeStride(trees.Count)!=0){if(measuring)lodCount++;continue;}
   if(!InView(point,point,diameter,distance)||!DetailVisible(diameter,point,6))continue;
   if(measuring)submittedCount++;buffer.DrawCircle(new Color(.1f,.3f,.16f,.9f),new Color(.19f,.46f,.23f,.35f),.25f,OverlayRenderSystem.StyleFlags.Projected,new float2(0,1),point,diameter);
  }
 }
 void Draw(OverlayRenderSystem.Buffer buffer,PlanShape shape,float opacity,float distance){if(opacity<=0||shape.Kind<0||shape.Kind>=colors.Length)return;
  // Thin parking/dash details only. Boundary width .5 and active width1.2 bypass LOD.
  if(shape.Kind==7&&shape.Width<.2f&&(!DetailVisible(shape.Width,(shape.A+shape.B)*.5f,.75f)||!DetailVisible(math.distance(shape.A,shape.B),(shape.A+shape.B)*.5f,3)))return;if(!InView(shape.A,shape.B,shape.Width,distance))return;var color=colors[shape.Kind];color.a=math.clamp(opacity,0,1);if(measuring)submittedCount++;buffer.DrawLine(color,color,0,OverlayRenderSystem.StyleFlags.Projected,new Line3.Segment(shape.A,shape.B),shape.Width,new float2(0));}
}
}






