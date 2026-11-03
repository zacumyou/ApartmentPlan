using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;
namespace ApartmentPlan {
public partial class PlanSystem {
 float3? bridgePoint;
 public string ApartmentTest(string json){
  if(!Ready)throw new InvalidOperationException("City not ready");
  var a=JObject.Parse(json);var action=(string)a["action"]??"state";
  if(action=="command"){
   var command=(string)a["command"];
   if(!new[]{"menu","new","close","closePlanAnswer","finish","entrance","road","roadCancel","select","clearAssets","surfaceParking","parkingInOpenSpace","autoCalculate","rebuild","save","inspectPlan","editSaved","nativePreview","nativeApply","nativeCancel"}.Contains(command))throw new ArgumentException("Unsupported test command");
   Request(command,(string)a["value"]??"");
  }else if(action=="point"){
   if(bridgePoint.HasValue)throw new InvalidOperationException("Point pending");
   float x=(float)a["x"],z=(float)a["z"];
   if(!math.isfinite(x)||!math.isfinite(z)||math.abs(x)>10000||math.abs(z)>10000)throw new ArgumentException("Invalid point");
   var data=World.GetOrCreateSystemManaged<Game.Simulation.TerrainSystem>().GetHeightData();
   var p=new float3(x,0,z);p.y=Game.Simulation.TerrainUtils.SampleHeight(ref data,p);bridgePoint=p;
  }else if(action=="catalog")return CatalogJson();
  else if(action!="state")throw new ArgumentException("Unsupported action");
  return JsonConvert.SerializeObject(new{adapterVersion=1,pendingPoint=bridgePoint.HasValue,pendingCommands=commands.Count,state=JObject.Parse(State()),parcels=Draft?.Parcels.GroupBy(p=>p.Kind).Select(g=>new{kind=g.Key,count=g.Count(),area=g.Sum(p=>p.W*p.D)}),saved=Saved.Select(p=>new{id=p.Info.Number,shapes=p.Shapes.Count})});
 }
 void BridgePointTick(){if(!bridgePoint.HasValue)return;var point=bridgePoint.Value;bridgePoint=null;Click(point);}
}
}
