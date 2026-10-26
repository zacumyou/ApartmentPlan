using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using ApartmentPlan;
static class RoadClearanceTests {
 static ConstructionCourse Line(Point a,Point b,string use,double width)=>new ConstructionCourse{Use=use,Width=width,Curve=new PlanBezier{A=a,B=a+(b-a)*(1d/3),C=a+(b-a)*(2d/3),D=b}};
 public static void Run(){
  var before=new List<ConstructionCourse>{Line(new Point(-25,0),new Point(25,0),"main",8),Line(new Point(0,20),new Point(0,0),"pedestrian",3)};
  var after=WalkRoadClearance.Apply(before);var end=after.Single(e=>e.Use=="pedestrian").Curve.D;
  if(Math.Abs(end.Y-5)>.00001)throw new Exception("path gap must be 1m outside 8m road");
  var crossing=WalkRoadClearance.Apply(new List<ConstructionCourse>{before[0],Line(new Point(0,-20),new Point(0,20),"pedestrian",3)});
  if(crossing.Count(e=>e.Use=="pedestrian")!=2)throw new Exception("road crossing must split outside ribbon");
  var p=Layout.Frame(new[]{new Point(0,0),new Point(100,0),new Point(100,100),new Point(0,100)},new Point(50,0));
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var solver=(ResidentialLayout)Activator.CreateInstance(typeof(ResidentialLayout),flags,null,new object[]{p,new LayoutOptions{Road=8},new List<Asset>(),null},null);
  object Call(string name,params object[] args)=>typeof(ResidentialLayout).GetMethod(name,flags).Invoke(solver,args);
  p.Routes.Clear();p.ConstructionRoutes.Clear();
  Call("DrawRoute",new List<Point>{new Point(-40,50),new Point(40,50)},8d,"main");
  Call("DrawRoute",new List<Point>{new Point(0,50),new Point(0,85)},8d,"main");
  Call("ProtectMainRoad");
  var field=typeof(ResidentialLayout).GetField("mainJunctionBuffer",flags);var mask=field.GetValue(solver);
  double x0=(double)typeof(ResidentialLayout).GetField("x0",flags).GetValue(solver),y0=(double)typeof(ResidentialLayout).GetField("y0",flags).GetValue(solver),cell=(double)typeof(ResidentialLayout).GetField("cell",flags).GetValue(solver);
  Func<int,int,bool> target=(x,y)=>Math.Abs(y0+(y+.5)*cell-50)<.6;
  field.SetValue(solver,null);var old=(List<Point>)Call("FindRoute",new Point(0,25),target,7d,false,80d);
  field.SetValue(solver,mask);var revised=(List<Point>)Call("FindRoute",new Point(0,25),target,7d,false,80d);
  if(old==null||revised==null||Math.Abs(revised.Last().X)<10)throw new Exception("junction approach not moved");
  var curved=new Plan{Origin=new Point(0,0),Right=new Point(1,0),Forward=new Point(0,1)};
  var bend=new PlanBezier{A=new Point(0,0),B=new Point(5,0),C=new Point(10,5),D=new Point(10,10)};
  curved.Routes.Add(new PlanRoute{Use="main",Width=8,Points=Enumerable.Range(0,17).Select(i=>bend.At(i/16d)).ToList(),Curves=new List<PlanBezier>{bend}});
  if(!MainJunctionClearance.Zones(curved).Any(z=>(z.Center-bend.At(.5)).Length<.01&&z.Radius>=10))throw new Exception("short curve buffer missing");
  curved.Routes.Add(new PlanRoute{Use="pedestrian",Width=3,Points=new List<Point>{bend.At(.5)+new Point(0,20),bend.At(.5)}});
  if(ConstructionCurves.Create(curved).Count(e=>e.Use=="main")!=1)throw new Exception("path split main Bezier");
  File.WriteAllText("ApartmentPlan/docs/road-clearance.json",JsonConvert.SerializeObject(new{Before=before,After=after,Plan=p,JunctionBefore=old,JunctionAfter=revised,Zones=MainJunctionClearance.Zones(p)}));
  Console.WriteLine("PASS: 1m pedestrian gap; crossing split; 7m vehicle approach moved from junction X="+old.Last().X+" to "+revised.Last().X+"m");
 }
}
