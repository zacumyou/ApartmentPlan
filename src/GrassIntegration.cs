using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
namespace ApartmentPlan {
public struct PlanGrassWork:IComponentData,ISerializable {
 public int State; // 0 pending (repaint safely after load), 1 complete, 2 interrupted
 public void Serialize<T>(T w) where T:IWriter{w.Write(State);}
 public void Deserialize<T>(T r) where T:IReader{r.Read(out State);}
}
// Optional dependency. Only public painting methods mutate Grass Sprites;
// read-only readiness checks prevent its pending save-mask load erasing our work.
public partial class GrassIntegration:GameSystemBase {
 internal bool Available{get;private set;}internal string Status="";
 Type runtimeType,modType;object runtime;MethodInfo paint;FieldInfo maskSize,maskBytes,maskTexture,pending;
 Entity active;IEnumerator<Point> points;int painted;double nextProbe,waitingSince;
 GreenSpace green;double pixel;int resolution;Bounds terrainBounds;
 protected override void OnGamePreload(Purpose purpose,GameMode mode){base.OnGamePreload(purpose,mode);points?.Dispose();points=null;active=Entity.Null;runtime=null;Available=false;Status="";nextProbe=0;}
 void Probe(){
  bool old=Available;Available=false;runtime=null;
  if(runtimeType==null){
   runtimeType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("GrassSprites.GrassSpritesRuntime",false)).FirstOrDefault(t=>t!=null);
   modType=runtimeType?.Assembly.GetType("GrassSprites.Mod");
   paint=runtimeType?.GetMethod("PaintAtWorldPosition",new[]{typeof(Vector3),typeof(float),typeof(bool),typeof(bool)});
   maskSize=runtimeType?.GetField("m_UserMaskSize",BindingFlags.Instance|BindingFlags.NonPublic);
   maskBytes=runtimeType?.GetField("m_UserMaskBytes",BindingFlags.Instance|BindingFlags.NonPublic);
   maskTexture=runtimeType?.GetField("m_UserMaskSplatMap",BindingFlags.Instance|BindingFlags.NonPublic);
   pending=runtimeType?.GetField("m_PendingMaskAutoLoad",BindingFlags.Instance|BindingFlags.NonPublic);
  }
  var settings=modType?.GetProperty("Settings")?.GetValue(null);
  var instance=runtimeType?.GetProperty("Instance")?.GetValue(null);
  if(settings?.GetType().GetProperty("Enabled")?.GetValue(settings) is bool enabled&&enabled&&instance is ComponentSystemBase system&&system.World==World&&system.Enabled&&paint!=null&&maskSize!=null&&maskBytes!=null&&maskTexture!=null&&pending!=null){runtime=instance;Available=true;}
  if(old!=Available)World.GetExistingSystemManaged<PlanSystem>()?.Request("nativeGrassRefresh");
 }
 bool Ready=>Available&&(int)maskSize.GetValue(runtime)>1&&maskBytes.GetValue(runtime) is byte[]&&maskTexture.GetValue(runtime) is Texture2D texture&&texture!=null&&!(bool)pending.GetValue(runtime);
 void End(int state,string status){
  if(EntityManager.Exists(active)&&EntityManager.HasComponent<PlanGrassWork>(active))EntityManager.SetComponentData(active,new PlanGrassWork{State=state});
  points?.Dispose();points=null;active=Entity.Null;Status=status;
  World.GetExistingSystemManaged<PlanSystem>()?.Request("nativeGrassRefresh");Trace.Event("landscape.grass",status+" brushCalls="+painted);
 }
 protected override void OnUpdate(){
  var plans=World.GetExistingSystemManaged<PlanSystem>();if(Mod.Options==null||plans==null||!plans.Ready)return;
  double now=UnityEngine.Time.realtimeSinceStartupAsDouble;
  try{
   if(now>=nextProbe){nextProbe=now+1;Probe();}
   if(active!=Entity.Null&&(!EntityManager.Exists(active)||EntityManager.HasComponent<Deleted>(active))){End(2,"AP.GrassInterrupted");return;}
   if(active!=Entity.Null&&EntityManager.GetComponentData<PlanGrassWork>(active).State!=0){End(2,"AP.GrassInterrupted");return;}
   if(active==Entity.Null){
    using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlanGrassWork>(),ComponentType.ReadOnly<SavedPlan>(),ComponentType.Exclude<Deleted>()))
    using(var es=q.ToEntityArray(Allocator.Temp))foreach(var e in es)if(EntityManager.GetComponentData<PlanGrassWork>(e).State==0){active=e;waitingSince=now;painted=0;Status="AP.GrassWorking";plans.Request("nativeGrassRefresh");break;}
    if(active==Entity.Null)return;
   }
   if(!Available||Mod.Options.GrassMode==1){End(2,"AP.GrassInterrupted");return;}
   if(!Ready){if(now-waitingSince>45)End(2,"AP.GrassUnavailable");return;}
   bool buildingLive=false,temporary=false;
   if(EntityManager.HasBuffer<PlanOwnedEntity>(active))foreach(var item in EntityManager.GetBuffer<PlanOwnedEntity>(active,true)){
    var e=item.Entity;if(!EntityManager.Exists(e)||EntityManager.HasComponent<Deleted>(e))continue;
    if(EntityManager.HasComponent<Game.Tools.Temp>(e))temporary=true;
    else if(EntityManager.HasComponent<Game.Buildings.Building>(e))buildingLive=true;
   }
   if(temporary||!buildingLive){if(now-waitingSince>45)End(2,"AP.GrassUnavailable");return;}
   if(points==null){
    var stored=plans.Saved.Find(p=>p.Entity==active);var plan=stored?.Edit?.Draft;
    if(plan==null){End(2,"AP.GrassUnavailable");return;}
    var bounds=World.GetOrCreateSystemManaged<TerrainSystem>().GetTerrainBounds();int size=(int)maskSize.GetValue(runtime);
    terrainBounds=bounds;
    resolution=size;pixel=Math.Max(bounds.size.x,bounds.size.z)/(size-1);
    green=new GreenSpace(plan);points=green.GrassCandidates(pixel,bounds.min.x,bounds.min.z).GetEnumerator();
   }
   if((int)maskSize.GetValue(runtime)!=resolution){End(2,"AP.GrassInterrupted");return;}
   // Small batches bound GPU uploads; stop while the user operates the grass brush.
   var settings=modType.GetProperty("Settings").GetValue(null);
   if(settings.GetType().GetProperty("BrushToolActive")?.GetValue(settings) is bool brush&&brush)return;
   int calls=0;for(int i=0;i<64&&calls<8;i++){
    if(!points.MoveNext()){End(1,"AP.GrassDone");return;}
    var q=points.Current;if(!green.GrassFits(q,pixel))continue;
    paint.Invoke(runtime,new object[]{new Vector3((float)q.X,0,(float)q.Y),0f,true,false});
    int x=Mathf.RoundToInt(((float)q.X-terrainBounds.min.x)/terrainBounds.size.x*(resolution-1));
    int y=Mathf.RoundToInt(((float)q.Y-terrainBounds.min.z)/terrainBounds.size.z*(resolution-1));
    var bytes=(byte[])maskBytes.GetValue(runtime);
    if(x<0||y<0||x>=resolution||y>=resolution||bytes[y*resolution+x]!=255)throw new InvalidOperationException("Grass Sprites did not paint the requested mask pixel");
    painted++;calls++;
   }
  }catch(Exception error){Trace.Failure("landscape.grass.failed",error);End(2,"AP.GrassUnavailable");}
 }
}
}
