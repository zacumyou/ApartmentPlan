using System;
using System.Collections.Generic;
using System.Reflection;
using Game;
using Game.Audio;
using Game.Effects;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
namespace ApartmentPlan {
internal enum PlanSound {Point,Finish,Remove,Select,Cancel,RoadStart,RoadNode,Build,Demolish,Fail}
public partial class PlanAudio:GameSystemBase {
 EntityQuery settings;
 readonly List<SourceUpdateInfo> retained=new List<SourceUpdateInfo>();
 static readonly FieldInfo queueField=typeof(AudioManager).GetField("m_SourceUpdateQueue",BindingFlags.Instance|BindingFlags.NonPublic);
 readonly BatchSnapSound batch=new BatchSnapSound();
 ConstructionTool run;int generation;bool unavailable,playbackFailed;
 protected override void OnCreate(){base.OnCreate();settings=GetEntityQuery(ComponentType.ReadOnly<ToolUXSoundSettingsData>());}
 internal void Play(PlanSound kind){
  if(playbackFailed||settings.IsEmptyIgnoreFilter)return;
  try{
  var s=settings.GetSingleton<ToolUXSoundSettingsData>();Entity clip;
  switch(kind){
   case PlanSound.Point:clip=s.m_PolygonToolDropPointSound;break;
   case PlanSound.Finish:clip=s.m_PolygonToolFinishAreaSound;break;
   case PlanSound.Remove:clip=s.m_PolygonToolRemovePointSound;break;
   case PlanSound.Select:clip=s.m_SelectEntitySound;break;
   case PlanSound.Cancel:clip=s.m_NetCancelSound;break;
   case PlanSound.RoadStart:clip=s.m_NetStartSound;break;
   case PlanSound.RoadNode:clip=s.m_NetNodeSound;break;
   case PlanSound.Build:clip=s.m_PlaceBuildingSound;break;
   case PlanSound.Demolish:clip=s.m_BulldozeSound;break;
   default:clip=s.m_PlaceBuildingFailSound;break;
  }
  if(clip!=Entity.Null)World.GetOrCreateSystemManaged<AudioManager>().PlayUISound(clip);
  }catch(Exception error){playbackFailed=true;Trace.Failure("audio.play.unavailable",error);}
 }
 protected override void OnUpdate(){
  // Only two native producers call AddSnap: the active net tool and temporary
  // building road connections. AP exclusively owns the tool during this batch.
  var tool=World.GetExistingSystemManaged<ToolSystem>()?.activeTool as ConstructionTool;
  if(tool==null){run=null;return;}
  if(run!=tool||generation!=tool.RunGeneration){run=tool;generation=tool.RunGeneration;batch.Reset();}
  if(unavailable)return;
  if(queueField==null||queueField.FieldType!=typeof(NativeQueue<SourceUpdateInfo>)){
   unavailable=true;Trace.Event("audio.coalesce.unavailable","Native audio queue contract changed; audio left untouched");return;
  }
  var audio=World.GetOrCreateSystemManaged<AudioManager>();
  audio.GetSourceUpdateData(out var writers);writers.Complete();
  var queue=(NativeQueue<SourceUpdateInfo>)queueField.GetValue(audio);
  if(!queue.IsCreated||queue.Count==0)return;
  retained.Clear();int removed=0;
  while(queue.TryDequeue(out var item)){
   if(!batch.Keep(item.m_Type==SourceUpdateType.Snap)){removed++;continue;}
   retained.Add(item);
  }
  foreach(var item in retained)queue.Enqueue(item);
  retained.Clear();
  if(removed>0)Trace.Event("audio.snap.coalesced","generation="+generation+" suppressed="+removed);
 }
}
public partial class PlanSystem {
 internal void Sound(PlanSound sound)=>World.GetOrCreateSystemManaged<PlanAudio>().Play(sound);
 void Command(string command,string value){
  var mode=Mode;var status=Status;int count=Boundary.Count+ZonePoints.Count+Entrances.Count+Roads.Count+RoadPoints.Count+Zones.Count;bool native=World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool;
  CommandCore(command,value);
  if(command=="finish"||command=="finishEntrances") {if(mode!=Mode)Sound(PlanSound.Finish);}
  else if(command=="undo"&&count!=Boundary.Count+ZonePoints.Count+Entrances.Count+Roads.Count+RoadPoints.Count+Zones.Count)Sound(PlanSound.Remove);
  else if((command=="roadCancel"||command=="nativeCancel")&&(mode!=Mode||native))Sound(PlanSound.Cancel);
  else if(command=="save"&&Status=="AP.Saved")Sound(PlanSound.Finish);
  else if(Status!=status&&StatusError)Sound(PlanSound.Fail);
 }
 internal void Click(Unity.Mathematics.float3 position){
  var mode=Mode;var status=Status;int boundary=Boundary.Count,zone=ZonePoints.Count,gates=Entrances.Count,road=RoadPoints.Count,segments=RoadDraft.Roads.Count,replacement=ReplacementIndex;
  ClickCore(position);
  if(Boundary.Count>boundary||ZonePoints.Count>zone)Sound(PlanSound.Point);
  else if(Entrances.Count>gates||ReplacementIndex>=0&&ReplacementIndex!=replacement)Sound(PlanSound.Select);
  else if(mode=="road"&&(road!=RoadPoints.Count||segments!=RoadDraft.Roads.Count))Sound(road==0?PlanSound.RoadStart:PlanSound.RoadNode);
  else if(mode=="zone"&&Mode!=mode)Sound(PlanSound.Finish);
  else if(Status!=status&&StatusError)Sound(PlanSound.Fail);
 }
}
}
