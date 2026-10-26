using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ApartmentPlan;
static class DiagonalRoadTests {
 public static void Run(string stage){
  if(stage!="before")AccessChecks();
  foreach(var name in new[]{"access","offset"}){
   var input=JObject.Parse(File.ReadAllText("ApartmentPlan/docs/access-latest-input.json"));
   var boundary=input["Boundary"].ToObject<List<Point>>();var entrances=input["Entrances"].ToObject<List<Point>>();var assets=input["Assets"].ToObject<List<Asset>>();var o=input["Options"].ToObject<LayoutOptions>();
   var zones=input["Zones"].ToObject<List<ManualZone>>();var roads=input["Roads"].ToObject<List<ManualRoad>>();
   if(name=="offset"){
    boundary=new List<Point>{new Point(0,0),new Point(240,0),new Point(240,360),new Point(0,360)};
    entrances=new List<Point>{new Point(80,0),new Point(160,360)};roads.Clear();
   }
   var p=Layout.Generate(boundary,entrances,assets,o,null,zones,roads);EnhancementTests.Verify(p,assets,o);
   if(stage!="before"&&name=="offset"){
    if(!p.Routes.Where(r=>r.Use=="main").Any(r=>r.Points.Zip(r.Points.Skip(1),(a,b)=>b-a).Any(d=>Math.Abs(d.X)>1&&Math.Abs(Math.Abs(d.X)-Math.Abs(d.Y))<.01)))throw new Exception("main diagonal missing");
    var curves=ConstructionCurves.Create(p);if(!curves.Any(c=>c.Use=="main"&&c.Curved))throw new Exception("native Bezier lost");
   }
   File.WriteAllText("ApartmentPlan/docs/diagonal-"+name+"-"+stage+".json",JsonConvert.SerializeObject(new{Plan=p,Assets=assets,Options=o,Entrances=entrances,Zones=zones}));
   Console.WriteLine(name+" "+stage+": "+p.Parcels.Count(r=>r.Kind==0)+" buildings / "+p.Parcels.Sum(r=>r.ParkingSpaces)+" bays");
  }
 }
 static void AccessChecks(){
  foreach(double width in new[]{4.0,7.0}){
   var original=new List<Point>{new Point(20.5,30.5),new Point(40.5,30.5),new Point(40.5,46.5),new Point(64.5,46.5)};
   var shortened=DiagonalRouting.Shorten(original,width,1,(a,b)=>true);
   if((shortened[0]-original[0]).Length>.001||(shortened.Last()-original.Last()).Length>.001)throw new Exception("endpoints moved");
   double Length(List<Point> p)=>p.Zip(p.Skip(1),(a,b)=>(b-a).Length).Sum();
   if(Length(shortened)>=Length(original)-1)throw new Exception("dogleg not shortened");
   var d=shortened[2]-shortened[1];if(Math.Abs(Math.Abs(d.X)-Math.Abs(d.Y))>.001)throw new Exception("not 45 degrees");
   var blocked=DiagonalRouting.Shorten(original,width,1,(a,b)=>false);
   if(blocked.Where((p,i)=>(p-original[i]).Length>.001).Any())throw new Exception("blocked shortcut accepted");
   // Exercise the real map reservation and DrawRoute, not just a line helper.
   var boundary=new List<Point>{new Point(0,0),new Point(200,0),new Point(200,100),new Point(0,100)};
   var plan=Layout.Frame(boundary,new Point(100,0));
   var solver=(ResidentialLayout)Activator.CreateInstance(typeof(ResidentialLayout),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{plan,new LayoutOptions(),new List<Asset>(),null},null);
   var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var map=(int[,])typeof(ResidentialLayout).GetField("map",flags).GetValue(solver);
   int X(double x)=>(int)typeof(ResidentialLayout).GetMethod("X",flags).Invoke(solver,new object[]{x});
   int Y(double y)=>(int)typeof(ResidentialLayout).GetMethod("Y",flags).Invoke(solver,new object[]{y});
   var clear=typeof(ResidentialLayout).GetMethod("ClearDiagonal",flags);
   var midpoint=(shortened[1]+shortened[2])*.5;
   // A blocker at the edge of the ribbon must reject even a clear centreline.
   var normal=new Point(-d.Y/d.Length,d.X/d.Length);var obstacle=midpoint+normal*(width/2-.4);
   int ox=X(obstacle.X),oy=Y(obstacle.Y),old=map[ox,oy];map[ox,oy]=10;
   if((bool)clear.Invoke(solver,new object[]{shortened[1],shortened[2],width}))throw new Exception("full width obstacle missed");
   map[ox,oy]=old;
   var tight=new List<Point>{new Point(20,20),new Point(26,20),new Point(26,36),new Point(32,36)};
   if(DiagonalRouting.Shorten(tight,width,1,(a,b)=>true).Where((p,i)=>(p-tight[i]).Length>.001).Any())throw new Exception("short entrance leads consumed");
   var dense=new List<Point>();for(int n=1;n<original.Count;n++){var a=original[n-1];var delta=original[n]-a;for(int j=0;j<=delta.Length;j++)dense.Add(a+delta*(j/delta.Length));}
   var draw=typeof(ResidentialLayout).GetMethod("DrawRoute",BindingFlags.Instance|BindingFlags.NonPublic);
   draw.Invoke(solver,new object[]{dense,width,width==7?"aisleWide":"access"});
   var route=plan.Routes.Last();if(!route.Points.Zip(route.Points.Skip(1),(a,b)=>b-a).Any(q=>Math.Abs(q.X)>1&&Math.Abs(q.Y)>1))throw new Exception("production shortcut missing");
   for(int k=0;k<=32;k++){
    var center=route.Points[1]+(route.Points[2]-route.Points[1])*(k/32.0);
    foreach(double across in new[]{-.49,0,.49}){var q=center+normal*(width*across);if(map[X(q.X),Y(q.Y)]!=1)throw new Exception("diagonal ribbon not reserved");}
   }
   File.WriteAllText("ApartmentPlan/docs/diagonal-aisle-"+width+".json",JsonConvert.SerializeObject(new{Before=original,After=route.Points,Width=width,BeforeLength=Length(original),AfterLength=Length(route.Points)}));
   Console.WriteLine("PASS "+width+"m production dogleg: "+Length(original).ToString("F2")+" -> "+Length(route.Points).ToString("F2")+"m");
  }
 }
}
