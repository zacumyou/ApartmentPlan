using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using ApartmentPlan;
static class VillaTests {
 public static void ExistingRoads(){
  var boundary=new[]{new Point(0,0),new Point(200,0),new Point(200,160),new Point(0,160)};
  var assets=new[]{new Asset{Id="CSKR test",Width=16,Depth=16,Homes=6,Height=9}};
  PlanRoute Road(Point a,Point b,double width)=>new PlanRoute{Use="aisle",Width=width,Points=new List<Point>{a,b},Curves=new List<PlanBezier>{new PlanBezier{A=a,B=a+(b-a)*(1d/3),C=a+(b-a)*(2d/3),D=b}}};
  var existing=new List<PlanRoute>{Road(new Point(-20,40),new Point(220,40),8),Road(new Point(100,-20),new Point(100,180),12),Road(new Point(-20,120),new Point(220,120),8)};
  // Use the same sampling as the native snapshot, including curves across the boundary.
  foreach(var r in existing)r.Points=VillaGeometry.Sample(r.Curves[0],2);
  var o=new LayoutOptions{Villa=true,VillaExistingRoads=existing,ParkingAisle=8,Road=16};
  var p=Layout.Generate(boundary,new Point[0],assets,o,null,null);
  if(!p.VillaExistingRoadsOnly||p.Parcels.Count(r=>r.Kind==0)<30||p.RequiredAccessFailures!=0)throw new Exception("existing road placement failed: "+p.Diagnostics+" buildings="+p.Parcels.Count+" failures="+p.RequiredAccessFailures);
  if(ConstructionCurves.Create(p).Count!=0||p.Parcels.Any(r=>r.Kind!=0))throw new Exception("existing roads submitted for construction");
  if(p.VillaRoadDensity<=0)throw new Exception("missing density");
  var restored=JsonConvert.DeserializeObject<Plan>(JsonConvert.SerializeObject(p));if(ConstructionCurves.Create(restored).Count!=0)throw new Exception("reference-only flag lost on save");
  File.WriteAllText("docs/villa-existing-033.json",JsonConvert.SerializeObject(p));
  Console.WriteLine("PASS existing roads: buildings="+p.Parcels.Count+" newRoads=0 density="+p.VillaRoadDensity);
  var gates=new List<VillaConnection>{new VillaConnection{Boundary=new Point(100,0),Road=new Point(100,-8),Tangent=new Point(1,0),Manual=true}};
  o=new LayoutOptions{Villa=true,VillaCentralRoad=false,VillaConnections=gates,ParkingAisle=8,Road=16};
  var alley=Layout.Generate(boundary,new[]{gates[0].Boundary},assets,o,null,null);
  if(alley.RequiredAccessFailures!=0||!alley.Parcels.Any(r=>r.Kind==0)||ConstructionCurves.Create(alley).Any(c=>c.Use=="main"))throw new Exception("alley-only failed "+alley.Diagnostics+" failures="+alley.RequiredAccessFailures);
  File.WriteAllText("docs/villa-alley-only-033.json",JsonConvert.SerializeObject(alley));
  Console.WriteLine("PASS alley only: buildings="+alley.Parcels.Count(r=>r.Kind==0)+" mainRoads=0");
  o.VillaExistingRoads=new List<PlanRoute>{Road(new Point(-20,-8),new Point(220,-8),8)};
  var outer=Layout.Generate(boundary,new[]{gates[0].Boundary},assets,o,null,null);if(outer.VillaExistingRoadsOnly)throw new Exception("external road mistaken for interior");
  Console.WriteLine("PASS external road excluded");
  o.VillaCentralRoad=true;o.VillaExistingRoads.Clear();
  var single=Layout.Generate(boundary,new[]{gates[0].Boundary},assets,o,null,null);
  if(single.RequiredAccessFailures!=0||single.VillaThrough||!single.Parcels.Any(r=>r.Kind==0))throw new Exception("single entrance fallback blocked");
  Console.WriteLine("PASS single entrance without through road");
  var bend=new PlanBezier{A=new Point(-20,70),B=new Point(60,70),C=new Point(140,110),D=new Point(220,110)};
  o.VillaExistingRoads=new List<PlanRoute>{new PlanRoute{Use="aisle",Width=8,Curves=new List<PlanBezier>{bend},Points=VillaGeometry.Sample(bend,2)}};
  var curved=Layout.Generate(boundary,new Point[0],assets,o,null,null);
  if(!curved.VillaExistingRoadsOnly||curved.Parcels.Count<10||curved.RequiredAccessFailures!=0||ConstructionCurves.Create(curved).Count!=0)throw new Exception("curved existing road failed");
  Console.WriteLine("PASS existing curved road: buildings="+curved.Parcels.Count);

 }
 public static void Sketch(){
  Point P(double x,double y)=>new Point((x-137)*(360d/325),(619-y)*(360d/325));
  var boundary=new[]{P(137,619),P(462,619),P(480,568),P(509,517),P(545,475),P(579,443),P(290,121),P(181,207),P(160,235),P(146,270),P(138,310)};
  var gates=new List<VillaConnection>{
   new VillaConnection{Boundary=P(295,127),Road=P(289,101),Junction=true},
   new VillaConnection{Boundary=P(573,448),Road=P(596,445),Junction=true},
   new VillaConnection{Boundary=P(142,619),Road=P(123,633),Junction=true},
   new VillaConnection{Boundary=P(458,619),Road=P(473,633),Junction=true},
   new VillaConnection{Boundary=P(137,420),Road=P(123,420)},
   new VillaConnection{Boundary=P(300,619),Road=P(300,633)},
   new VillaConnection{Boundary=P(430,277),Road=P(442,277)},
   new VillaConnection{Boundary=P(480,568),Road=P(492,578),Tangent=new Point(29,52)}};
  foreach(var gate in gates){gate.Tangent=new Point(1,0);if(gate.Junction)gate.Arms=new List<Point>{new Point(1,0),new Point(-1,0),new Point(0,1),new Point(0,-1)};}
  gates[0].Arms=new List<Point>{new Point(307,-344),new Point(-307,344),new Point(121,99),new Point(-121,-99)};
  gates[1].Arms=new List<Point>{new Point(-307,344),new Point(307,-344),new Point(88,57),new Point(-41,-38)};
  gates[3].Arms=new List<Point>{new Point(-1,0),new Point(0,-1),new Point(19,55)};
  gates[7].Tangent=new Point(29,52);gates[4].Tangent=new Point(0,1);gates[6].Tangent=VillaGeometry.Unit(new Point(289,-322));
  var sides=VillaGeometry.BoundarySides(boundary);var middle=new List<VillaConnection>();
  foreach(var side in sides){var q=VillaGeometry.ChainMidpoint(side);int k=Enumerable.Range(1,side.Count-1).OrderBy(i=>(VillaGeometry.Nearest(q,side[i-1],side[i])-q).Length).First();var tangent=VillaGeometry.Unit(side[k]-side[k-1]);var outward=new Point(tangent.Y,-tangent.X);middle.Add(new VillaConnection{Boundary=q,Road=q+outward*14,Tangent=tangent});}
  gates.RemoveRange(4,gates.Count-4);gates.AddRange(middle);
  var assets=new[]{new Asset{Id="CSKR test 2x2",Width=16,Depth=16,GeometryWidth=11,GeometryDepth=12,Height=9,Homes=6}};
  var options=new LayoutOptions{Villa=true,VillaMesh=true,VillaConnections=gates,ParkingAisle=8,Road=16};
  var plan=Layout.Generate(boundary,new[]{gates[0].Boundary},assets,options,null,null);
  var courses=ConstructionCurves.Create(plan);
  if(Math.Abs((boundary[1]-boundary[0]).Length-360)>.001)throw new Exception("sketch bottom edge is not 360m");
  if(plan.VillaRoots.Count(root=>courses.Any(c=>c.Use=="main"&&((c.Curve.A-root).Length<.1||(c.Curve.D-root).Length<.1)))<4)throw new Exception("two arterial axes missing");
  if(!plan.VillaThrough||plan.VillaEmptyBlocks!=0||VillaRoadRules.EmptyBlocks(plan,ConstructionGraph.Create(plan)).Count!=0)throw new Exception("through road / empty block invariant failed");
  foreach(var gate in gates){var attached=courses.Where(e=>(e.Curve.A-gate.Road).Length<.1||(e.Curve.D-gate.Road).Length<.1).ToArray();
   if(gate.Junction&&attached.Length!=0)throw new Exception("blocked intersection received another branch");
   foreach(var e in attached){var direction=VillaGeometry.Unit((e.Curve.A-gate.Road).Length<.1?e.Curve.Tangent(0):e.Curve.Tangent(1)*-1);if(!VillaGeometry.Approaches(gate).Any(a=>SiteFrame.Dot(a,direction)>.99999))throw new Exception("diagonal external approach");}
  }
  if(!courses.Any(e=>e.Use=="main"&&e.Curved))throw new Exception("arterial Bezier lost");
  if(!courses.Any(e=>e.Curved))throw new Exception("curved roads not preserved into native courses");
  if(!plan.Parcels.Any(r=>r.Kind==0&&r.PlacementForward.HasValue&&Math.Abs(r.PlacementForward.Value.X)>.1&&Math.Abs(r.PlacementForward.Value.Y)>.1))throw new Exception("houses not rotated with contour");
  var details=new List<string>();int missing=FrontageNetwork.Missing(plan,assets,8,1,details);if(missing!=0)throw new Exception("sketch frontage failed");
  var folder=Directory.Exists("docs")?"docs":"ApartmentPlan/docs";
  File.WriteAllText(folder+"/villa-user-sketch.json",JsonConvert.SerializeObject(new{plan,gates,options,missing,details,courses,emptyBlocks=VillaRoadRules.EmptyBlocks(plan,ConstructionGraph.Create(plan))}));
  Console.WriteLine("buildings="+plan.Parcels.Count(p=>p.Kind==0)+" area="+plan.Area+" missingFrontage="+missing+" accessFailures="+plan.RequiredAccessFailures+" "+plan.Diagnostics);
 }
 public static void Run(){
  var boundary=new[]{new Point(0,0),new Point(220,0),new Point(220,280),new Point(0,280)};
  var assets=new[]{new Asset{Id="CSKR test 2x2",Width=16,Depth=16,GeometryWidth=11,GeometryDepth=12,Height=9,Homes=6}};
  var gates=new List<VillaConnection>{new VillaConnection{Tangent=new Point(1,0),Arms=new List<Point>{new Point(1,0),new Point(-1,0),new Point(0,-1)},Boundary=new Point(55,0),Road=new Point(55,-8),Junction=true},new VillaConnection{Tangent=new Point(1,0),Boundary=new Point(165,280),Road=new Point(165,288)},new VillaConnection{Boundary=new Point(220,140),Road=new Point(228,140),Disabled=true}};
  Plan Make(bool mesh)=>Layout.Generate(boundary,new[]{gates[0].Boundary},assets,new LayoutOptions{Villa=true,VillaMesh=mesh,VillaConnections=gates,ParkingAisle=8,Road=16},null,null);
  var before=Make(false);var after=Make(true);
  foreach(var p in new[]{before,after}){
   var lots=p.Parcels.Where(r=>r.Kind==0).ToArray();if(lots.Length<10||p.RequiredAccessFailures>0)throw new Exception("villa generation/access failed: "+p.Diagnostics+" through="+p.VillaThrough+" empty="+p.VillaEmptyBlocks);
   for(int i=0;i<lots.Length;i++)for(int j=0;j<i;j++)if(VillaGeometry.Overlap(lots[i].PlacementOutline,lots[j].PlacementOutline))throw new Exception("compact envelopes overlap");
   var details=new List<string>();int missing=FrontageNetwork.Missing(p,assets,8,1,details);if(missing>0)Console.WriteLine(string.Join("\n",details));if(missing>0)throw new Exception("villa frontage missing "+missing);
   if(ConstructionGraph.Create(p).Any(e=>(e.A-gates[2].Road).Length<.1||(e.B-gates[2].Road).Length<.1))throw new Exception("excluded connection built");
  }
  if(after.Parcels.Count(r=>r.Kind==0)<=before.Parcels.Count(r=>r.Kind==0))throw new Exception("random spacing did not increase density");
  // Concave boundary, unavailable geometry and all connections excluded.
  var concave=new[]{new Point(0,0),new Point(220,0),new Point(220,180),new Point(120,180),new Point(120,280),new Point(0,280)};
  var c=Layout.Generate(concave,new[]{gates[0].Boundary},assets,new LayoutOptions{Villa=true,VillaConnections=new List<VillaConnection>{gates[0]},ParkingAisle=8,Road=16},null,null);
  if(c.VillaThrough||c.RequiredAccessFailures==0)throw new Exception("single external direction accepted as through route");
  if(c.Parcels.Where(r=>r.Kind==0).Any(r=>!VillaGeometry.Inside(r.PlacementOutline,c.Boundary.Select(c.Local).ToList())))throw new Exception("concave lot escape");
  var none=Layout.Generate(boundary,new[]{gates[0].Boundary},assets,new LayoutOptions{Villa=true,VillaConnections=new List<VillaConnection>()},null,null);
  if(none.RequiredAccessFailures==0||none.Parcels.Any(r=>r.Kind==0))throw new Exception("no approved connection accepted");
  var options=new LayoutOptions();
  if(VillaLayout.Span(16,0,options)!=VillaLayout.Span(16,100,options))throw new Exception("geometry still affects spacing");
  var spans=Enumerable.Range(0,100).Select(i=>VillaLayout.Spacing(16,new Point(i,7),0,options)).ToArray();
  if(spans.Any(v=>v<13||v>14)||spans.Distinct().Count()<90)throw new Exception("random compression range");
  if(VillaLayout.Spacing(16,new Point(1,7),0,options)!=VillaLayout.Spacing(16,new Point(1,7),0,options))throw new Exception("unstable spacing");
  if(VillaLayout.Candidate(new Asset{Id="CSKR Rural",Width=16,Depth=16,Homes=6})||!VillaLayout.Excluded(new Asset{Id="CS KR",Name="rUrAl"}))throw new Exception("Rural filter failed");
  var data=new PlanEditData{Boundary=boundary.ToList(),Entrances=new List<Point>{gates[0].Boundary},Draft=after,VillaConnections=gates,Settings=new Dictionary<string,object>{{"PlanMode",1}}};var restored=PlanEditCodec.Decode(PlanEditCodec.Encode(data));
  if(!restored.Draft.Villa||!restored.VillaConnections[2].Disabled||!restored.Draft.Parcels.First(r=>r.Kind==0).PlacementForward.HasValue)throw new Exception("villa save roundtrip");
  var folder=Directory.Exists("docs")?"docs":"ApartmentPlan/docs";File.WriteAllText(folder+"/villa-comparison.json",JsonConvert.SerializeObject(new{before,after,gates}));
  Console.WriteLine("PASS villa spacing, frontage, excluded connection, save roundtrip: "+before.Parcels.Count(r=>r.Kind==0)+" -> "+after.Parcels.Count(r=>r.Kind==0)+" buildings");
 }
}
