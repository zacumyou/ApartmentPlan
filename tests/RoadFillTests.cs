using System;
using System.Linq;
using System.Collections.Generic;
using ApartmentPlan;
static class RoadFillTests {
 public static void Run(){
  var square=new List<RoadFill.Edge>{new RoadFill.Edge(new Point(-100,-100),new Point(100,-100),12),new RoadFill.Edge(new Point(100,-100),new Point(100,100),12),new RoadFill.Edge(new Point(100,100),new Point(-100,100),12),new RoadFill.Edge(new Point(-100,100),new Point(-100,-100),12)};
  var output=new List<object>();
  void Test(string name,List<RoadFill.Edge> edges,Point seed,double radius,bool expected){var p=RoadFill.Solve(seed,edges,radius);if((p.Count>=3)!=expected)throw new Exception(name+" count="+p.Count);if(expected&&p.Any(q=>Math.Abs(q.X)>100||Math.Abs(q.Y)>100))throw new Exception("outside");Console.WriteLine("PASS "+name+" vertices="+p.Count);output.Add(new{name,roads=edges,seed,radius,polygon=p});}
  Test("Closed",square,new Point(0,0),250,true);
  Test("Open U",square.Take(3).ToList(),new Point(0,0),250,true);
  var loose=square.Select(e=>new RoadFill.Edge(e.A+(e.B-e.A)*.08,e.B+(e.A-e.B)*.08,e.Width)).ToList();Test("Loose corners",loose,new Point(0,0),250,true);
  Test("No roads",new List<RoadFill.Edge>(),new Point(0,0),250,false);
  Test("Single road",square.Take(1).ToList(),new Point(0,0),250,false);
  Test("On road",square,new Point(100,0),250,false);
  Test("Too small radius",square,new Point(0,0),50,false);
  Test("Outside",square,new Point(170,0),250,false);
  System.IO.File.WriteAllText("docs/road-fill-038.json",Newtonsoft.Json.JsonConvert.SerializeObject(output));
 }
}
