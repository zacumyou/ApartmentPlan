using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
// Uncommitted input only: no layout, ECS or save mutations while placing points.
internal sealed class RoadSketch {
 internal readonly List<ManualRoad> Roads=new List<ManualRoad>();
 internal readonly List<List<Point>> Samples=new List<List<Point>>();
 internal readonly List<Point> Points=new List<Point>();
 internal bool Curved;
 internal void Clear(){Roads.Clear();Samples.Clear();Points.Clear();}
 internal void Shape(bool curved){Curved=curved;if(Points.Count>1)Points.RemoveRange(1,Points.Count-1);}
 internal void Add(Point p){
  if(Points.Count+1==(Curved?3:2)){
   var road=new ManualRoad{Curved=Curved,Points=Points.Concat(new[]{p}).ToList()};
   var samples=road.Sample(); // validate before mutating the sketch
   Roads.Add(road);Samples.Add(samples);Points.Clear();
  }
  Points.Add(p);
 }
 internal void Undo(){
  if(Points.Count>1){Points.RemoveAt(Points.Count-1);return;}
  if(Roads.Count>0){var last=Roads[Roads.Count-1];Roads.RemoveAt(Roads.Count-1);Samples.RemoveAt(Samples.Count-1);Points.Clear();Points.Add(last.Points[0]);}
  else Points.Clear();
 }
}
}
