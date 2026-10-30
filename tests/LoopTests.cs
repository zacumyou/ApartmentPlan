using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using ApartmentPlan;
static class LoopTests {
 sealed class Input {
  public List<Point> Boundary=new List<Point>(),Entrances=new List<Point>();public List<Asset> Assets=new List<Asset>();public LayoutOptions Options=new LayoutOptions();
  public List<Parcel> Ramps=new List<Parcel>();public List<ManualZone> Zones=new List<ManualZone>();public List<ManualRoad> Roads=new List<ManualRoad>();
 }
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Terrain(){
  var gate=new TerrainSettlement(0);
  Check(!gate.Observe(.2,10,100),"unsettled terrain applied");
  Check(!gate.Observe(.4,0,100)&&!gate.Observe(.6,0,100),"too few stable observations");
  Check(!gate.Observe(.8,2,100),"late GPU change ignored");
  Check(!gate.Observe(1,.1,100)&&!gate.Observe(1.2,.1,100)&&gate.Observe(1.4,.1,100),"settled terrain not accepted");
  bool rejected=false;try{new TerrainSettlement(0).Observe(31,8,100);}catch(InvalidOperationException){rejected=true;}Check(rejected,"timeout must stop placement");
  rejected=false;try{new TerrainSettlement(0).Observe(1,0,0);}catch(InvalidOperationException){rejected=true;}Check(rejected,"empty terrain samples accepted");
  rejected=false;try{new TerrainSettlement(0).Observe(1,double.NaN,100);}catch(InvalidOperationException){rejected=true;}Check(rejected,"invalid terrain accepted");
  Console.WriteLine("PASS terrain gate: delayed readback, unstable samples, tolerance, timeout, missing/nonfinite samples");
 }
 public static void Run(string label){
  var i=JsonConvert.DeserializeObject<Input>(File.ReadAllText("ApartmentPlan/docs/loop-before-layout-last-input.json"));
  var p=Layout.Generate(i.Boundary,i.Entrances,i.Assets,i.Options,i.Ramps,i.Zones,i.Roads);
  if(p.RequiredAccessFailures!=0||p.ParkingAccessFailures!=0)throw new Exception("access failures");
  var local=i.Boundary.Select(p.Local).ToArray();
  foreach(var r in p.Parcels)if(!Layout.Contains(r,local))throw new Exception("outside");
  for(int a=0;a<p.Parcels.Count;a++)for(int b=0;b<a;b++)if(Layout.Overlap(p.Parcels[a],p.Parcels[b]))throw new Exception("overlap");
  var graph=ConstructionGraph.Create(p).Where(e=>e.Use!="pedestrian").ToList();var seen=new HashSet<ConstructionEdge>{graph[0]};bool more=true;
  while(more){more=false;foreach(var e in graph)if(!seen.Contains(e)&&seen.Any(q=>new[]{q.A,q.B}.Any(a=>new[]{e.A,e.B}.Any(b=>(a-b).Length<.03)))){seen.Add(e);more=true;}}
  if(seen.Count!=graph.Count)throw new Exception("disconnected native graph "+seen.Count+"/"+graph.Count);
  if(label=="after"){
   var old=JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(File.ReadAllText("ApartmentPlan/docs/loop-before.json"))["Plan"].ToObject<Plan>();
   Check(old.Parcels.Count(r=>r.Kind==0)==p.Parcels.Count(r=>r.Kind==0)&&old.Parcels.Sum(r=>r.ParkingSpaces)==p.Parcels.Sum(r=>r.ParkingSpaces),"closure removed buildings or bays");
   Check(p.ConstructionRoutes.Count>old.ConstructionRoutes.Count,"no closure roads generated");
   Check(p.Parcels.Where(r=>r.Kind==8).Sum(r=>r.Area)>old.Parcels.Where(r=>r.Kind==8).Sum(r=>r.Area),"closed parking void not paved");
   var oldRoutes=new HashSet<string>(old.ConstructionRoutes.Select(JsonConvert.SerializeObject));
   foreach(var route in p.ConstructionRoutes.Where(r=>r.Use=="aisle"&&!oldRoutes.Contains(JsonConvert.SerializeObject(r)))){
    var a=route.Points[0];var b=route.Points[1];double pad=route.Width/2;
    var bounds=new Parcel{X=Math.Min(a.X,b.X)-pad,Y=Math.Min(a.Y,b.Y)-pad,W=Math.Abs(a.X-b.X)+2*pad,D=Math.Abs(a.Y-b.Y)+2*pad};
    Check(Layout.Contains(bounds,local)&&!p.Parcels.Any(r=>(r.Kind==0||r.Kind==2||r.Kind==5)&&Layout.Overlap(bounds,r)),"closure collision");
   }
  }
  File.WriteAllText("ApartmentPlan/docs/loop-"+label+".json",JsonConvert.SerializeObject(new{Plan=p,i.Assets,i.Options,i.Entrances,i.Zones}));
  Console.WriteLine("PASS "+label+" buildings="+p.Parcels.Count(r=>r.Kind==0)+" bays="+p.Parcels.Sum(r=>r.ParkingSpaces)+" routes="+p.ConstructionRoutes.Count+" paving="+p.Parcels.Where(r=>r.Kind==8).Sum(r=>r.Area));
 }
}

