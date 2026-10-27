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
 internal readonly List<Dictionary<string,int>> ManualRoadEvidence=new List<Dictionary<string,int>>();
 void RecordManualRoadFailure(ArgumentException error){
  ManualRoadEvidence.Clear();
  if(error is ManualRoadFailure failure)ManualRoadEvidence.Add(new Dictionary<string,int>(failure.Evidence));
  else ManualRoadEvidence.Add(new Dictionary<string,int>{{"reason",29},{"ordinal",-1}});
  Trace.Event("road.failure.fit",Newtonsoft.Json.JsonConvert.SerializeObject(ManualRoadEvidence));
 }
 void ClearRoadEdit(){Roads.Clear();RoadDraft.Clear();ManualRoadEvidence.Clear();}
 void ClickRoad(Point point){
  if(NativeCommitted||!Layout.Valid(Boundary))return;
  int reason=!Layout.Inside(point,Boundary)?24:RoadPoints.Count>0&&(point-RoadPoints.Last()).Length<4?25:0;
  if(reason!=0){RecordManualRoadFailure(new ManualRoadFailure(reason,Roads.Count+RoadDraft.Roads.Count,RoadPoints.Count+1,RoadCurved));Status="AP.ManualRoadFit";Revision++;return;}
  if(Roads.Count+RoadDraft.Roads.Count>=PlanLimits.Roads){SetLimit("AP.LimitRoads",Roads.Count+RoadDraft.Roads.Count+1,PlanLimits.Roads);Revision++;return;}
  try{RoadDraft.Add(point);}catch(ArgumentException error){RecordManualRoadFailure(error);Status="AP.ManualRoadFit";Revision++;return;}
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
  if(!LayoutPending&&Mod.Options.AutoCalculate){reject();LayoutOutdated=false;}
  Trace.Event("road.batch","segments="+batch.Length+" queued="+LayoutPending);
 }
}
}
