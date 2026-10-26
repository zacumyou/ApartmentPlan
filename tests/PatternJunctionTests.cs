using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using ApartmentPlan;
static class PatternJunctionTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
 internal static void Run(){
  var boundary=new[]{new Point(0,0),new Point(240,0),new Point(240,360),new Point(0,360)};
  var gates=new[]{new Point(120,0),new Point(120,360),new Point(0,150),new Point(240,240)};
  var assets=new[]{new Asset{Id="Slab",Width=48,Depth=20,Height=60,Homes=80},new Asset{Id="Short slab",Width=32,Depth=16,Height=45,Homes=60}};
  for(int mode=-1;mode<=3;mode++){
   var o=new LayoutOptions{BuildingPattern=Math.Max(0,mode),AllowMainTJunctions=mode>=0,ContinuousFrontage=true,ParkingAisle=4,AccessRoad=4,WideParkingAisle=7,ParkingPavingMargin=0,BayWidth=2.85,BayDepth=6,MainCurveRadius=40,MainRoadPedestrian=true,AisleRoadPedestrian=true,PedestrianNetwork=false,MaxAccessLength=60};
   var p=Layout.Generate(boundary,gates,assets,o,null,null);
   var lots=p.Parcels.Where(r=>r.Kind==0).ToList();Check(lots.Count>0,"mode "+mode+" has buildings");
   if(mode>0)Check(p.Courts.All(c=>c.Pattern!="u"&&c.Pattern!="ring"),"bar mode excludes U and ring templates");
   if(mode==1)Check(lots.All(p=>p.W>=p.D-.01),"horizontal only, including infill");
   if(mode==2)Check(lots.All(p=>p.D>=p.W-.01),"vertical only, including infill");
   if(mode==3)Check(lots.Any(p=>p.W>p.D)&&lots.Any(p=>p.D>p.W),"mixed bars uses both directions");
   Check(p.ParkingAccessFailures==0&&FrontageNetwork.Missing(p,assets,4,0)==0,"parking and complete frontage connected mode "+mode);
   var graph=ConstructionGraph.Create(p).Where(e=>e.Use=="main").ToList();
   string Key(Point q)=>Math.Round(q.X*100)+","+Math.Round(q.Y*100);
   var junctions=graph.SelectMany(e=>new[]{e.A,e.B}).GroupBy(Key).Where(g=>g.Count()==3).Select(g=>g.First()).ToList();
   if(mode>=0){
    Check(junctions.Count>=2,"native graph has both T junctions");
    Check(p.Routes.Where(r=>r.Use=="main").All(r=>r.Curves.Count==0),"straight branch fixture does not detour through curves");
    foreach(var q in junctions){var directions=graph.Where(e=>(e.A-q).Length<.01||(e.B-q).Length<.01).Select(e=>(e.A-q).Length<.01?e.B-q:e.A-q).ToArray();Check(directions.Length==3&&directions.Any(a=>directions.Any(b=>SiteFrame.Dot(a,b)<-.99*a.Length*b.Length)),"T junction retains straight through road");}
   }
   File.WriteAllText("ApartmentPlan/docs/pattern-junction-"+mode+".json",JsonConvert.SerializeObject(new{Plan=p,Assets=assets,Options=o,Entrances=gates,Junctions=junctions}));
   Console.WriteLine("MODE "+mode+": buildings="+lots.Count+" bays="+p.Parcels.Sum(r=>r.ParkingSpaces)+" T="+junctions.Count);
  }
  var options=new LayoutOptions{PedestrianNetwork=false};
  var pair=gates.Take(2).ToArray();options.AllowMainTJunctions=false;
  var before=Layout.Generate(boundary,pair,Array.Empty<Asset>(),options,null,null);
  options.AllowMainTJunctions=true;var after=Layout.Generate(boundary,pair,Array.Empty<Asset>(),options,null,null);
  Check(JsonConvert.SerializeObject(before.Routes)==JsonConvert.SerializeObject(after.Routes),"two-entrance routing unchanged");
  var mask=new ManualZone{Kind=5,Points=new List<Point>{new Point(25,135),new Point(80,135),new Point(80,165),new Point(25,165)}};
  var masked=Layout.Generate(boundary,gates,Array.Empty<Asset>(),options,null,new[]{mask});
  Check(!masked.Routes.Where(r=>r.Use=="main").SelectMany(r=>r.Points).Select(masked.World).Any(p=>p.X>25&&p.X<80&&p.Y>135&&p.Y<165),"blocked direct T approach preserves exclusion mask");
 }
}
