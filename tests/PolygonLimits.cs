using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ApartmentPlan;

static partial class LayoutSweep {
 static Point[] Outline(params int[] coords)=>Enumerable.Range(0,coords.Length/2).Select(i=>new Point(coords[2*i],coords[2*i+1])).ToArray();
 static Point[] Neck(int width){
  int low=90-width/2,high=low+width;
  return Outline(0,0,96,0,96,low,160,low,160,0,256,0,256,180,160,180,160,high,96,high,96,180,0,180);
 }
 static Point[] Comb(int teeth){
  var pts=Outline(0,0,256,0,256,180).ToList();
  double pitch=248.0/teeth;
  for(int i=0;i<teeth;i++){
   double x=252-i*pitch,d=100+(i%3)*12;
   pts.AddRange(new[]{new Point(x,180),new Point(x,d),new Point(x-pitch/2,d),new Point(x-pitch/2,180)});
  }
  pts.Add(new Point(0,180));return pts.ToArray();
 }
 public static void PolygonLimits(string folder){
  Directory.CreateDirectory(folder);
  var sites=new Dictionary<string,Point[]>{
   {"u",Outline(0,0,256,0,256,180,178,180,178,46,78,46,78,180,0,180)},
   {"neck12",Neck(12)},{"neck32",Neck(32)},{"neck64",Neck(64)},
   {"spur",Outline(0,0,256,0,256,108,212,108,232,172,172,130,148,194,108,132,72,158,64,112,0,136)},
   {"comb",Comb(5)},{"limit128",Comb(31)}
  };
  var over=sites["limit128"].ToList();over.Insert(1,new Point(32,0));
  if(over.Count!=129||Layout.Valid(over))throw new Exception("129-point boundary accepted");
  if(Environment.GetEnvironmentVariable("AP_POLYGON_STRESS")=="1"){
   sites=new Dictionary<string,Point[]>{
    {"neck4",Neck(4)},{"deepNotch",Outline(0,0,256,0,256,180,160,180,130,12,100,180,0,180)},
    {"comb128large",Comb(31).Select(q=>q*2).ToArray()},
    {"longSpur",Outline(0,0,256,0,256,80,760,92,256,104,256,180,0,180)}
   };
  }
  bool villaGallery=Environment.GetEnvironmentVariable("AP_VILLA_GALLERY")=="1";
  if(villaGallery)sites=new Dictionary<string,Point[]>{
   {"square",Outline(0,0,64,0,64,64,0,64)},{"rectangle",Outline(0,0,146,0,146,94,0,94)},
   {"triangle",Outline(0,0,194,0,72,148)},{"trapezoid",Outline(0,0,220,0,172,124,28,124)},
   {"diamond",Outline(0,90,130,0,260,90,130,180)},{"pentagon",Outline(0,0,180,0,212,96,118,182,0,108)},
   {"hexagon",Outline(0,50,60,0,200,0,262,60,207,146,46,146)},
   {"octagon",Outline(0,60,60,0,220,0,280,60,280,180,220,240,60,240,0,180)},
   {"ribbon",Outline(0,0,422,0,422,55,0,55)},{"skew",Outline(0,0,274,0,358,125,84,125)},
   {"ell",Outline(0,0,280,0,280,82,104,82,104,224,0,224)},
   {"tee",Outline(0,0,302,0,302,82,202,82,202,218,96,218,96,82,0,82)},
   {"u",Outline(0,0,256,0,256,180,178,180,178,46,78,46,78,180,0,180)},
   {"stairs",Outline(0,0,330,0,330,72,248,72,248,132,162,132,162,202,0,202)},
   {"neck",Neck(32)},{"notch",Outline(0,0,256,0,256,180,160,180,130,12,100,180,0,180)},
   {"spur",Outline(0,0,256,0,256,80,760,92,256,104,256,180,0,180)},
   {"comb",Comb(5)},{"comb128",Comb(31).Select(q=>q*2).ToArray()},
   {"large",Outline(0,0,640,0,710,135,620,310,410,380,80,331,-50,160)}
  };
  var rows=new List<object>();
  foreach(var site in sites){
   var boundary=site.Value;if(!Layout.Valid(boundary)||!Layout.ValidOpen(boundary))throw new Exception("Invalid fixture "+site.Key);
   foreach(var mode in new[]{"slab","tower","villa"})foreach(int count in new[]{1,2}){
    if(villaGallery&&(mode!="villa"||count!=(Array.IndexOf(sites.Keys.ToArray(),site.Key)%2+1)))continue;
    var filter=Environment.GetEnvironmentVariable("AP_POLYGON_CASES");
    if(filter!=null&&!filter.Split(',').Contains(site.Key+"-"+mode+"-"+count))continue;
    var gates=count==1?Outline(46,0):Outline(46,0,256,68);
    if(site.Key=="comb128large"&&count==2)gates[1]=new Point(512,68);
    var o=new LayoutOptions{Villa=mode=="villa",Tower=mode=="tower",TowerParking=new Asset{Id="parking",Width=16,Depth=24},ParkingAisle=8,Road=24,Gap=14,VillaMainSpacing=140,ContinuousFrontage=true,RequireBuildingRoad=true};
    o.VillaConnections=gates.Select((g,i)=>new VillaConnection{Boundary=g,Road=g+(i==0?new Point(0,-8):new Point(8,0)),Tangent=i==0?new Point(1,0):new Point(0,1),Manual=true}).ToList();
    if(villaGallery){
     var edgeIndices=count==1?new[]{0}:new[]{0,1};
     o.VillaConnections=edgeIndices.Select(i=>{
      var a=boundary[i];var b=boundary[i+1];var t=VillaGeometry.Unit(b-a);var mid=(a+b)*.5;
      return new VillaConnection{Boundary=mid,Road=mid+new Point(t.Y,-t.X)*8,Tangent=t,Manual=true};
     }).ToList();
     gates=o.VillaConnections.Select(g=>g.Boundary).ToArray();
    }
    var assets=new[]{new Asset{Id=o.Villa?"CSKR test":"apartment",Width=o.Villa?16:32,Depth=o.Villa?16:24,Homes=40,Height=30}};
    if(Environment.GetEnvironmentVariable("AP_POLYGON_MIXED")=="1"){
     var sizes=o.Villa?new[]{(16,16),(12,8),(24,16),(8,8),(32,24)}:new[]{(32,16),(24,12),(56,24),(16,16),(72,32)};
     assets=sizes.Select(s=>new Asset{Id=s.Item1+"x"+s.Item2,Width=s.Item1,Depth=s.Item2,Homes=10,Height=o.Villa?8:40}).ToArray();
    }
    long allocated=GC.GetAllocatedBytesForCurrentThread();var clock=Stopwatch.StartNew();Plan p;
    try{p=Layout.Generate(boundary,gates,assets,o,null,null);}
    catch(ArgumentException e){if(mode=="villa"&&site.Key=="comb128large")throw;rows.Add(new{shape=site.Key,mode,count,boundary,gates,ms=clock.ElapsedMilliseconds,allocated=GC.GetAllocatedBytesForCurrentThread()-allocated,planning=e.Data["planning"],rejection=e.Message});continue;}
    clock.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
    var houses=p.Parcels.Where(q=>q.Kind==0).Select(q=>(q.PlacementOutline??q.Corners).Select(p.World).ToArray()).ToArray();
    var roads=ConstructionCurves.Create(p).Where(c=>c.Use!="pedestrian").ToArray();
    var conflicts=new List<string>();
    for(int i=0;i<roads.Length;i++)for(int j=0;j<i;j++){var issue=Conflict(roads[i],roads[j]);if(issue==null&&ParallelOverlap(roads[i],roads[j]))issue="parallel";if(issue!=null)conflicts.Add(issue+" "+i+" "+j);}
    if(mode=="villa"&&site.Key=="comb128large"&&(conflicts.Count!=0||houses.Length==0||p.RequiredAccessFailures!=0))
     throw new Exception("Folded boundary must produce connected housing without road conflicts: entrances="+count);
    rows.Add(new{shape=site.Key,mode,count,boundary,gates,ms=clock.ElapsedMilliseconds,allocated,planning=p.Planning,houses,roads,conflicts,outside=houses.Count(h=>!VillaGeometry.Inside(h,boundary)),missing=FrontageNetwork.Missing(p,assets,o.ParkingAisle,0),access=p.RequiredAccessFailures,parking=p.ParkingAccessFailures});
   }
   Console.WriteLine(site.Key+" "+boundary.Length+" vertices finished");
   File.WriteAllText(Path.Combine(folder,"results.json"),JsonConvert.SerializeObject(rows));
  }
 }
}
