using System;
using System.Collections.Generic;
using System.Linq;
using ApartmentPlan;
static class MasterPlanTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static void Geometry(Plan p){
  var polygon=p.Boundary.Select(p.Local).ToList();
  foreach(var r in p.Parcels)CheckSilent(Layout.Contains(r,polygon),"parcel escaped site");
  for(int i=0;i<p.Parcels.Count;i++)for(int j=i+1;j<p.Parcels.Count;j++)CheckSilent(!Layout.Overlap(p.Parcels[i],p.Parcels[j]),"overlapping parcels");
 }
 static void CheckSilent(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Preview(string name){
  var watch=System.Diagnostics.Stopwatch.StartNew();
  var site=new[]{new Point(0,0),new Point(160,0),new Point(160,275),new Point(0,325)};
  var entries=new[]{new Point(60,0),new Point(120,287.5)};
  var assets=new[]{new Asset{Id="Slab 40m",Width=40,Depth=12,Height=45,Homes=80},new Asset{Id="Slab 24m",Width=24,Depth=12,Height=30,Homes=40}};
  var zones=new[]{new ManualZone{Kind=5,Points=new List<Point>{new Point(105,65),new Point(148,65),new Point(148,135),new Point(105,135)}},new ManualZone{Kind=4,Points=new List<Point>{new Point(10,150),new Point(35,150),new Point(35,180),new Point(10,180)}}};
  var p=Layout.Generate(site,entries,assets,new LayoutOptions(),null,zones);
  Geometry(p);
  var directory=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"../../../../docs"));
  System.IO.File.WriteAllText(System.IO.Path.Combine(directory,name+".json"),System.Text.Json.JsonSerializer.Serialize(p,new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));
  Console.WriteLine("buildings="+p.Parcels.Count(r=>r.Kind==0)+" bays="+p.Parcels.Sum(r=>r.ParkingSpaces)+" requiredAccessFailures="+p.RequiredAccessFailures+" ms="+watch.ElapsedMilliseconds);
 }
 public static void Run(){
  var o=new LayoutOptions();var assets=new[]{new Asset{Id="Slab 40m",Width=40,Depth=12,Height=45,Homes=80},new Asset{Id="Slab 24m",Width=24,Depth=12,Height=30,Homes=40}};
  var site=new[]{new Point(0,0),new Point(160,0),new Point(160,275),new Point(0,325)};
  var entries=new[]{new Point(60,0),new Point(120,287.5)};
  var p=Layout.Generate(site,entries,assets,o,null,null);Geometry(p);
  var main=p.Routes.Where(r=>r.Use=="main").ToList();
  Check(main.Count==1&&main[0].CornerRadii.Count==2&&main[0].CornerRadii.All(r=>r>=Math.Max(o.Road/2+1,12*o.MainCurveLengthRatio)),"offset entrances connect with two tangent curves of adequate radius");
  for(int i=1;i<main[0].Points.Count-1;i++){var a=main[0].Points[i]-main[0].Points[i-1];var b=main[0].Points[i+1]-main[0].Points[i];if(a.Length<.001||b.Length<.001)continue;CheckSilent(SiteFrame.Dot(a,b)/a.Length/b.Length>Math.Cos(Math.PI/12),"sharp corner remains on main road");}
  Check(true,"main-road tangent changes are under 15 degrees per sampled segment");
  Check(p.Parcels.Count(r=>r.Kind==0)>5,"reference scale supports multiple actual-size slabs");
  var reverse=Layout.Frame(site,entries[1]);Check(Math.Abs(reverse.Right.X)<1e-6||Math.Abs(reverse.Right.Y)<1e-6,"diagonal first entrance does not rotate buildings diagonally");
  Geometry(Layout.Generate(site,new[]{entries[1],entries[0]},assets,o,null,null));
  var clockwise=site.Reverse().ToArray();var frame=Layout.Frame(clockwise,entries[0]);Check(Math.Abs(SiteFrame.Dot(frame.Right,p.Right))>.999,"common axis survives boundary winding");
  var zones=new[]{new ManualZone{Kind=5,Points=new List<Point>{new Point(105,65),new Point(148,65),new Point(148,135),new Point(105,135)}},new ManualZone{Kind=4,Points=new List<Point>{new Point(10,150),new Point(35,150),new Point(35,180),new Point(10,180)}}};
  var reserved=Layout.Generate(site,entries,assets,o,null,zones);Geometry(reserved);
  var buildings=reserved.Parcels.Where(r=>r.Kind==0).ToList();
  var courts=reserved.Courts;
  Check(courts.Count>=2&&courts.All(c=>c.Parking.Count>0),"each accepted group includes its parking court");
  Check(buildings.Any(b=>b.D>b.W)&&buildings.Any(b=>b.W>b.D),"horizontal and vertical groups survive exclusion packing");
  Check(p.Parcels.Count(b=>b.Kind==0)>=24,"no-exclusion reference preserves at least 24 buildings with mandatory parking");
  Check(p.Parcels.Sum(b=>b.ParkingSpaces)>224,"no-exclusion reference adds parking in previously empty courts");
  foreach(var building in p.Parcels.Where(b=>b.Kind==0))CheckSilent(ParkingFrontRules.Served(building,p.Parcels,40,4),"building without front parking");
  Check(true,"every default building has front parking");
  foreach(var building in buildings){
   CheckSilent(building.Frontage!=null,"missing explicit front road");var front=building.Frontage;
   double covered=reserved.Parcels.Where(r=>r.Kind==1).Sum(r=>Math.Max(0,Math.Min(r.X+r.W,front.X+front.W)-Math.Max(r.X,front.X))*Math.Max(0,Math.Min(r.Y+r.D,front.Y+front.D)-Math.Max(r.Y,front.Y)));
   CheckSilent(Math.Abs(covered-front.Area)<.01,"road does not pass full building front");
  }
  Check(true,"every building has a continuous road along its planning front");
  for(int i=0;i<buildings.Count;i++)for(int j=i+1;j<buildings.Count;j++)CheckSilent(FrontageRules.Spaced(buildings[i],buildings[j],24,8),"building spacing violated");
  Check(true,"parallel long-face spacing at least 24m and end spacing at least 8m");
  foreach(var bay in reserved.Parcels.Where(r=>r.Kind==2))CheckSilent(!reserved.MainRoadArea.Any(r=>Layout.Overlap(r,bay,6-.001)),"parking directly serves main through road");
  Check(reserved.Parcels.Any(r=>r.Kind==2),"parking remains available only along secondary access streets");
  Check(true,"no parking within 6m of through-road edge including bends and entrances");

  foreach(var zone in zones){var local=zone.Points.Select(reserved.Local).ToList();foreach(var parcel in reserved.Parcels.Where(r=>zone.Kind==5||r.Kind!=zone.Kind))CheckSilent(!Layout.Inside(new Point(parcel.X+parcel.W/2,parcel.Y+parcel.D/2),local),"manual zone overwritten");}
  Check(true,"external exclusion and manual park survive building parking road and pedestrian packing");
  Check(reserved.RequiredAccessFailures==0,"all required building and facility pedestrian access points connected");
  Check(reserved.Parcels.Any(r=>r.Kind==9),"pedestrian paths generated toward perimeter");
  var multi=Layout.Generate(site,new[]{entries[0],entries[1],new Point(0,130)},assets,o,null,zones);Geometry(multi);Check(multi.Routes.Count(r=>r.Use=="main")==2,"three entrances belong to a connected master network");
  var neck=new[]{new Point(0,0),new Point(100,0),new Point(100,80),new Point(52,80),new Point(52,120),new Point(100,120),new Point(100,200),new Point(0,200),new Point(0,120),new Point(48,120),new Point(48,80),new Point(0,80)};
  try{Layout.Generate(neck,new[]{new Point(50,0),new Point(50,200)},assets,o,null,null);throw new Exception("narrow neck accepted");}catch(ArgumentException e){Check(e.Message=="DisconnectedEntrance","full-width road rejects a 4m neck");}
  var angle=.35;Func<Point,Point> rotate=v=>new Point(v.X*Math.Cos(angle)-v.Y*Math.Sin(angle),v.X*Math.Sin(angle)+v.Y*Math.Cos(angle));
  var rotated=Layout.Generate(site.Select(rotate).ToArray(),entries.Select(rotate).ToArray(),assets,o,null,null);Geometry(rotated);Check(Math.Abs(SiteFrame.Dot(rotated.Right,rotate(p.Right)))>.999,"site-wide rotation retained without aligning to sloped edge");
  var directory=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"../../../../docs"));
  System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"master-plan-reference.json"),System.Text.Json.JsonSerializer.Serialize(reserved,new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));
 }
}
