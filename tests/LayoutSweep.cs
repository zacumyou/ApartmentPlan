using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using ApartmentPlan;
using Newtonsoft.Json;

static class LayoutSweep {
 static IEnumerable<(Point A,Point B)> Segments(ConstructionCourse c){
  int n=c.Curved?32:1;
  for(int i=0;i<n;i++)yield return(c.Curve.At((double)i/n),c.Curve.At((i+1d)/n));
 }
 // Flag sustained overlap of distinct straight road ribbons. A normal crossing
 // shares a local junction footprint and is intentionally excluded.
 static bool ParallelOverlap(ConstructionCourse a,ConstructionCourse b){
  if(a.Curved||b.Curved)return false;
  var delta=a.Curve.D-a.Curve.A;double length=delta.Length;
  var axis=delta*(1/length);var x=b.Curve.A-a.Curve.A;var y=b.Curve.D-a.Curve.A;
  var offset=Layout.Cross(axis,x);double width=(a.Width+b.Width)/2;
  if(Math.Abs(offset)>=width-.1||Math.Abs(Layout.Cross(axis,y)-offset)>=.05)return false;
  double start=SiteFrame.Dot(x,axis),end=SiteFrame.Dot(y,axis);
  return Math.Min(length,Math.Max(start,end))-Math.Max(0,Math.Min(start,end))>Math.Max(4,width*.6);
 }
 static string Conflict(ConstructionCourse a,ConstructionCourse b){
  var ends=new[]{a.Curve.A,a.Curve.D};
  var shared=ends.Where(p=>(p-b.Curve.A).Length<.05||(p-b.Curve.D).Length<.05).ToList();
  foreach(var x in Segments(a))foreach(var y in Segments(b)){
   Point u=x.B-x.A,v=y.B-y.A,z=y.A-x.A;double cross=Layout.Cross(u,v);
   if(u.Length<.0001||v.Length<.0001)continue;
   if(Math.Abs(cross)<1e-7){
    if(Math.Abs(Layout.Cross(z,u))/u.Length>.02)continue;
    double l=SiteFrame.Dot(z,u)/u.Length,r=SiteFrame.Dot(y.B-x.A,u)/u.Length;
    if(Math.Min(u.Length,Math.Max(l,r))-Math.Max(0,Math.Min(l,r))>.15)return "overlap";
   }else{
    double t=Layout.Cross(z,v)/cross,s=Layout.Cross(z,u)/cross;
    if(t>=-1e-7&&t<=1.0000001&&s>=-1e-7&&s<=1.0000001&&!shared.Any(p=>(p-(x.A+u*t)).Length<.08))return "crossing";
   }
  }
  return null;
 }
 public static void Run(string folder){
  Directory.CreateDirectory(folder);int bad=0,generated=0,rejected=0;
  foreach(string mode in new[]{"villa","slab","tower"})foreach(int shape in new[]{0,1,2})foreach(int config in Enumerable.Range(0,8)){
   var id=mode+"-"+shape+"-"+config;if(Environment.GetEnvironmentVariable("AP_SWEEP_CASE") is string only && id!=only)continue;
   var boundary=new List<Point>{new Point(0,0),new Point(240,0),new Point(240,180)};
   if(shape==1)boundary.AddRange(new[]{new Point(150,180),new Point(150,125),new Point(95,125),new Point(95,180)});
   if(shape==2)boundary.Add(new Point(60,215));
   boundary.Add(new Point(0,180));
   var gates=new List<Point>{new Point(24+config*26,0)};
   if(config%2==1)gates.Add(new Point(240,30+config*15));
   var o=new LayoutOptions{Villa=mode=="villa",Tower=mode=="tower",TowerParking=new Asset{Id="parking",Width=16,Depth=24},ParkingAisle=8,Road=config==0?16:24,Gap=config==0?8:14,VillaMainSpacing=config==0?80:140,ContinuousFrontage=true,RequireBuildingRoad=true};
   o.VillaConnections=gates.Select((g,i)=>new VillaConnection{Boundary=g,Road=g+(i==0?new Point(0,-8):new Point(8,0)),Tangent=i==0?new Point(1,0):new Point(0,1),Manual=true}).ToList();
   var assets=new[]{new Asset{Id=mode=="villa"?"CSKR test":"apartment",Width=mode=="villa"?16:32,Depth=mode=="villa"?16:24,Homes=40,Height=30}};
   
   Plan plan;try{plan=Layout.Generate(boundary,gates,assets,o,null,null);}catch(ArgumentException e) when(mode=="tower"&&(e.Message=="AP.Tower.RoutingRetryFit"||e.Message=="AP.Tower.ConnectionFit")){Console.WriteLine(id+" rejected "+e.Message);rejected++;if(config!=0)bad++;continue;}
   generated++;var roads=ConstructionCurves.Create(plan).Where(c=>c.Use!="pedestrian").ToArray();
   var conflicts=new List<string>();
   for(int i=0;i<roads.Length;i++)for(int j=0;j<i;j++){var issue=Conflict(roads[i],roads[j]);if(issue==null&&ParallelOverlap(roads[i],roads[j]))issue="parallel";if(issue!=null)conflicts.Add(issue+" "+i+" "+j);}
   var houses=plan.Parcels.Where(p=>p.Kind==0).ToArray();
   int missing=FrontageNetwork.Missing(plan,assets,o.ParkingAisle,0);
   Console.WriteLine(id+" frontage="+missing+" houses="+houses.Length+" roads="+roads.Length+" access="+plan.RequiredAccessFailures+" conflicts="+string.Join(",",conflicts));
   File.WriteAllText(Path.Combine(folder,id+".json"),JsonConvert.SerializeObject(new{boundary,gates,roads,conflicts,houses=houses.Select(p=>(p.PlacementOutline??p.Corners).Select(plan.World))}));
   if(missing>0||conflicts.Count>0||plan.RequiredAccessFailures>0||houses.Length==0)bad++;
  }
  Console.WriteLine($"Generated {generated}, rejected {rejected}, failed checks {bad}");
  if(bad>0)throw new Exception(bad+" failing layouts");
 }
}
