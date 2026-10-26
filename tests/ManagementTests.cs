using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using ApartmentPlan;
static class ManagementTests {
 public static void Run(){
  var boundary=new List<Point>{new Point(0,0),new Point(160,0),new Point(160,275),new Point(0,325)};
  var entries=new[]{new Point(60,0),new Point(120,287.5)};
  var zones=new List<ManualZone>{new ManualZone{Kind=5,Points=new List<Point>{new Point(1,110),new Point(26,110),new Point(26,145),new Point(1,145)}},new ManualZone{Kind=5,Points=new List<Point>{new Point(130,165),new Point(156,165),new Point(156,200),new Point(130,200)}}};
  var mask=SiteTerrainMask.Raster(boundary,zones,-5,-5,340,340);
  if(mask[125*340+15]!=0||mask[185*340+145]!=0||mask[30*340+30]!=255||mask[330*340+330]!=0)throw new Exception("Flattening mask must subtract both holes and exterior");
  var assets=new[]{new Asset{Id="Slab",Width=40,Depth=12,Height=45,Homes=80}};
  var options=new LayoutOptions{ContinuousFrontage=true,RequireBuildingRoad=true,MainRoadPedestrian=true,AisleRoadPedestrian=true,Road=16,AccessRoad=4,ParkingAisle=4,BayWidth=2.85,BayDepth=5.97292232,Play=0,Facility=0};
  var p=Layout.Generate(boundary,entries,assets,options,null,zones);
  foreach(var z in zones){var local=z.Points.Select(p.Local).ToList();foreach(var r in p.Parcels){if(Layout.Inside(new Point(r.X+r.W/2,r.Y+r.D/2),local)||r.Corners.Any(q=>Layout.Inside(q,local)))throw new Exception("Excluded zone painted");}}
  if(!p.Parcels.Any(r=>r.Kind==0)||p.RequiredAccessFailures!=0||p.ParkingAccessFailures!=0)throw new Exception("Masked layout access failed");
  File.WriteAllText("ApartmentPlan/docs/management-exclusions.json",JsonSerializer.Serialize(new{Plan=p,Assets=assets,Options=options,Entrances=entries,Zones=zones,DecalWidth=3.0,DecalDepth=5.97292232},new JsonSerializerOptions{IncludeFields=true}));
  Console.WriteLine("PASS two subtract polygons including edge margin; terrain mask holes/exterior; connected masked layout; buildings="+p.Parcels.Count(r=>r.Kind==0));
 }
}
