using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ApartmentPlan;
static class ConnectionUpdateTests {
 public static void WalkCurves(){
  var outputs=new List<object>();
  foreach(string kind in new[]{"clear","obstacle","junction","short","entrance"}){
   var p=Layout.Frame(new[]{new Point(0,0),new Point(100,0),new Point(100,100),new Point(0,100)},new Point(50,0));
   var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var solver=(ResidentialLayout)Activator.CreateInstance(typeof(ResidentialLayout),flags,null,new object[]{p,new LayoutOptions{Road=8,Walkway=3},new List<Asset>(),null},null);
   object Call(string name,params object[] args)=>typeof(ResidentialLayout).GetMethod(name,flags).Invoke(solver,args);
   var route=new PlanRoute{Use="pedestrian",Width=3,Points=new List<Point>{new Point(0,20),new Point(20,20),new Point(20,40)}};
   if(kind=="short")route.Points[2]=new Point(20,23);
   if(kind=="entrance")route.Points=new List<Point>{new Point(-20,6),new Point(0,6),new Point(0,26)};
   p.Routes.Add(route);
   if(kind=="obstacle"){
    var box=new Parcel{Kind=0,X=17.9,Y=21.6,W=.5,D=.5};p.Parcels.Add(box);Call("Paint",box,10);
   }
   if(kind=="junction")p.Routes.Add(new PlanRoute{Use="pedestrian",Width=3,Points=new List<Point>{new Point(20,20),new Point(40,20)}});
   var before=ConstructionCurves.Create(p);Call("RoundWalkCorners");var after=ConstructionCurves.Create(p);
   int count=after.Count(e=>e.Use=="pedestrian"&&e.Curved);
   if(count!=(kind=="clear"?1:0))throw new Exception("walk corner "+kind+" curves="+count);
   if(kind=="clear"){
    var c=after.Single(e=>e.Curved).Curve;
    if(after.Count(e=>e.Use=="pedestrian")!=3||Math.Abs(route.CornerRadii.Single()-2)>.001)throw new Exception("Bezier split or radius changed");
    if(Math.Abs(Layout.Cross(c.Tangent(0),new Point(1,0)))>.001||Math.Abs(Layout.Cross(c.Tangent(1),new Point(0,1)))>.001)throw new Exception("walk tangent discontinuity");
   }
   outputs.Add(new{Kind=kind,Plan=p,Before=before,After=after});Console.WriteLine("PASS walk Bezier "+kind+": curves="+count);
  }
  File.WriteAllText("ApartmentPlan/docs/walk-curves.json",JsonConvert.SerializeObject(outputs));
 }
 public static void EntranceWalks(string label){
  var boundary=new[]{new Point(0,0),new Point(100,0),new Point(100,100),new Point(0,100)};
  var gates=new[]{new Point(50,0),new Point(50,100)};
  var p=Layout.Frame(boundary,gates[0]);var o=new LayoutOptions{Walkway=3,Road=8,BoundaryGreenMargin=4,MainRoadPedestrian=true};
  o.ExternalWalkTargets.Add(new ExternalWalkTarget{Inside=new Point(90,6),Boundary=new Point(90,0),Road=new Point(90,-6)});
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var solver=(ResidentialLayout)Activator.CreateInstance(typeof(ResidentialLayout),flags,null,new object[]{p,o,new List<Asset>(),null},null);
  object Call(string name,params object[] args)=>typeof(ResidentialLayout).GetMethod(name,flags).Invoke(solver,args);
  Call("EntranceParcel",gates[1]);Call("MainRoads");
  double x0=(double)typeof(ResidentialLayout).GetField("x0",flags).GetValue(solver);
  double cell=(double)typeof(ResidentialLayout).GetField("cell",flags).GetValue(solver);
  foreach(var start in new[]{new Point(20,4),new Point(20,96)}){
   Func<int,int,bool> target=(x,y)=>Math.Abs(x0+(x+.5)*cell)<.6;
   var route=(List<Point>)Call("FindRoute",start,target,3d,true,double.MaxValue);
   if(route==null)throw new Exception("entrance walkway detour missing");Call("DrawRoute",route,3d,"pedestrian");
  }
  Call("OpenSpaceWalks");
  var graph=ConstructionGraph.Create(p);
  var paths=graph.Where(e=>e.Use=="pedestrian").ToList();
  double Distance(Point q,ConstructionEdge e){var d=e.B-e.A;double t=Math.Max(0,Math.Min(1,SiteFrame.Dot(q-e.A,d)/SiteFrame.Dot(d,d)));return (q-e.A-d*t).Length;}
  double clearance=paths.Min(e=>gates.Min(g=>Distance(g,e))-e.Width/2);
  if(label!="before"&&clearance<8-1e-6)throw new Exception("walk ribbon intrudes entrance radius: "+clearance);
  if(!p.ConstructionRoutes.Any(r=>r.Use=="pedestrian"&&r.Points.Any(q=>p.World(q).Y<0)))throw new Exception("boundary exit lost");
  File.WriteAllText("ApartmentPlan/docs/entrance-walk-"+label+".json",JsonConvert.SerializeObject(new{Plan=p,Graph=graph,Gates=gates,Clearance=clearance}));
  Console.WriteLine("PASS entrance walks "+label+": two gates, boundary exit retained; minimum path edge clearance="+clearance.ToString("F2")+"m");
 }
 public static void Run(){
  var input=JObject.Parse(File.ReadAllText("ApartmentPlan/docs/frontage-repair-input.json"));
  var assets=input["Assets"].ToObject<List<Asset>>();var options=input["Options"].ToObject<LayoutOptions>();var zones=input["Zones"].ToObject<List<ManualZone>>();
  var old=JObject.Parse(File.ReadAllText("ApartmentPlan/docs/frontage-repair-before.json"))["Plan"].ToObject<Plan>();
  if(FrontageNetwork.Missing(old,assets,4,options.ParkingPavingMargin)!=1)throw new Exception("captured rejection not reproduced");
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;int count=old.ConstructionRoutes.Count;
  var solver=(ResidentialLayout)Activator.CreateInstance(typeof(ResidentialLayout),flags,null,new object[]{old,options,assets,zones},null);
  old.ConstructionRoutes.RemoveRange(count,old.ConstructionRoutes.Count-count);
  var paint=typeof(ResidentialLayout).GetMethod("Paint",flags);var exact=(List<Parcel>)typeof(ResidentialLayout).GetField("exact",flags).GetValue(solver);
  foreach(var p in old.Parcels){paint.Invoke(solver,new object[]{p,p.Kind==0||p.Kind==2?10:p.Kind});if(p.Kind==0||p.Kind==2||p.Kind==3||p.Kind==5||p.Kind==6)exact.Add(p);}
  typeof(ResidentialLayout).GetMethod("RepairFrontageConnections",flags).Invoke(solver,null);
  if(FrontageNetwork.Missing(old,assets,4,options.ParkingPavingMargin)!=0)throw new Exception("exact captured plan not repaired");
  Console.WriteLine("PASS exact captured 14-building plan: missing 1 -> 0; no lot or parking moved");
  foreach(int mode in new[]{0,1}){
   options.BuildingPattern=mode;
   var p=Layout.Generate(input["Boundary"].ToObject<List<Point>>(),input["Entrances"].ToObject<List<Point>>(),assets,options,input["Ramps"].ToObject<List<Parcel>>(),zones,input["Roads"].ToObject<List<ManualRoad>>());
   if(p.ParkingAccessFailures!=0||p.RequiredAccessFailures!=0||FrontageNetwork.Missing(p,assets,options.ParkingAisle,options.ParkingPavingMargin)!=0)throw new Exception("regenerated connection failed mode "+mode);
   var green=new GreenSpace(p);var species=new[]{new TreeSpecies{Id="test",Radius=2}};
   var trees=Enumerable.Range(0,3).Select(m=>green.Trees(species,12,6,73471,m)).ToArray();
   if(trees[0].Count!=trees[1].Count+trees[2].Count||trees[1].Any(t=>green.BoundaryDistance(t.Position)>12)||trees[2].Any(t=>green.BoundaryDistance(t.Position)<=12))throw new Exception("tree band separation");
   File.WriteAllText("ApartmentPlan/docs/connection-update-"+mode+".json",JsonConvert.SerializeObject(new{Plan=p,Options=options,Assets=assets,Entrances=input["Entrances"],Trees=trees}));
   Console.WriteLine("PASS mode="+mode+" buildings="+p.Parcels.Count(r=>r.Kind==0)+" bays="+p.Parcels.Sum(r=>r.ParkingSpaces)+" wide="+p.Routes.Concat(p.ConstructionRoutes).Count(r=>r.Use=="aisleWide")+" trees all/border/interior="+string.Join("/",trees.Select(t=>t.Count)));
  }
 }
}
