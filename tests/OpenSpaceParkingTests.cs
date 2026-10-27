using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using ApartmentPlan;
static class OpenSpaceParkingTests {
 public static void Run(){
  var boundary=new[]{new Point(0,0),new Point(260,0),new Point(260,240),new Point(0,240)};
  var entries=new[]{new Point(130,0),new Point(130,240)};
  var assets=new[]{new Asset{Id="test-slab",Width=48,Depth=16,Height=48,Homes=100}};
  var zones=new[]{new ManualZone{Kind=5,Points=new List<Point>{new Point(10,10),new Point(35,10),new Point(35,35),new Point(10,35)}}};
  var options=new LayoutOptions{ParkingPerHome=2,Parking=.3,MaxBuildings=6,PedestrianNetwork=false,EdgeInfill=false};
  var before=Layout.Generate(boundary,entries,assets,options,null,zones);
  options.ParkingInOpenSpace=true;
  var after=Layout.Generate(boundary,entries,assets,options,null,zones);
  var graph=ConstructionGraph.Create(after);
  if(!graph.Any(e=>e.Use=="parkingTwoWay"&&Math.Abs(e.Width-options.SupplementalTwoWayWidth)<.01)||!graph.Any(e=>e.Use=="aisleWide"&&Math.Abs(e.Width-options.SupplementalAccessWidth)<.01))throw new Exception("Supplemental native road roles missing");
  int Count(Plan p)=>p.Parcels.Sum(x=>x.ParkingSpaces);
  string Buildings(Plan p)=>string.Join(";",p.Parcels.Where(x=>x.Kind==0).Select(x=>$"{x.X},{x.Y},{x.W},{x.D},{x.AssetId}"));
  if(Count(after)<=Count(before))throw new Exception("No supplemental parking generated");
  if(Buildings(before)!=Buildings(after))throw new Exception("Supplemental parking moved housing");
  if(after.ParkingAccessFailures>0)throw new Exception("Supplemental parking is disconnected");
  var local=boundary.Select(after.Local).ToList();
  if(after.Parcels.Any(p=>!Layout.Contains(p,local)))throw new Exception("Parcel outside boundary");
  var reserved=zones[0].Points.Select(after.Local).ToList();
  if(after.Parcels.Where(p=>p.Kind==2).Any(p=>p.Corners.Any(c=>Layout.Inside(c,reserved))))throw new Exception("Parking entered a manually excluded zone");
  options.ParkingInOpenSpace=false;
  var restored=Layout.Generate(boundary,entries,assets,options,null,zones);
  if(Count(restored)!=Count(before)||Buildings(restored)!=Buildings(before))throw new Exception("Toggle off did not restore baseline");
  Directory.CreateDirectory("docs");
  File.WriteAllText("docs/open-space-parking.json",JsonConvert.SerializeObject(new{width=260,height=240,before,after},Formatting.Indented));
  Console.WriteLine($"PASS open-space parking: {Count(before)} -> {Count(after)}, buildings preserved, connected, exclusion preserved, toggle reversible");
 }
}

