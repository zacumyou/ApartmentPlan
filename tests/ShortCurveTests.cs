using System;
using System.Linq;
using ApartmentPlan;
static class ShortCurveTests {
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
 public static void Run(){
  var site=new[]{new Point(0,0),new Point(400,0),new Point(400,600),new Point(0,600)};
  Plan Make(Point[] entrances,double scale)=>Layout.Generate(site,entrances,new Asset[0],new LayoutOptions{MainCurveRadius=80,MainCurveLengthRatio=scale,MainRoadCornerMode=1},null,null);
  var perpendicular=new[]{new Point(160,0),new Point(400,350)};
  var old=Make(perpendicular,1);var next=Make(perpendicular,.5);
  var oldR=old.Routes.SelectMany(r=>r.CornerRadii).ToArray();var nextR=next.Routes.SelectMany(r=>r.CornerRadii).ToArray();
  Check(oldR.Length>0&&oldR.Length==nextR.Length&&oldR.Zip(nextR,(a,b)=>Math.Abs(a*.5-b)<.001).All(x=>x),"same corner skeleton halves arc radii and arc lengths");
  var offset=new[]{new Point(160,0),new Point(162,600)};old=Make(offset,1);next=Make(offset,.5);
  double Transition(Plan p){var points=p.Routes.Where(r=>r.Use=="main"&&r.CornerRadii.Count>0).SelectMany(r=>r.Points).Select(p.World).ToArray();var moving=points.Where(q=>q.X>160.001&&q.X<161.999).ToArray();return moving.Max(q=>q.Y)-moving.Min(q=>q.Y);}
  Check(Math.Abs(Transition(next)/Transition(old)-.5)<.02,"shallow S transition occupies half the previous longitudinal span");
  Check(next.Routes.SelectMany(r=>r.Points).All(q=>Layout.Inside(next.World(q),site)),"shortened route remains inside site");
  Check(next.Routes.Any(r=>r.Use=="main"&&r.CornerRadii.Count>0),"offset entrances retain curved connected route");
  var codec=new Codec();new PlanConstructionState{Applied=1}.Serialize(codec);codec.Reset();var state=new PlanConstructionState();state.Deserialize(codec);Check(state.Applied==1,"placed marker persists independently of editable plan payload");
  CornerEditingTests.Run();
 }
}
static class RoadNodeGapTests {
 public static void Run(){
  foreach(double gap in new[]{.005,.02,.029}){
   var p=new Plan{Right=new Point(1,0),Forward=new Point(0,1)};
   p.Routes.Add(new PlanRoute{Use="main",Width=8,Points=new System.Collections.Generic.List<Point>{new Point(-50,0),new Point(50,0)}});
   p.Routes.Add(new PlanRoute{Use="main",Width=8,Points=new System.Collections.Generic.List<Point>{new Point(0,40),new Point(0,gap)}});
   var courses=ConstructionCurves.Create(p);
   var nodes=courses.SelectMany(c=>new[]{c.Curve.A,c.Curve.D}).GroupBy(q=>Math.Round(q.X,6)+","+Math.Round(q.Y,6));
   if(!nodes.Any(g=>g.Count()==3))throw new Exception("Disconnected T junction at gap "+gap);
   if(courses.Count!=3)throw new Exception("Tiny redundant road segment remains");
   System.IO.File.WriteAllText("ApartmentPlan/docs/road-node-gap.json",Newtonsoft.Json.JsonConvert.SerializeObject(courses));
   Console.WriteLine("PASS shared T junction / 3 courses / gap="+gap);
  }
 }
}

static class ManualConnectionTimingTests {
 public static void Run(){
  var stages=new System.Collections.Generic.List<string>();long sharedMs=0;
  var o=new LayoutOptions{Road=8,PedestrianNetwork=false};o.Timing=(name,ms)=>{if(ms<0)throw new Exception("Negative elapsed");stages.Add(name);if(name=="sharedParking")sharedMs=ms;};
  var boundary=new[]{new Point(0,0),new Point(300,0),new Point(300,300),new Point(0,300)};
  var roads=new[]{new ManualRoad{Points=new System.Collections.Generic.List<Point>{new Point(220,90),new Point(220,190)}}};
  var p=Layout.Generate(boundary,new[]{new Point(70,0)},new Asset[0],o,null,null,roads);
  var courses=ConstructionCurves.Create(p).Where(c=>c.Use!="pedestrian").ToList();
  var seen=new System.Collections.Generic.HashSet<int>{0};bool more=true;while(more){more=false;for(int i=0;i<courses.Count;i++)if(!seen.Contains(i)&&seen.Any(j=>new[]{courses[i].Curve.A,courses[i].Curve.D}.Any(a=>new[]{courses[j].Curve.A,courses[j].Curve.D}.Any(b=>(a-b).Length<.03)))){seen.Add(i);more=true;}}
  if(seen.Count!=courses.Count)throw new Exception("Manual road is disconnected");
  if(!stages.Contains("manualRoads")||!stages.Contains("parkingLoops")||!stages.Contains("geometryFinish"))throw new Exception("Missing timing phases");
  System.IO.File.WriteAllText("ApartmentPlan/docs/manual-connection-0105.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {courses,stages}));
  p.LayoutTimings.Clear();Console.WriteLine("sharedMs="+sharedMs+" hash="+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(p)))));
  Console.WriteLine("PASS detached manual road joins entrance component: "+seen.Count+"/"+courses.Count+"; measured stages="+stages.Count);
 }
}
