using System;
using System.Linq;
using System.Collections.Generic;
using ApartmentPlan;
static class ParkingOptionsTests {
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 public static void Run(){
  var site=new[]{new Point(0,0),new Point(160,0),new Point(160,275),new Point(0,325)};
  var entries=new[]{new Point(60,0),new Point(120,287.5)};
  var assets=new[]{new Asset{Id="L",Width=40,Depth=12,Height=45,Homes=80},new Asset{Id="S",Width=24,Depth=12,Height=30,Homes=40}};
  var relaxed=Layout.Generate(site,entries,assets,new LayoutOptions{RequireFrontParking=false,FrontParkingDistance=8},null,null);
  Check(relaxed.Parcels.Any(b=>b.Kind==0&&!ParkingFrontRules.Served(b,relaxed.Parcels,8,4)),"disabled front-parking condition permits unserved infill");
  var o=new LayoutOptions{BoundaryGreenMargin=8,ParallelGap=32,BuildingStagger=4,ParkingPavingMargin=2,FrontParkingDistance=20,AllowRingCourts=false,PedestrianNetwork=false};
  var p=Layout.Generate(site,entries,assets,o,null,null);
  Check(p.Parcels.Any(b=>b.Kind==0)&&p.Courts.All(c=>c.Pattern!="ring")&&!p.Routes.Any(r=>r.Use=="pedestrian"),"pattern and pedestrian switches applied");
  foreach(var b in p.Parcels.Where(b=>b.Kind==0))Check(ParkingFrontRules.Served(b,p.Parcels,20,4),"changed front parking distance enforced");
  var bars=p.Parcels.Where(b=>b.Kind==0).ToList();for(int i=0;i<bars.Count;i++)for(int j=0;j<i;j++)if(!FrontageRules.Spaced(bars[i],bars[j],32,8))throw new Exception("configured spacing lost");
  Check(true,"configured parallel spacing enforced");
  var boundary=site.Select(p.Local).ToArray();foreach(var r in p.Parcels.Where(b=>b.Kind==0||b.Kind==2))foreach(var v in r.Corners)for(int i=0;i<boundary.Length;i++){
   var a=boundary[i];var d=boundary[(i+1)%boundary.Length]-a;double t=Math.Max(0,Math.Min(1,SiteFrame.Dot(v-a,d)/SiteFrame.Dot(d,d)));
   if((v-a-d*t).Length<7.999)throw new Exception("green margin lost");
  }
  Check(true,"8m green margin enforced for all building and parking corners");
  var zone=new ManualZone{Kind=5,Points=new List<Point>{new Point(105,65),new Point(148,65),new Point(148,135),new Point(105,135)}};
  var masked=Layout.Generate(site,entries,assets,new LayoutOptions(),null,new[]{zone,zone});
  var local=masked.Local(new Point(105,65));var mask=new Parcel{X=local.X,Y=local.Y,W=43,D=70};
  Check(!masked.Parcels.Any(r=>Layout.Overlap(r,mask)),"overlapping exclusions form a subtract mask with no green or facility fill");
  var json=System.Text.Json.JsonSerializer.Serialize(o,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});var loaded=System.Text.Json.JsonSerializer.Deserialize<LayoutOptions>(json,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
  Check(loaded.BoundaryGreenMargin==8&&loaded.ParkingPavingMargin==2&&!loaded.AllowRingCourts&&!loaded.PedestrianNetwork,"layout parameter serialization roundtrip (not game settings reload)");
 }
}
