using System;
using System.Linq;
using System.Collections.Generic;
using ApartmentPlan;
static class ConstructionTests {
 internal static void NativeAccess(){
  var runtimeAsset=new Asset{Id="CS Doan Xi Apt 30",Width=40,Depth=32,Height=111,Homes=144,PedestrianAccess=new List<Point>{new Point(-3.48265839,-14.8937654),new Point(14.417119,-14.7238178),new Point(-2.70267868,14.6536751),new Point(14.5901165,14.7767353)}};
  var runtimeSite=new[]{new Point(-628.0688,-106.1712),new Point(-526.7653,-149.1722),new Point(-574.6811,-269.1866),new Point(-675.326,-235.1589)};
  var runtime=Layout.Generate(runtimeSite,new[]{new Point(-665.0499,-208.6865)},new[]{runtimeAsset},new LayoutOptions{Road=8,AccessRoad=8,Walkway=3,ParkingAisle=8,ParkingPavingMargin=0,MainRoadPedestrian=true,AisleRoadPedestrian=true,BayWidth=3.70000004768372,BayDepth=6.19999980926514,MainCurveLengthRatio=.6},null,null);
  Check(runtime.Parcels.Count(p=>p.Kind==0)==2,"observed site retains both Doan lots with actual decal dimensions");
  Check(runtime.RequiredAccessFailures==0,"all four actual Doan pedestrian access points per lot have clear routes");
  Check(runtime.ParkingAccessFailures==0,"entrance openings preserve vehicle access to all remaining parking bays");
  foreach(var lot in runtime.Parcels.Where(p=>p.Kind==0)){
   var forward=BuildingPlacement.Forward(lot,runtimeAsset);var front=new Point(lot.X+lot.W/2,lot.Y+lot.D/2)+forward*(runtimeAsset.Depth/2);
   Check(!runtime.Routes.Where(r=>r.Use=="pedestrian").Any(r=>r.Points.Skip(1).Select((v,i)=>new[]{r.Points[i],v}).Any(s=>Math.Abs(SiteFrame.Dot(s[1]-s[0],forward))<.01&&(s[1]-s[0]).Length>runtimeAsset.Width/2&&Math.Abs(SiteFrame.Dot((s[0]+s[1])*.5-front,forward))<3)),"no continuous path along the Doan frontage");
  }
  foreach(var route in runtime.Routes.Where(r=>r.Use=="pedestrian"))for(int i=1;i<route.Points.Count;i++){
   var a=route.Points[i-1];var b=route.Points[i];var ribbon=new Parcel{X=Math.Min(a.X,b.X)-route.Width/2,Y=Math.Min(a.Y,b.Y)-route.Width/2,W=Math.Abs(a.X-b.X)+route.Width,D=Math.Abs(a.Y-b.Y)+route.Width};
   if(runtime.Parcels.Any(p=>(p.Kind==0||p.Kind==2)&&Layout.Overlap(p,ribbon)))throw new Exception("pedestrian crossing overlaps building or parking bay");
  }
  Check(true,"local pedestrian crossings do not overlap lots or remaining parking rows");
  Check(runtime.Parcels.All(p=>p.Kind<=10),"internal reserved landscape marker never enters saved plan parcels");
  var noDoors=new Asset{Id=runtimeAsset.Id,Width=40,Depth=32,Height=111,Homes=144};
  Plan WithoutPaths(double width)=>Layout.Generate(runtimeSite,new[]{new Point(-665.0499,-208.6865)},new[]{noDoors},new LayoutOptions{Road=8,AccessRoad=8,Walkway=width,ParkingAisle=8,ParkingPavingMargin=0,BayWidth=3.7,BayDepth=6.2,PedestrianNetwork=false},null,null);
  string Footprints(Plan p)=>string.Join(";",p.Parcels.Where(r=>r.Kind==0||r.Kind==2).Select(r=>$"{r.Kind}:{r.X}:{r.Y}:{r.W}:{r.D}"));
  var narrow=WithoutPaths(3);var wide=WithoutPaths(8);
  Check(narrow.Parcels.Any(p=>p.Kind==0)&&Footprints(narrow)==Footprints(wide),"changing selected path width cannot insert a compulsory frontage strip");
  Check(!narrow.Parcels.Any(p=>p.Kind==8||p.Kind==9),"unused court clearance is landscape, not automatic pavement or path");
  System.IO.File.WriteAllText("ApartmentPlan/docs/runtime-access-fixture.json",System.Text.Json.JsonSerializer.Serialize(new{runtime,runtimeAsset},new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));
  var asset=new Asset{Id="Doan access fixture",Width=40,Depth=32,Height=222,Homes=144,PedestrianAccess=new List<Point>{new Point(-9,-16),new Point(9,-16)}};
  for(int k=0;k<4;k++){
   var f=new Point(Math.Sin(k*Math.PI/2),Math.Cos(k*Math.PI/2));var lot=new Parcel{W=k%2==0?40:32,D=k%2==0?32:40};var c=new Point(lot.W/2,lot.D/2);lot.Frontage=new Parcel{X=c.X+f.X*40,Y=c.Y+f.Y*40};
   Check((BuildingPlacement.Forward(lot,asset)-f).Length<.001,"native access shares lot rotation "+k);
   var target=BuildingPlacement.AccessTarget(lot,asset,new Point(0,-16),3.5);
   Check(SiteFrame.Dot(target-c,f)<-19,"rear door stays on rear edge "+k);
  }
  var site=new[]{new Point(-628.0688,-106.1712),new Point(-519.3495,-130.5979),new Point(-573.0862,-265.1919),new Point(-659.5193,-232.3885)};
  var plan=Layout.Generate(site,new[]{new Point(-653.8571,-209.2863)},new[]{asset},new LayoutOptions{Road=16,AccessRoad=8,Walkway=3,ParkingAisle=8,ParkingPavingMargin=0},null,null);
  Check(plan.Parcels.Count(p=>p.Kind==0)==2,"rear-access paths preserve both Doan lots");
  Check(plan.RequiredAccessFailures==0,"all native access targets reachable in repro "+plan.RequiredAccessFailures);
  var graph=ConstructionGraph.Create(plan);
  foreach(var lot in plan.Parcels.Where(p=>p.Kind==0))foreach(var door in asset.PedestrianAccess){
   var target=plan.World(BuildingPlacement.AccessTarget(lot,asset,door,3.5));
   Check(graph.Where(e=>e.Use=="pedestrian").Any(e=>Distance(target,e)<2),"path reaches each actual rear access target");
  }
  Check(graph.All(e=>(e.B-e.A).Length>=1.99),"sub-cell path junction spans removed without crossing lots");
  var options=new LayoutOptions{Road=16,AccessRoad=8,Walkway=3,ParkingAisle=8,ParkingPavingMargin=0,MainRoadPedestrian=true,AisleRoadPedestrian=true};
  var sidewalks=Layout.Generate(site,new[]{new Point(-653.8571,-209.2863)},new[]{asset},options,null,null);
  Check(sidewalks.RequiredAccessFailures==0,"road sidewalks serve access without requiring a standalone path to entrances");
  Check(sidewalks.Parcels.Count(p=>p.Kind==0)==2,"sidewalk access preserves Doan lots");
  double Length(Plan p)=>p.Routes.Where(r=>r.Use=="pedestrian").Sum(r=>r.Points.Skip(1).Select((v,i)=>(v-r.Points[i]).Length).Sum());
  Check(Length(sidewalks)<Length(plan),"walkable roads reduce separate path length: "+Length(sidewalks)+" vs "+Length(plan));
  foreach(var lot in sidewalks.Parcels.Where(p=>p.Kind==0))foreach(var door in asset.PedestrianAccess){
   var target=BuildingPlacement.AccessTarget(lot,asset,door,3.5);
   Check(sidewalks.ConstructionRoutes.Concat(sidewalks.Routes).Any(r=>PedestrianRoadAccess.Supports(r,options)&&PedestrianRoadAccess.Covers(r,target,options))||ConstructionGraph.Create(sidewalks).Where(e=>e.Use=="pedestrian").Any(e=>Distance(sidewalks.World(target),e)<2),"each rear access still reaches a sidewalk or separate path");
  }
  var road=new PlanRoute{Use="aisle",Width=8,Points=new List<Point>{new Point(0,0),new Point(40,0)}};
  Check(PedestrianRoadAccess.Covers(road,new Point(20,4),options)&&!PedestrianRoadAccess.Covers(road,new Point(20,4.1),options)&&!PedestrianRoadAccess.Covers(road,new Point(41,0),options),"road access obeys actual width and endpoints");
  road.Width=20;
  Check(!PedestrianRoadAccess.Covers(road,new Point(20,6),options),"court paving margin does not enlarge the selected native sidewalk width");
  Check(!PedestrianRoadAccess.Supports(road,new LayoutOptions{MainRoadPedestrian=true})&&PedestrianRoadAccess.Supports(road,options),"car-only aisle cannot inherit main-road sidewalk capability");
  System.IO.File.WriteAllText("ApartmentPlan/docs/native-access-fixture.json",System.Text.Json.JsonSerializer.Serialize(new{plan,graph},new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));
 }
 static double Distance(Point p,ConstructionEdge e){var d=e.B-e.A;double t=Math.Clamp(SiteFrame.Dot(p-e.A,d)/SiteFrame.Dot(d,d),0,1);return (p-e.A-d*t).Length;}
 internal static void DoanRepro(){
  var site=new[]{new Point(-628.0688,-106.1712),new Point(-519.3495,-130.5979),new Point(-573.0862,-265.1919),new Point(-659.5193,-232.3885)};
  var plan=Layout.Generate(site,new[]{new Point(-653.8571,-209.2863)},new[]{new Asset{Id="Doan",Width=40,Depth=32,Height=222,Homes=144}},new LayoutOptions{Road=16,AccessRoad=7,Walkway=3,ParkingAisle=7},null,null);
  var graph=ConstructionGraph.Create(plan);
  Check(plan.Parcels.Count(p=>p.Kind==0)==2,"observed Doan site fits two actual-size lots");
  Check(graph.All(e=>(e.B-e.A).Length>=3),"observed short native junction spans removed");
  Console.WriteLine("buildings="+plan.Parcels.Count(p=>p.Kind==0)+" graph="+graph.Count);
  foreach(var e in graph)Console.WriteLine(e.Use+" "+(e.B-e.A).Length.ToString("F2")+" "+plan.Local(e.A).X.ToString("F2")+","+plan.Local(e.A).Y.ToString("F2")+" -> "+plan.Local(e.B).X.ToString("F2")+","+plan.Local(e.B).Y.ToString("F2"));
  System.IO.File.WriteAllText("ApartmentPlan/docs/doan-repro.json",System.Text.Json.JsonSerializer.Serialize(new{plan,graph},new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));
 }
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
 internal static void VillaNodeSimplify(){
  var c=new PlanBezier{A=new Point(0,0),B=new Point(15,0),C=new Point(30,30),D=new Point(45,30)};
  var courses=Enumerable.Range(0,20).Select(i=>new ConstructionCourse{Use="aisle",Width=8,Curved=true,Curve=c.Slice(i/20d,(i+1)/20d)}).ToList();
  var join=c.At(.5);courses.Add(new ConstructionCourse{Use="main",Width=16,Curve=TowerGeometry.Line(join,join+new Point(0,30))});
  System.IO.File.WriteAllText("docs/villa-nodes-before-032.json",Newtonsoft.Json.JsonConvert.SerializeObject(courses));
  ConstructionCurves.SimplifyVillaNodes(courses);
  Check(courses.Count==3,"20 sampled curve pieces become two curves plus branch");
  Check(courses.Count(q=>(q.Curve.A-join).Length<.001||(q.Curve.D-join).Length<.001)==3,"real branch node retained");
  System.IO.File.WriteAllText("docs/villa-nodes-after-032.json",Newtonsoft.Json.JsonConvert.SerializeObject(courses));
 }
 internal static void VillaJunction(){
  var p=new Plan{Villa=true,Right=new Point(1,0),Forward=new Point(0,1)};
  p.Routes.Add(TowerGeometry.Route(new[]{TowerGeometry.Line(new Point(-40,0),new Point(40,0))},"main",16));
  p.Routes.Add(TowerGeometry.Route(new[]{TowerGeometry.Line(new Point(0,-40),new Point(0,40))},"aisle",8));
  var courses=ConstructionCurves.Create(p);
  System.IO.File.WriteAllText("docs/villa-junction-output.json",Newtonsoft.Json.JsonConvert.SerializeObject(courses));
  Check(courses.Count(c=>c.Curve.A.Length<.001||c.Curve.D.Length<.001)==4,"both authored roads split into shared four-way junction");
  foreach(bool reverse in new[]{false,true})foreach(bool tee in new[]{false,true}){
   var bend=new PlanBezier{A=new Point(0,-40),B=new Point(-18,-10),C=new Point(18,10),D=new Point(0,40)};
   var join=bend.At(.37);var fixture=new Plan{Villa=true,Right=new Point(1,0),Forward=new Point(0,1)};
   fixture.Routes.Add(TowerGeometry.Route(new[]{bend},"main",16));
   fixture.Routes.Add(TowerGeometry.Route(new[]{TowerGeometry.Line(new Point(-40,join.Y),tee?join:new Point(40,join.Y))},"aisle",8));
   if(reverse)fixture.Routes.Reverse();var result=ConstructionCurves.Create(fixture);
   var ends=result.SelectMany(c=>new[]{c.Curve.A,c.Curve.D}).Where(q=>(q-join).Length<.1).ToArray();
   Check(ends.Length==(tee?3:4)&&ends.All(q=>(q-ends[0]).Length<.001),"curved shared "+(tee?"T":"X")+" junction reverse="+reverse);
   if(!reverse&&!tee)System.IO.File.WriteAllText("docs/villa-curved-junction-030.json",Newtonsoft.Json.JsonConvert.SerializeObject(result));
  }
 }
 internal static void Run(){
  var p=new Plan{Right=new Point(1,0),Forward=new Point(0,1)};
  p.Routes.Add(new PlanRoute{Use="main",Points=new List<Point>{new Point(0,0),new Point(40,0)}});
  p.Routes.Add(new PlanRoute{Use="access",Points=new List<Point>{new Point(20,0),new Point(20,20)}});
  p.ConstructionRoutes.Add(new PlanRoute{Use="aisle",Points=new List<Point>{new Point(10,0),new Point(30,0)}});
  var g=ConstructionGraph.Create(p);
  Check(g.Count==3,"T junction retained and redundant straight splits removed");
  Check(g.Count(e=>(e.A-new Point(20,0)).Length<.01||(e.B-new Point(20,0)).Length<.01)==3,"shared T junction has three incident edges");
  Check(Math.Abs(g.Sum(e=>(e.B-e.A).Length)-60)<.01,"graph length conserved without duplicate spans");
  var site=new List<Point>{new Point(0,0),new Point(160,0),new Point(160,300),new Point(0,340)};
  var plan=Layout.Generate(site,new[]{new Point(60,0),new Point(105,313.75)},new[]{new Asset{Id="test",Width=32,Depth=16,Height=45,Homes=30}},new LayoutOptions{Road=16,AccessRoad=8,Walkway=4,ParkingAisle=8},null,null);
  g=ConstructionGraph.Create(plan);
  Check(plan.ConstructionRoutes.Any(r=>r.Use=="aisle"),"original court street centerlines retained");
  Check(g.All(e=>double.IsFinite(e.A.X)&&double.IsFinite(e.B.Y)&&(e.B-e.A).Length>.09),"native courses finite and nondegenerate");
  System.IO.File.WriteAllText("ApartmentPlan/docs/native-graph.json",System.Text.Json.JsonSerializer.Serialize(g,new System.Text.Json.JsonSerializerOptions{IncludeFields=true}));
  var roads=g.Where(e=>e.Use!="pedestrian").ToList();var visited=new HashSet<int>{0};bool changed=true;while(changed){changed=false;for(int i=0;i<roads.Count;i++)if(!visited.Contains(i)&&visited.Any(j=>new[]{roads[i].A,roads[i].B}.Any(a=>new[]{roads[j].A,roads[j].B}.Any(b=>(a-b).Length<.03)))){visited.Add(i);changed=true;}}
  Check(visited.Count==roads.Count,"all construction road axes share entrance component "+visited.Count+"/"+roads.Count);
  Check(g.Count<1500,"representative native graph within segment budget");
  var courses=ConstructionCurves.Create(plan).Where(c=>c.Use!="pedestrian").ToList();
  var reached=new HashSet<int>{0};bool grew=true;while(grew){grew=false;for(int i=0;i<courses.Count;i++)if(!reached.Contains(i)&&reached.Any(j=>new[]{courses[i].Curve.A,courses[i].Curve.D}.Any(a=>new[]{courses[j].Curve.A,courses[j].Curve.D}.Any(b=>(a-b).Length<.001)))){reached.Add(i);grew=true;}}
  Check(reached.Count==courses.Count,"restored native curves share exact junctions "+reached.Count+"/"+courses.Count);
  Console.WriteLine("Native graph checks only; game generation and traffic require runtime tests.");
 }
}
