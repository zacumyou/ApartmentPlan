using System;
using System.Collections.Generic;
using System.Linq;
using Game.Common;
using Unity.Entities;
namespace ApartmentPlan {
public partial class PlanSystem {
 int replacementPlan=-1,replacementPreviewFrames;
 internal bool ReplacingCompound(int id)=>replacementPlan==id;
 void CancelCompoundReplacement(){replacementPlan=-1;replacementPreviewFrames=0;}
 void TickCompoundReplacement(){
  if(replacementPreviewFrames>0&&--replacementPreviewFrames==0){replacementPlan=-1;Request("nativePreview");}
 }
 bool BeginCompoundReplacement(){
  var stored=Saved.Find(p=>p.Info.Number==EditingPlan);
  if(stored==null||!EntityManager.HasBuffer<PlanOwnedEntity>(stored.Entity)){Status="AP.NoOwnership";return false;}
  int shapes=Saved.Where(p=>p.Info.Number!=EditingPlan).Sum(p=>p.Shapes.Count)+Preview.Count+Boundary.Count;
  if(shapes>PlanLimits.TotalShapes){SetLimit("AP.LimitTotalShapes",shapes,PlanLimits.TotalShapes);return false;}
  if(!SaveCompoundPending(false)||stored.Edit?.Pending==null)return false;
  replacementPlan=EditingPlan;
  // Resume a saved pending edit if deletion finished before the metadata checkpoint.
  if(!HasOwnedObjects(stored)){FinishCompoundReplacement(stored);return true;}
  SelectedPlan=EditingPlan;BeginDemolition();
  if(SelectedPlan>=0){CancelCompoundReplacement();return false;}return true;
 }
 bool FinishCompoundReplacement(StoredPlan stored){
  if(!ReplacingCompound(stored.Info.Number))return false;
  // Keep the same plan entity and its pending edit. City saves retain the plan even
  // if construction is cancelled or the game exits after demolition.
  var edit=stored.Edit?.Pending;if(edit==null){Status="AP.Error";CancelCompoundReplacement();return true;}
  edit.Pending=null;edit.AppliedPlans=null;edit.NativeCommitted=false;WriteCompoundEdit(stored,edit);
  if(EntityManager.HasComponent<PlanConstructionState>(stored.Entity))EntityManager.SetComponentData(stored.Entity,new PlanConstructionState{Applied=0});
  if(EntityManager.HasBuffer<PlanBuiltRoad>(stored.Entity))EntityManager.GetBuffer<PlanBuiltRoad>(stored.Entity).Clear();
  if(EntityManager.HasComponent<PlanGrassWork>(stored.Entity))EntityManager.RemoveComponent<PlanGrassWork>(stored.Entity);
  NativeCommitted=false;Save(true);Activate();replacementPreviewFrames=3;Status="AP.ReplacementPreparing";Revision++;
  Trace.Event("plan.replacement.cleared","id="+stored.Info.Number);return true;
 }
 Plan compoundFullDraft;internal bool CompoundApplying=>compoundFullDraft!=null;
 internal bool CompoundEditing=>EditingPlan>=0&&Saved.Any(p=>p.Info.Number==EditingPlan&&IsBuilt(p));
 internal List<Plan> AppliedCompoundPlans(){var stored=Saved.Find(p=>p.Info.Number==EditingPlan);return stored?.Edit?.AppliedPlans??new List<Plan>{stored?.Edit?.Draft};}
 bool SaveCompoundPending(bool notify=true){
  if(!CompoundEditing)return false;var stored=Saved.Find(p=>p.Info.Number==EditingPlan);if(stored?.Edit==null)return true;
  var edit=ReadEdit(stored.Entity);var pending=PlanEditCodec.Decode(CaptureEdit());pending.NativeCommitted=false;pending.Name=edit.Name;edit.Pending=pending;
  WriteCompoundEdit(stored,edit);if(notify)Status="AP.CompoundPendingSaved";Revision++;return true;
 }
 void WriteCompoundEdit(StoredPlan stored,PlanEditData edit){var bytes=PlanEditCodec.Encode(edit);if(bytes.Length>MaxEditBytes)throw new InvalidOperationException("Compound edit exceeds save budget");var b=EntityManager.GetBuffer<PlanEditByte>(stored.Entity);b.Clear();foreach(var value in bytes)b.Add(new PlanEditByte{Value=value});stored.Edit=edit;}
 bool PrepareCompoundAdditions(){
  if(!CompoundEditing)return true;
  if(CompoundApplying){Status="AP.NativeChecking";return false;}
  var applied=AppliedCompoundPlans();if(applied.Any(p=>p==null)){Status="AP.LegacyUneditable";return false;}
  var next=PlanEditCodec.Decode(PlanEditCodec.Encode(new PlanEditData{Draft=Draft})).Draft;
  CompoundAdditions.Filter(next,applied);
  if(!next.Parcels.Any(p=>p.Kind==0||p.Kind==2&&p.ParkingSpaces>0)&&next.AdditionCourses.Count==0){Status="AP.NoCompoundAdditions";Revision++;return false;}
  var check=PlanEditCodec.Decode(CaptureEdit());check.AppliedPlans=applied.Concat(new[]{next}).ToList();check.NativeCommitted=true;
  if(PlanEditCodec.Encode(check).Length>MaxEditBytes){Status="AP.AdditionSaveLimit";Revision++;return false;}
  compoundFullDraft=Draft;Draft=next;treePlan=null;treeKey=null;RenderDraft();return true;
 }
 void RestoreCompoundDraft(){if(compoundFullDraft==null)return;Draft=compoundFullDraft;compoundFullDraft=null;NativeCommitted=false;treePlan=null;treeKey=null;RenderDraft();Revision++;}
 void FinishCompoundAddition(StoredPlan stored){
  if(!CompoundApplying)return;
  var edit=ReadEdit(stored.Entity);var applied=edit.AppliedPlans??new List<Plan>{edit.Draft};applied.Add(Draft);
  var next=edit.Pending;next.Pending=null;next.AppliedPlans=applied;next.NativeCommitted=true;
  WriteCompoundEdit(stored,next);Draft=compoundFullDraft;compoundFullDraft=null;NativeCommitted=true;
  RenderDraft();var shapes=EntityManager.GetBuffer<PlanShape>(stored.Entity);shapes.Clear();foreach(var shape in Preview)shapes.Add(shape);
  for(int i=0;i<Boundary.Count;i++){var a=Boundary[i];var b=Boundary[(i+1)%Boundary.Count];shapes.Add(new PlanShape{A=new Unity.Mathematics.float3((float)a.X,Elevation,(float)a.Y),B=new Unity.Mathematics.float3((float)b.X,Elevation,(float)b.Y),Width=.5f,Kind=7});}
  stored.Shapes.Clear();for(int i=0;i<shapes.Length;i++)stored.Shapes.Add(shapes[i]);var info=stored.Info;info.Area=(float)Draft.Area;info.Homes=applied.Sum(p=>p.Homes);info.Far=info.Area>0?(float)(applied.Sum(p=>p.Parcels.Where(x=>x.Kind==0).Sum(x=>x.FloorArea))/info.Area*100):0;stored.Info=info;EntityManager.SetComponentData(stored.Entity,info);
 }
 internal bool ExistingCompoundPoint(Point world)=>CompoundApplying&&AppliedCompoundPlans().Any(p=>Layout.Inside(p.Local(world),p.Boundary));
}
}
