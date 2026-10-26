using System;
using System.Linq;
using ApartmentPlan;
static class ShortCurveTests {
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
 public static void Run(){
  var site=new[]{new Point(0,0),new Point(400,0),new Point(400,600),new Point(0,600)};
  Plan Make(Point[] entrances,double scale)=>Layout.Generate(site,entrances,new Asset[0],new LayoutOptions{MainCurveRadius=80,MainCurveLengthRatio=scale,MainRoadCornerMode=1},null,null);
  var perpendicular=new[]{new Point(160,0),new Point(400,350)};
  var old=Make(perpendicular,1);var next=Make(perpendicular,.5);
  var oldR=old.Routes.SelectMany(r=>r.CornerRadii).ToArray();var nextR=next.Routes.SelectMany(r=>r.CornerRadii).ToArray();
  Check(oldR.Length>0&&oldR.Length==nextR.Length&&oldR.Zip(nextR,(a,b)=>Math.Abs(a*.5-b)<.001).All(x=>x),"same corner skeleton halves arc radii and arc lengths");
  var offset=new[]{new Point(160,0),new Point(162,600)};old=Make(offset,1);next=Make(offset,.5);
  double Transition(Plan p){var points=p.Routes.Where(r=>r.Use=="main"&&r.CornerRadii.Count>0).SelectMany(r=>r.Points).Select(p.World).ToArray();var moving=points.Where(q=>q.X>160.001&&q.X<161.999).ToArray();return moving.Max(q=>q.Y)-moving.Min(q=>q.Y);}
  Check(Math.Abs(Transition(next)/Transition(old)-.5)<.02,"shallow S transition occupies half the previous longitudinal span");
  Check(next.Routes.SelectMany(r=>r.Points).All(q=>Layout.Inside(next.World(q),site)),"shortened route remains inside site");
  Check(next.Routes.Any(r=>r.Use=="main"&&r.CornerRadii.Count>0),"offset entrances retain curved connected route");
  var codec=new Codec();new PlanConstructionState{Applied=1}.Serialize(codec);codec.Reset();var state=new PlanConstructionState();state.Deserialize(codec);Check(state.Applied==1,"placed marker persists independently of editable plan payload");
  CornerEditingTests.Run();
 }
}
