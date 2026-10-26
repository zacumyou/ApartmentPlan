using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ApartmentPlan;
static class ParkingDecalPreview {
 public static void SingleEntry(string suffix){
  var boundary=new[]{new Point(0,0),new Point(260,0),new Point(130,190),new Point(0,140)};
  var assets=new[]{new Asset{Id="Doan Xi",Width=40,Depth=32,Height=111,Homes=144,PedestrianAccess=new List<Point>{new Point(-3.48265839,-14.8937654),new Point(14.417119,-14.7238178),new Point(-2.70267868,14.6536751),new Point(14.5901165,14.7767353)}}};
  foreach(var entrance in new[]{new Point(240,0),new Point(130,0),new Point(15,0)}){
   var options=new LayoutOptions{ContinuousFrontage=true,RequireBuildingRoad=true,MainRoadPedestrian=true,AisleRoadPedestrian=true,Road=8,ParkingAisle=suffix.Contains("network")?6:4,AccessRoad=suffix.Contains("network")?0:4,ParkingPavingMargin=0,BayWidth=2.85,BayDepth=5.97292232,Play=0,Facility=0};
   var p=Layout.Generate(boundary,new[]{entrance},assets,options,null,null);
   if(suffix=="recovered-network"){
    if(!p.Parcels.Any(r=>r.Kind==0)||p.ParkingAccessFailures!=0||p.RequiredAccessFailures!=0||FrontageNetwork.Missing(p,assets,6,0)!=0)throw new Exception("Single entrance recovered-width access failed");
    var poly=p.Boundary.Select(p.Local).ToList();if(p.Parcels.Any(r=>!Layout.Contains(r,poly)))throw new Exception("Outside polygon");
    for(int i=0;i<p.Parcels.Count;i++)for(int j=0;j<i;j++)if(Layout.Overlap(p.Parcels[i],p.Parcels[j]))throw new Exception("Parcel overlap");
   }
   Console.WriteLine(entrance.X+","+entrance.Y+" buildings="+p.Parcels.Count(r=>r.Kind==0)+" parking="+p.Parcels.Sum(r=>r.ParkingSpaces)+" access="+p.ParkingAccessFailures+"/"+p.RequiredAccessFailures+" timings="+JsonSerializer.Serialize(p.LayoutTimings));
   File.WriteAllText("ApartmentPlan/docs/single-entry-"+entrance.X+"-"+suffix+".json",JsonSerializer.Serialize(new{Plan=p,Assets=assets,Options=options,Entrances=new[]{entrance},DecalWidth=3.0,DecalDepth=5.97292232},new JsonSerializerOptions{IncludeFields=true}));
  }
 }
 public static void Run(double aisle=7){
  var boundary=new[]{new Point(0,0),new Point(160,0),new Point(160,275),new Point(0,325)};
  var entrances=new[]{new Point(60,0),new Point(120,287.5)};
  var slab=new[]{new Asset{Id="Slab 40m",Width=40,Depth=12,Height=45,Homes=80},new Asset{Id="Slab 24m",Width=24,Depth=12,Height=30,Homes=40}};
  var doan=new[]{new Asset{Id="Doan Xi",Width=40,Depth=32,Height=111,Homes=144,PedestrianAccess=new List<Point>{new Point(-3.48265839,-14.8937654),new Point(14.417119,-14.7238178),new Point(-2.70267868,14.6536751),new Point(14.5901165,14.7767353)}}};
  foreach(var assets in new[]{slab,doan}){
   var o=new LayoutOptions{ContinuousFrontage=true,RequireBuildingRoad=true,MainRoadPedestrian=true,AisleRoadPedestrian=true,Road=16,ParkingAisle=aisle,AccessRoad=aisle,MainCurveRadius=40,BayWidth=2.85,BayDepth=5.97292232,Play=0,Facility=0};
   var p=Layout.Generate(boundary,entrances,assets,o,null,null);
   int missing=FrontageNetwork.Missing(p,assets,aisle,1);
   var graph=ConstructionGraph.Create(p).Where(e=>e.Use!="pedestrian").ToList();
   var visited=new HashSet<ConstructionEdge>{graph.OrderBy(e=>Math.Min((e.A-p.Origin).Length,(e.B-p.Origin).Length)).First()};
   bool changed=true;while(changed){changed=false;foreach(var e in graph)if(!visited.Contains(e)&&visited.Any(q=>new[]{q.A,q.B}.Any(a=>new[]{e.A,e.B}.Any(b=>(a-b).Length<.03)))){visited.Add(e);changed=true;}}
   if(visited.Count!=graph.Count)throw new Exception("Infill native road graph disconnected");
   if(missing!=0||p.ParkingAccessFailures!=0||p.RequiredAccessFailures!=0)throw new Exception("Preview access failed: "+missing+"/"+p.ParkingAccessFailures+"/"+p.RequiredAccessFailures);
   var poly=p.Boundary.Select(p.Local).ToList();
   foreach(var r in p.Parcels){if(!Layout.Contains(r,poly))throw new Exception("Outside boundary");if(r.Kind==2&&r.ParkingSpaces>0&&((r.ParkingAlongX?r.W:r.D)/r.ParkingSpaces<2.85-.01||(r.ParkingAlongX?r.D:r.W)<5.97292232-.01))throw new Exception("Decal spacing");}
   for(int i=0;i<p.Parcels.Count;i++)for(int j=0;j<i;j++)if(Layout.Overlap(p.Parcels[i],p.Parcels[j]))throw new Exception("Parcel overlap");
   if(!p.Parcels.Any(r=>r.Kind==8))throw new Exception("No enclosed parking pavement");
   if(aisle==4&&assets==doan){
    if(p.Parcels.Count(r=>r.Kind==0)!=9)throw new Exception("Parking infill displaced a Doan building");
    if(p.Parcels.Any(r=>r.Kind==4&&Layout.Overlap(r,new Parcel{X=-24,Y=42,W=6,D=48})))throw new Exception("Open court parking apron left green");
    if(!p.Parcels.Any(r=>r.Kind==2&&r.X>=-12&&r.X+r.W<=4&&r.Y>=158&&r.Y<198))throw new Exception("Central court infill missing");
    if(!p.Parcels.Any(r=>r.Kind==2&&r.X>=28&&r.Y>=242))throw new Exception("Opposite parking extension missing");
    var upper=p.Parcels.Where(r=>r.Kind==2&&r.X<0&&r.Y>=42&&r.Y<90).ToArray();
    if(upper.Length!=6||upper.Any(r=>r.ParkingSpaces!=11))throw new Exception("Transverse street clearance wasted a bay at the row ends");
    var middle=p.Parcels.Where(r=>r.Kind==2&&r.X>=-18&&r.X<=4&&r.Y>=152&&r.Y<196).ToArray();
    if(middle.Length!=4||middle.Any(r=>Math.Abs(r.Y-middle[0].Y)>.001||Math.Abs(r.D-middle[0].D)>.001||r.ParkingSpaces!=13))throw new Exception("Central infill did not match neighbouring rows");
    foreach(var local in new[]{new Point(-4,155),new Point(-4,199)}){
     var junction=p.World(local);if(graph.Count(e=>(e.A-junction).Length<.03||(e.B-junction).Length<.03)<3)throw new Exception("Central aisle must join BOTH transverse streets");
    }
    var bottom=p.Parcels.Where(r=>r.Kind==2&&r.X>=16&&r.Y>=242&&r.Y<280).ToArray();
    if(bottom.Length!=3||bottom.Any(r=>Math.Abs(r.Y-bottom[0].Y)>.001||Math.Abs(r.D-bottom[0].D)>.001||r.ParkingSpaces!=12))throw new Exception("Bottom infill did not preserve neighbouring row length");
   }
   Console.WriteLine("pavementArea="+p.Parcels.Where(r=>r.Kind==8).Sum(r=>r.Area)+" minSurfaceSide="+p.Parcels.Where(r=>r.Kind==8||r.Kind==2||r.Kind==1&&r.InvisibleRoad).Min(r=>Math.Min(r.W,r.D)));
   var name=assets==slab?"slab":"doan";
   var data=new{Plan=p,Assets=assets,Options=o,Entrances=entrances,ParkingDecal="CSKRParkingDecalClosed1x",DecalWidth=3.0,DecalDepth=5.97292232,DecalGap=-.15,MissingFronts=missing};
   File.WriteAllText("ApartmentPlan/docs/cs-kr-"+name+(aisle==8?"-alley":aisle==7?"-paved":"-paved-narrow")+".json",JsonSerializer.Serialize(data,new JsonSerializerOptions{IncludeFields=true}));
   Console.WriteLine(name+" buildings="+p.Parcels.Count(r=>r.Kind==0)+" parking="+p.Parcels.Sum(r=>r.ParkingSpaces)+" area="+p.Area+" access=PASS overlap=PASS decal-size=PASS");
  }
 }
}
