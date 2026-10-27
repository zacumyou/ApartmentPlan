using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ApartmentPlan;
static class FrontageReportTests {
 internal static void Run(){
  var p=new Plan{Right=new Point(1,0),Forward=new Point(0,1),Boundary=new List<Point>{new Point(-10,-10),new Point(100,-10),new Point(100,100),new Point(-10,100)}};
  var asset=new Asset{Id="Test",Width=20,Depth=10};
  p.Parcels.Add(new Parcel{Kind=0,AssetId="Test",X=20,Y=10,W=20,D=10,Frontage=new Parcel{X=20,Y=20,W=20,D=4}});
  p.Routes.Add(new PlanRoute{Use="main",Width=8,Points=new List<Point>{new Point(0,0),new Point(0,5)}});
  p.Routes.Add(new PlanRoute{Use="aisle",Width=4,Points=new List<Point>{new Point(20,22),new Point(40,22)}});
  var evidence=new List<Dictionary<string,int>>();
  var details=new List<string>();
  if(FrontageNetwork.Missing(p,new[]{asset},4,0,details,evidence:evidence)!=1||!details.Single().Contains("reason=disconnected-from-entrance"))throw new Exception("Disconnected frontage reason missing");
  if(evidence.Single()["reason"]!=13||evidence[0]["widthCm"]!=2000)throw new Exception("numeric disconnect evidence");evidence.Clear();
  p.Routes.Add(new PlanRoute{Use="aisle",Width=4,Points=new List<Point>{new Point(0,5),new Point(0,22),new Point(20,22)}});
  details.Clear();if(FrontageNetwork.Missing(p,new[]{asset},4,0,details,evidence:evidence)!=0||details.Count!=0)throw new Exception("Connected frontage incorrectly rejected");
  if(evidence.Count!=0)throw new Exception("stale evidence");
  p.Routes[1].Points[1]=new Point(30,22);details.Clear();
  if(FrontageNetwork.Missing(p,new[]{asset},4,0,details,evidence:evidence)!=1||!details.Single().Contains("reason=frontage-coverage"))throw new Exception("Short frontage reason missing");
  if(evidence.Single()["reason"]!=14||evidence[0]["coverageCm"]>=evidence[0]["widthCm"])throw new Exception("numeric coverage evidence");
  Console.WriteLine("PASS frontage diagnostics distinguish disconnected, connected and short coverage without suppressing rejection");
 }
 internal static void Capture(string directory){
  var input=JObject.Parse(File.ReadAllText(Path.Combine(directory,"layout-last-input.json")));
  var assets=input["Assets"].ToObject<Asset[]>();var o=input["Options"].ToObject<LayoutOptions>();
  var plan=Layout.Generate(input["Boundary"].ToObject<List<Point>>(),input["Entrances"].ToObject<List<Point>>(),assets,o,input["Ramps"].ToObject<List<Parcel>>(),input["Zones"].ToObject<List<ManualZone>>(),input["Roads"].ToObject<List<ManualRoad>>());
  File.WriteAllText("ApartmentPlan/docs/frontage-repair-output.json",JsonConvert.SerializeObject(new{Plan=plan,Assets=assets,Options=o,Entrances=input["Entrances"]}));
  var detail=new List<string>();Console.WriteLine("missing="+FrontageNetwork.Missing(plan,assets,o.ParkingAisle,o.ParkingPavingMargin,detail));
  foreach(var line in detail)Console.WriteLine(line);
  var graph=ConstructionGraph.Create(plan).Where(e=>e.Use!="pedestrian").ToList();
  string Key(Point p)=>Math.Round(p.X*100)+","+Math.Round(p.Y*100);
  var seen=new HashSet<ConstructionEdge>{graph.OrderBy(e=>Math.Min((e.A-plan.Origin).Length,(e.B-plan.Origin).Length)).First()};
  bool changed=true;while(changed){changed=false;foreach(var e in graph)if(!seen.Contains(e)&&seen.Any(q=>new[]{q.A,q.B}.Any(a=>new[]{e.A,e.B}.Any(b=>Key(a)==Key(b))))){seen.Add(e);changed=true;}}
  var near=(from e in graph.Except(seen) from a in new[]{e.A,e.B} from q in seen from b in new[]{q.A,q.B} select new {d=(a-b).Length,a=plan.Local(a),b=plan.Local(b),e.Use}).OrderBy(x=>x.d).Take(5);
  foreach(var n in near)Console.WriteLine(JsonConvert.SerializeObject(n));
 }
}
