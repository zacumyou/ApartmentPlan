using System;using System.Linq;using System.Collections.Generic;using System.IO;using ApartmentPlan;
static class PlanningObservationTests {
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static Plan Roads(params Point[][] lines){var p=new Plan{Villa=true,Right=new Point(1,0),Forward=new Point(0,1)};foreach(var line in lines)p.Routes.Add(new PlanRoute{Use="aisle",Width=24,Points=line.ToList()});return p;}
 public static void Run(){
  var parallel=Roads(new[]{new Point(0,0),new Point(60,0)},new[]{new Point(0,8),new Point(60,8)});var graph=ConstructionGraph.Create(parallel);Check(graph.Count==2,"parallel roads acquired sideways connectors");
  var approach=Roads(new[]{new Point(-40,0),new Point(40,0)},new[]{new Point(-15,-25),new Point(-3,-5)});var joined=ConstructionGraph.Create(approach);Check(joined.Count==3,"T junction not joined");Check(joined.Where(e=>e.A.Y!=0||e.B.Y!=0).All(e=>Math.Abs(Layout.Cross(e.B-e.A,new Point(12,20)))<.001),"approach kink");
  var far=Roads(new[]{new Point(-40,0),new Point(40,0)},new[]{new Point(0,-50),new Point(0,-30)});Check(ConstructionGraph.Create(far).Count==2,"long gap bridged");
  foreach(bool villa in new[]{false,true}){var o=new LayoutOptions{Villa=villa,Tower=!villa};try{Layout.Generate(new List<Point>(),new List<Point>(),new List<Asset>(),o,null,null);throw new Exception("expected failure");}catch(ArgumentException e){var d=e.Data["planning"] as Dictionary<string,int>;Check(d!=null&&d["planMode"]==(villa?1:2)&&d["outcome"]==2&&d["stage"]==1,"failure capture");Check(d.Keys.All(PlanningObservation.Allowed.Contains),"allowlist");}}
  var obs=new PlanningObservation(new LayoutOptions{Villa=true},4,1,new List<Asset>(),0,0);obs.Set("coordinates",123);obs.Stage("packing");obs.Result(parallel);var result=obs.Finish(1);Check(!result.ContainsKey("coordinates")&&result["outcome"]==1&&result["routes"]==2,"success capture");Check(PlanningObservation.Allowed.Count<=128,"bounded schema");
  File.WriteAllText("docs/road-joins-0124.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{parallel=graph,approach=joined}));Console.WriteLine("PASS parallel separation, tangent-preserving T join, long gap, villa/tower failure and bounded observation");
 }
}
