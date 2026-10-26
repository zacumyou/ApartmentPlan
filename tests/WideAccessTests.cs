using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ApartmentPlan;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
static class WideAccessTests {
 public static void Run(){
  var input=JObject.Parse(File.ReadAllText("ApartmentPlan/docs/access-latest-input.json"));
  var boundary=input["Boundary"].ToObject<List<Point>>();var entrances=input["Entrances"].ToObject<List<Point>>();var assets=input["Assets"].ToObject<List<Asset>>();var o=input["Options"].ToObject<LayoutOptions>();
  var zones=input["Zones"].ToObject<List<ManualZone>>();var ramps=input["Ramps"].ToObject<List<Parcel>>();var roads=input["Roads"]?.ToObject<List<ManualRoad>>();
  o.WideParkingAisle=7;
  var plan=Layout.Generate(boundary,entrances,assets,o,ramps,zones,roads);EnhancementTests.Verify(plan,assets,o);
  int wide=plan.Routes.Count(r=>r.Use=="aisleWide"&&r.Width==7);
  if(wide==0)throw new Exception("latest site still has no wide approach");
  if(plan.Routes.Concat(plan.ConstructionRoutes).Any(r=>r.Use=="aisleWide"&&r.Width!=7))throw new Exception("wide network width differs from native 7m");
  File.WriteAllText("ApartmentPlan/docs/wide-access-latest.json",JsonConvert.SerializeObject(new{Plan=plan,Assets=assets,Options=o,Entrances=entrances,Zones=zones}));
  Console.WriteLine("PASS latest site: buildings="+plan.Parcels.Count(r=>r.Kind==0)+" bays="+plan.Parcels.Sum(r=>r.ParkingSpaces)+" wide approaches="+wide+" total wide roads="+plan.Routes.Concat(plan.ConstructionRoutes).Count(r=>r.Use=="aisleWide"));
  o.WideParkingAisle=0;var narrow=Layout.Generate(boundary,entrances,assets,o,ramps,zones,roads);EnhancementTests.Verify(narrow,assets,o);
  if(narrow.Routes.Concat(narrow.ConstructionRoutes).Any(r=>r.Use=="aisleWide"))throw new Exception("unavailable wide road used");
  Console.WriteLine("PASS unavailable 2x fallback: buildings="+narrow.Parcels.Count(r=>r.Kind==0)+" bays="+narrow.Parcels.Sum(r=>r.ParkingSpaces));
 }
}
