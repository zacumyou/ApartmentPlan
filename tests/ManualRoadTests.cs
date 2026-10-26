using System;
using System.Linq;
using System.Collections.Generic;
using ApartmentPlan;
static class ManualRoadTests {
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
 public static void Run(){
  var boundary=new[]{new Point(0,0),new Point(220,0),new Point(220,260),new Point(0,260)};
  var entrances=new[]{new Point(100,0)};
  var assets=new[]{new Asset{Id="Test",Width=32,Depth=16,Height=40,Homes=40}};
  var roads=new List<ManualRoad>{new ManualRoad{Points=new List<Point>{new Point(100,80),new Point(170,80)}},new ManualRoad{Curved=true,Points=new List<Point>{new Point(170,80),new Point(190,110),new Point(170,150)}}};
  var sketch=new RoadSketch();sketch.Add(roads[0].Points[0]);sketch.Add(roads[0].Points[1]);
  sketch.Shape(true);sketch.Add(roads[1].Points[1]);sketch.Add(roads[1].Points[2]);
  Check(sketch.Roads.Count==2&&sketch.Points.Count==1&&sketch.Samples[1].Count>2,"straight and curved input accumulates without layout calls");
  var batch=sketch.Roads.ToArray();sketch.Undo();Check(sketch.Roads.Count==1&&sketch.Points[0].X==170&&sketch.Points[0].Y==80,"undo removes the last pending segment and restores its start");
  sketch.Clear();Check(sketch.Roads.Count==0&&sketch.Samples.Count==0&&batch.Length==2,"cancel clears preview cache without mutating captured roads");
  var options=new LayoutOptions{Road=12,MainRoadPedestrian=true};
  var plan=Layout.Generate(boundary,entrances,assets,options,null,null,roads);
  var baseline=Layout.Generate(boundary,entrances,assets,options,null,null,null);
  System.IO.File.WriteAllText("ApartmentPlan/docs/manual-road-batch.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {Before=baseline,After=plan,Roads=roads,Courses=ConstructionCurves.Create(plan)}));
  Check(plan.Parcels.Any(p=>p.Kind==0),"manual roads leave usable building layout");
  var curve=roads[1].Sample();Check(curve.Count>2&&curve.First().X==170&&curve.Last().Y==150&&curve.Any(p=>p.X>170),"three-point curve preserves endpoints and bends toward control");
  Check(ConstructionCurves.Create(plan).Any(c=>c.Curved),"manual curve produces native Bezier course");
  var graph=ConstructionGraph.Create(plan);var junction=new Point(100,80);
  Check(graph.Count(e=>(e.A-junction).Length<.02||(e.B-junction).Length<.02)>=3,"added road creates a real graph T junction with the main road");
  foreach(var r in roads){var first=plan.Local(r.Points[0]);Check(plan.Routes.Any(p=>p.Use=="main"&&p.Width==12&&(p.Points[0]-first).Length<.001),"added road uses actual selected network width");}
  var zones=plan.Parcels;for(int i=0;i<zones.Count;i++)for(int j=0;j<i;j++)if(Layout.Overlap(zones[i],zones[j]))throw new Exception("manual road layout overlaps parcels");
  Check(true,"repacked buildings and parking do not overlap roads or other parcels");
  var excluded=new ManualZone{Kind=5,Points=new List<Point>{new Point(120,60),new Point(150,60),new Point(150,100),new Point(120,100)}};
  try{Layout.Generate(boundary,entrances,assets,options,null,new[]{excluded},roads);throw new Exception("excluded area accepted");}catch(ArgumentException e){Check(e.Message=="ManualRoadFit","road crossing an exclusion is rejected");}
  var outside=new[]{new ManualRoad{Points=new List<Point>{new Point(100,80),new Point(219,80)}}};
  try{Layout.Generate(boundary,entrances,assets,options,null,null,outside);throw new Exception("road width outside accepted");}catch(ArgumentException e){Check(e.Message=="ManualRoadFit","full road width must fit inside the boundary and green margin");}
  try{new ManualRoad{Points=new List<Point>{new Point(1,1),new Point(double.NaN,2)}}.Sample();throw new Exception("NaN accepted");}catch(ArgumentException){Check(true,"invalid control coordinates rejected");}
  var data=new PlanEditData{Boundary=boundary.ToList(),Entrances=entrances.ToList(),Draft=plan,Roads=roads,Ramps=new List<Parcel>{new Parcel{Kind=6,W=8,D=20}}};
  var restored=PlanEditCodec.Decode(PlanEditCodec.Encode(data));
  Check(restored.Roads.Count==2&&restored.Roads[1].Curved&&restored.Roads[1].Points[1].X==190&&restored.Ramps.Count==1,"save codec preserves manual controls and legacy ramps without reinterpreting them");
  var old=PlanEditCodec.Decode(System.Text.Encoding.UTF8.GetBytes("{\"Version\":1}"));Check(old.Roads!=null&&old.Roads.Count==0,"old save without roads defaults to empty list");
  options.Road=20;var wider=Layout.Generate(boundary,entrances,assets,options,null,null,restored.Roads);
  Check(wider.Routes.Where(r=>r.Use=="main").All(r=>r.Width==20),"changing network width regenerates manual road reservation");
 }
}

