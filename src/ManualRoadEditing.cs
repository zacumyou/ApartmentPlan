using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public partial class PlanSystem {
 internal readonly List<ManualRoad> Roads=new List<ManualRoad>();
 internal readonly RoadSketch RoadDraft=new RoadSketch();
 internal List<Point> RoadPoints=>RoadDraft.Points;
 internal bool RoadCurved=>RoadDraft.Curved;
 internal string RoadHint=>RoadPoints.Count==0?"AP.RoadStart":RoadCurved&&RoadPoints.Count==1?"AP.RoadControl":"AP.RoadEnd";
 void ClearRoadEdit(){Roads.Clear();RoadDraft.Clear();}
 void ClickRoad(Point point){
  if(NativeCommitted||Draft==null)return;
  if(!Layout.Inside(point,Boundary)||RoadPoints.Count>0&&(point-RoadPoints.Last()).Length<4){Status="AP.ManualRoadFit";Revision++;return;}
  if(Roads.Count+RoadDraft.Roads.Count>=PlanLimits.Roads){SetLimit("AP.LimitRoads",Roads.Count+RoadDraft.Roads.Count+1,PlanLimits.Roads);Revision++;return;}
  try{RoadDraft.Add(point);}catch(ArgumentException){Status="AP.ManualRoadFit";Revision++;return;}
  Status=RoadHint;Revision++;
 }
 void UndoRoad(){
  RoadDraft.Undo();
  Status=RoadHint;
 }
 void FinishRoad(){
  if(LayoutPending||NativeCommitted)return;
  if(RoadPoints.Count>1){Status="AP.RoadIncomplete";return;}
  if(RoadDraft.Roads.Count==0){RoadPoints.Clear();Mode="preview";Status="AP.Ready";return;}
  var batch=RoadDraft.Roads.ToArray();var points=RoadPoints.ToArray();
  Roads.AddRange(batch);RoadDraft.Clear();Mode="preview";
  Action reject=()=>{foreach(var road in batch)Roads.Remove(road);RoadDraft.Clear();RoadDraft.Roads.AddRange(batch);RoadDraft.Samples.AddRange(batch.Select(r=>r.Sample()));RoadPoints.AddRange(points);Mode="road";};
  Rebuild(reject);
  if(!LayoutPending){reject();LayoutOutdated=false;}
  Trace.Event("road.batch","segments="+batch.Length+" queued="+LayoutPending);
 }
}
}
