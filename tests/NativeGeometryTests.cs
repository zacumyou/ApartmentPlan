using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ApartmentPlan;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
static class NativeGeometryTests {
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 public static void Run(){
  var p=new Plan{Right=new Point(1,0),Forward=new Point(0,1)};
  var curve=new PlanBezier{A=new Point(0,0),B=new Point(20,0),C=new Point(40,20),D=new Point(40,40)};
  var r=new PlanRoute{Use="main",Width=8};r.Curves.Add(curve);for(int i=0;i<=40;i++)r.Points.Add(curve.At(i/40.0));p.Routes.Add(r);
  Check(ConstructionCurves.Create(p).Count==1,"free curve fragmented");
  var branch=curve.At(.5);p.Routes.Add(new PlanRoute{Use="aisle",Width=4,Points=new List<Point>{branch,branch+new Point(0,-20)}});
  var courses=ConstructionCurves.Create(p);Check(courses.Count==3&&courses.Count(c=>c.Curved)==2,"T junction not preserved");
  Check(courses.Count(c=>(c.Curve.A-branch).Length<.01||(c.Curve.D-branch).Length<.01)==3,"Bezier branch endpoints disconnected");
  foreach(var c in courses.Where(c=>c.Curved))for(int i=0;i<=10;i++)Check((curve.At(curve.Parameter(c.Curve.At(i/10.0)))-c.Curve.At(i/10.0)).Length<.001,"split curve changed shape");
  var patch=new Parcel{Kind=8,X=0,Y=0,W=20,D=10};var outline=SurfaceOutline.Create(patch,new List<Parcel>{patch},2);
  Check(outline.Count==20&&outline.All(v=>v.X>=-.001&&v.X<=20.001&&v.Y>=-.001&&v.Y<=10.001),"rounded corner chords");
  Check(Math.Abs(Layout.SignedArea(outline))<200&&Layout.SignedArea(outline)>190,"rounded area or winding");
  var adjacent=new Parcel{Kind=1,X=20,Y=0,W=8,D=10};outline=SurfaceOutline.Create(patch,new List<Parcel>{patch,adjacent},2);
  Check(outline.Any(v=>(v-new Point(20,0)).Length<.001)&&outline.Any(v=>(v-new Point(20,10)).Length<.001),"road contact was rounded");
  var fixture=JObject.Parse(File.ReadAllText("ApartmentPlan/docs/enhancement-compact.json"));var plan=fixture["Plan"].ToObject<Plan>();var generated=ConstructionCurves.Create(plan);var graph=ConstructionGraph.Create(plan,false);
  Check(generated.Any(c=>c.Curved)&&generated.Count<graph.Count,"production curve not consolidated");
  var nodes=new List<Point>();foreach(var c in generated)foreach(var end in new[]{c.Curve.A,c.Curve.D})if(!nodes.Any(v=>(v-end).Length<.03))nodes.Add(end);
  var reached=new HashSet<int>{0};bool more=true;while(more){more=false;foreach(var c in generated){int a=nodes.FindIndex(v=>(v-c.Curve.A).Length<.03),b=nodes.FindIndex(v=>(v-c.Curve.D).Length<.03);if(reached.Contains(a))more|=reached.Add(b);if(reached.Contains(b))more|=reached.Add(a);}}
  Check(reached.Count==nodes.Count,"production curved graph disconnected");
  File.WriteAllText("ApartmentPlan/docs/native-geometry.json",JsonConvert.SerializeObject(new{Plan=plan,Before=graph,Courses=generated,Surfaces=plan.Parcels.Where(q=>q.Kind==0||q.Kind==8||q.Kind==2||q.Kind==1&&q.InvisibleRoad).Select(q=>new{Kind=q.Kind,Outline=SurfaceOutline.Create(q,plan.Parcels,2)}),Buildings=plan.Parcels.Count(q=>q.Kind==0),Parking=plan.Parcels.Sum(q=>q.ParkingSpaces)}));
  Console.WriteLine("PASS native geometry: isolated cubic 1 course, branched cubic 2 + branch, exact shape, connected production graph "+graph.Count+" -> "+generated.Count+" courses, rounded four-chord corners/contact preservation");
 }
}
