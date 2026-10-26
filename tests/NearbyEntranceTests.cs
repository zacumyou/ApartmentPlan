using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ApartmentPlan;
static class NearbyEntranceTests {
 public static void Run(string label){
  var boundary=new[]{new Point(0,0),new Point(180,0),new Point(180,600),new Point(0,600)};
  var gates=new[]{new Point(0,450),new Point(180,465),new Point(0,100),new Point(180,60)};
  var p=Layout.Generate(boundary,gates,Array.Empty<Asset>(),new LayoutOptions{Road=8,PedestrianNetwork=false,MainCurveRadius=40},null,null);
  var courses=ConstructionCurves.Create(p).Where(e=>e.Use=="main").ToList();
  File.WriteAllText("ApartmentPlan/docs/nearby-entrances-"+label+".json",JsonConvert.SerializeObject(new{Plan=p,Gates=gates,Courses=courses}));
  Console.WriteLine(label+": main courses="+courses.Count+" length="+courses.Sum(e=>Enumerable.Range(1,32).Sum(i=>(e.Curve.At(i/32d)-e.Curve.At((i-1)/32d)).Length)));
  if(label=="before")return;
  var edges=ConstructionGraph.Create(p).Where(e=>e.Use=="main").ToList();
  var nodes=edges.SelectMany(e=>new[]{e.A,e.B}).GroupBy(q=>Math.Round(q.X*100)+","+Math.Round(q.Y*100)).Where(g=>g.Count()>2).Select(g=>g.First()).ToList();
  if(nodes.Count!=2)throw new Exception("four gates must have two T junctions");
  foreach(var q in nodes){
   var ds=edges.Where(e=>(e.A-q).Length<.03||(e.B-q).Length<.03).Select(e=>(e.A-q).Length<.03?e.B-q:e.A-q).ToList();
   if(ds.Count!=3||!ds.Any(a=>ds.Any(b=>SiteFrame.Dot(a,b)<-.999*a.Length*b.Length)))throw new Exception("not a straight through T");
   foreach(var c in courses.Where(c=>c.Curved))if((c.Curve.At(c.Curve.Parameter(q))-q).Length<8)throw new Exception("junction too close to curve");
  }
  if(!nodes.Any(q=>q.Y<180))throw new Exception("lower gate still joins upper road");
  Console.WriteLine("PASS four gates: two straight T junctions, lower gate joins nearby lower spine, curve clearance");
 }
}
