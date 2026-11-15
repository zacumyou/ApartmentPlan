using System;using System.Linq;using System.Collections.Generic;using System.IO;using ApartmentPlan;
static class JunctionStubTests {
 static List<ConstructionEdge> Roads(double w,double len=6)=>new List<ConstructionEdge>{new ConstructionEdge{A=new Point(-40,0),B=new Point(0,0),Use="aisle",Width=w},new ConstructionEdge{A=new Point(0,0),B=new Point(40,0),Use="aisle",Width=w},new ConstructionEdge{A=new Point(0,0),B=new Point(0,len),Use="aisle",Width=w}};
 static Plan Plan()=>new Plan{Right=new Point(1,0),Forward=new Point(0,1)};
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 public static void Run(){var p=Plan();var before=Roads(24);var after=Roads(24);ConstructionGraph.RemoveCoveredJunctionStubs(p,after);Check(after.Count==1,"wide stub");
 var cross=Roads(24);cross.Add(new ConstructionEdge{A=new Point(0,-40),B=new Point(0,0),Use="aisle",Width=24});ConstructionGraph.RemoveCoveredJunctionStubs(p,cross);Check(cross.Count==3&&cross.Any(e=>e.A.Y==-40),"incoming branch lost");
 var narrow=Roads(8);ConstructionGraph.RemoveCoveredJunctionStubs(p,narrow);Check(narrow.Count==3,"narrow end");
 var longer=Roads(24,18);ConstructionGraph.RemoveCoveredJunctionStubs(p,longer);Check(longer.Count==3,"outside ribbon");
 var protectedPlan=Plan();protectedPlan.Parcels.Add(new Parcel{Kind=0,X=-5,Y=15,W=10,D=10,Access=new Point(0,12)});var access=Roads(24);ConstructionGraph.RemoveCoveredJunctionStubs(protectedPlan,access);Check(access.Count==3,"access");
 var branch=Roads(24);branch.Add(new ConstructionEdge{A=new Point(0,6),B=new Point(30,6),Use="aisle",Width=24});ConstructionGraph.RemoveCoveredJunctionStubs(p,branch);Check(branch.Count==4,"junctions");
 var mixed=Roads(24);mixed[0].Use="main";ConstructionGraph.RemoveCoveredJunctionStubs(p,mixed);Check(mixed.Count==3,"mixed role");
 var walk=Roads(24);walk.Add(new ConstructionEdge{A=new Point(0,6),B=new Point(0,30),Use="pedestrian",Width=2});ConstructionGraph.RemoveCoveredJunctionStubs(p,walk);Check(walk.Count==4,"walk");
 var integrated=Plan();foreach(var e in before)integrated.Routes.Add(new PlanRoute{Use=e.Use,Width=e.Width,Points=new List<Point>{e.A,e.B}});var generated=ConstructionCurves.Create(integrated);Check(generated.Count==1,"production");
 File.WriteAllText("docs/junction-stub-0123.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{before,after,narrow,generated}));Console.WriteLine("PASS width24 covered6m stub 3->1; narrow/long/access/junction/mixed/walk preserved; production passed");}
}
