using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using ApartmentPlan;
static class ParkingFinishTests {
 internal static void Run(string phase){
  var boundary=new[]{new Point(0,0),new Point(170,0),new Point(170,220),new Point(0,220)};
  var gates=new[]{new Point(85,0),new Point(85,220)};
  var assets=new[]{new Asset{Id="Test apartment",Width=40,Depth=32,Height=90,Homes=100}};
  var o=new LayoutOptions{ContinuousFrontage=true,ParkingAisle=4,AccessRoad=4,WideParkingAisle=7,ParkingPavingMargin=1,BayWidth=2.85,BayDepth=6,MainRoadPedestrian=true,AisleRoadPedestrian=true,PedestrianNetwork=false,MaxAccessLength=60,ParkingOrientation=2};
  var p=Layout.Generate(boundary,gates,assets,o,null,null);
  if(p.ParkingAccessFailures!=0||FrontageNetwork.Missing(p,assets,4,1)!=0)throw new Exception("Disconnected parking/frontage");
  var paving=ParkingSurfaceGeometry.Paving(p,true).ToArray();
  Console.WriteLine("Paving patches="+paving.Length+" minEdge="+paving.Min(r=>Math.Min(r.W,r.D))+" grass="+p.GrassPockets.Sum(r=>r.Area));
  if(phase!="before")Focused(o);
  File.WriteAllText("ApartmentPlan/docs/parking-finish-"+phase+".json",JsonConvert.SerializeObject(new{Plan=p,Assets=assets,Options=o,Entrances=gates,Paving=paving,Aprons=ParkingSurfaceGeometry.JunctionAprons(ConstructionCurves.Create(p))}));
  Console.WriteLine(phase+": "+p.Parcels.Count(r=>r.Kind==0)+" buildings / "+p.Parcels.Sum(r=>r.ParkingSpaces)+" bays; connectivity PASS");
 }
 static void Focused(LayoutOptions o){
  var boundary=new[]{new Point(0,0),new Point(120,0),new Point(120,120),new Point(0,120)};
  var plan=Layout.Frame(boundary,new Point(60,0));
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var solver=(ResidentialLayout)Activator.CreateInstance(typeof(ResidentialLayout),flags,null,new object[]{plan,o,new List<Asset>(),null},null);
  plan.Routes.Add(new PlanRoute{Use="main",Width=8,Points=new List<Point>{new Point(0,0),new Point(0,20),new Point(20,20)}});
  var paint=typeof(ResidentialLayout).GetMethod("Paint",flags);
  paint.Invoke(solver,new object[]{new Parcel{X=-4,Y=0,W=8,D=24},1});
  paint.Invoke(solver,new object[]{new Parcel{X=-4,Y=16,W=28,D=8},1});
  typeof(ResidentialLayout).GetMethod("ProtectMainRoad",flags).Invoke(solver,null);
  var route=new PlanRoute{Use="aisleWide",Width=7,Points=new List<Point>{new Point(20,20),new Point(20,80)}};
  plan.ConstructionRoutes.Add(route);
  var exact=(List<Parcel>)typeof(ResidentialLayout).GetField("exact",flags).GetValue(solver);
  var blockers=new[]{new Parcel{Kind=5,X=4,Y=29,W=8,D=40},new Parcel{Kind=5,X=28,Y=29,W=8,D=40}};
  foreach(var blocker in blockers){exact.Add(blocker);paint.Invoke(solver,new object[]{blocker,10});}
  if(!Layout.Overlap(new Parcel{X=10.5,Y=29,W=6,D=39.9},blockers[0])||!Layout.Overlap(new Parcel{X=23.5,Y=29,W=6,D=39.9},blockers[1]))throw new Exception("fixture would fit wide bays");
  typeof(ResidentialLayout).GetMethod("Paint",flags).Invoke(solver,new object[]{new Parcel{X=16.5,Y=20,W=7,D=60},1});
  typeof(ResidentialLayout).GetMethod("ReclaimWideParking",flags).Invoke(solver,null);
  var rows=exact.Where(r=>r.Kind==2).ToArray();
  if(route.Width!=4||route.Use!="aisle"||rows.Length!=2||rows.Sum(r=>r.ParkingSpaces)!=28)throw new Exception("wide aisle not reclaimed");
  if(route.Points[0].Y!=20||route.Points[1].Y!=80||rows.Any(r=>Layout.Overlap(r,new Parcel{X=18,Y=20,W=4,D=60})))throw new Exception("reclaimed road obstructed");
  typeof(ResidentialLayout).GetMethod("Finish",flags).Invoke(solver,null);ParkingTraffic.Validate(plan,o);
  if(plan.ParkingAccessFailures!=0)throw new Exception("reclaimed rows inaccessible");
  var aprons=ParkingSurfaceGeometry.JunctionAprons(ConstructionCurves.Create(plan)).ToArray();
  if(aprons.Length!=1||aprons[0].Any(q=>double.IsNaN(q.X)))throw new Exception("native junction apron missing");
  var joint=plan.World(new Point(20,20));if(aprons[0].Count(q=>Math.Abs(q.Y-joint.Y)<.001)!=2)throw new Exception("apron stops before road centre");
  File.WriteAllText("ApartmentPlan/docs/parking-reclaim.json",JsonConvert.SerializeObject(new{BeforeWidth=7,After=route,Rows=rows,Blockers=blockers,Plan=plan,Aprons=aprons,Paving=ParkingSurfaceGeometry.Paving(plan,true)}));
  // Union must cover the entire court without gaps or duplicated area.
  var rects=new[]{new Parcel{X=0,Y=0,W=20,D=10},new Parcel{X=10,Y=5,W=20,D=10}};
  var union=ParkingSurfaceGeometry.Union(rects).ToArray();
  if(Math.Abs(union.Sum(r=>r.Area)-350)>.001)throw new Exception("paving union area");
  for(int i=0;i<union.Length;i++)for(int j=i+1;j<union.Length;j++)if(Layout.Overlap(union[i],union[j]))throw new Exception("overlapping union");
  var surfacePlan=new Plan();surfacePlan.Parcels.AddRange(rects.Select(r=>new Parcel{Kind=8,X=r.X,Y=r.Y,W=r.W,D=r.D}));
  surfacePlan.Parcels.Add(new Parcel{Kind=5,X=10,Y=5,W=2,D=2});
  if(Math.Abs(ParkingSurfaceGeometry.Paving(surfacePlan,true).Sum(r=>r.Area)-346)>.001)throw new Exception("mask not preserved");
  Console.WriteLine("PASS focused native-width reclaim: 7 -> 4m, +28 bays; entry/parking connectivity, centre-line apron, paving union/mask");
 }
}
