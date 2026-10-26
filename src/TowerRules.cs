using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
// Persisted independently of the slab/villa options. Missing keys keep approved defaults.
public sealed class TowerRules {
 public sealed class Rule {public string Id;public double Default,Min,Max,Step;public Rule(string id,double value,double min,double max,double step=1){Id=id;Default=value;Min=min;Max=max;Step=step;}}
 public static readonly Rule[] Definitions={
 new Rule("MaxBuildings",100,1,200),new Rule("FacilityWidth",20,8,64),new Rule("FacilityDepth",16,8,64),new Rule("Angle",45,0,180),new Rule("BuildingGap",20,4,60),new Rule("GapTolerance",2,0,4,.5),new Rule("BuildingBoundary",4,0,20,.5),new Rule("PackingStep",4,2,8),new Rule("BoundaryTurn",1,0,1),
 new Rule("EntranceInset",20,20,60),new Rule("RoadRadius",4.1,4,16,.1),new Rule("CurveGap",10,10,30),new Rule("FrontCurveGap",10,4,24),new Rule("ParallelGap",10,10,30),new Rule("BranchGap",16,16,40),new Rule("TurnaroundGap",4,4,12,.5),
 new Rule("WalkParallelGap",4,0,12,.5),new Rule("WalkRadius",2.1,2,8,.1),new Rule("WalkBoundary",4,0,20,.5),new Rule("BoundaryParallelRange",16,4,40),new Rule("RoadWalkGap",1,1,5,.5),new Rule("WalkApproachAngle",45,45,90,5),new Rule("ApproachExclusion",12,12,30),
 new Rule("CrossAreaPercent",200,100,500,10),new Rule("LoopBridgeGap",12,0,30),new Rule("CrossWalkGap",4,4,12,.5),new Rule("JunctionExclusion",8,4,16),new Rule("WalkCell",8,4,12),new Rule("TwoFaces",1,0,1),new Rule("Walks",1,0,1),new Rule("OutwardWalks",1,0,1)};
 public Dictionary<string,double> Values=new Dictionary<string,double>();
 public double this[string id]=>Values.TryGetValue(id,out var v)?v:Definitions.First(d=>d.Id==id).Default;
 public bool Set(string id,double value){var d=Definitions.FirstOrDefault(q=>q.Id==id);if(d==null||double.IsNaN(value)||double.IsInfinity(value))return false;Values[id]=Math.Max(d.Min,Math.Min(d.Max,value));return true;}
 public static TowerRules Parse(string json){var r=new TowerRules();try{var values=Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,double>>(json??"{}");if(values!=null)foreach(var v in values)r.Set(v.Key,v.Value);}catch(Newtonsoft.Json.JsonException){}return r;}
 public string Encode()=>Newtonsoft.Json.JsonConvert.SerializeObject(Values);
 public object State()=>Definitions.Select(d=>new{id=d.Id,value=this[d.Id],min=d.Min,max=d.Max,step=d.Step}).ToArray();
}
public static class TowerGeometry {
 public static List<Point> SurfaceNodes(IList<Point> input){
  var points=new List<Point>();foreach(var q in input)if(points.Count==0||(q-points.Last()).Length>.01)points.Add(q);
  if(points.Count>1&&(points[0]-points.Last()).Length<=.01)points.RemoveAt(points.Count-1);
  bool changed=true;while(changed&&points.Count>3){changed=false;for(int i=0;i<points.Count;i++){
   var a=points[(i+points.Count-1)%points.Count];var b=points[(i+1)%points.Count];
   if((VillaGeometry.Nearest(points[i],a,b)-points[i]).Length>.001)continue;points.RemoveAt(i);changed=true;break;
  }}return points;
 }
 public static PlanBezier Line(Point a,Point b)=>new PlanBezier{A=a,B=a+(b-a)*(1d/3),C=a+(b-a)*(2d/3),D=b};
 // True circular fillet approximation, one cubic per bend. Never shrink radius silently.
 public static List<PlanBezier> Fillet(IList<Point> input,double radius,bool closed,double gap=0){
  var pts=input.ToList();if(closed&&(pts[0]-pts.Last()).Length<.001)pts.RemoveAt(pts.Count-1);int n=pts.Count;if(n<2)return null;
  var a=pts.ToArray();var b=pts.ToArray();var handles=new double[n];
  for(int i=closed?0:1;i<(closed?n:n-1);i++){
   var u=VillaGeometry.Unit(pts[i]-pts[(i+n-1)%n]);var v=VillaGeometry.Unit(pts[(i+1)%n]-pts[i]);double angle=Math.Acos(Math.Max(-1,Math.Min(1,SiteFrame.Dot(u,v))));if(angle<.001)continue;if(angle>Math.PI-.01)return null;
   double cut=radius*Math.Tan(angle/2);a[i]=pts[i]-u*cut;b[i]=pts[i]+v*cut;handles[i]=4d/3*Math.Tan(angle/4)*radius;
  }
  var curves=new List<PlanBezier>();for(int i=0;i<(closed?n:n-1);i++){int j=(i+1)%n;double length=(pts[j]-pts[i]).Length-(b[i]-pts[i]).Length-(a[j]-pts[j]).Length;if(length<-.001||handles[i]>0&&handles[j]>0&&length<gap-.001)return null;if(length>.001)curves.Add(Line(b[i],a[j]));if(handles[j]>0){var u=VillaGeometry.Unit(pts[j]-pts[i]);var v=VillaGeometry.Unit(pts[(j+1)%n]-pts[j]);curves.Add(new PlanBezier{A=a[j],B=a[j]+u*handles[j],C=b[j]-v*handles[j],D=b[j]});}}
  return curves;
 }
 public static PlanRoute Route(IEnumerable<PlanBezier> curves,string use,double width){var r=new PlanRoute{Use=use,Width=width,Curves=curves.ToList()};foreach(var c in r.Curves){var pts=VillaGeometry.Sample(c,2);r.Points.AddRange(r.Points.Count==0?pts:pts.Skip(1));}return r;}
 public static double Distance(Point p,IList<Point> poly){double d=double.MaxValue;for(int i=0;i<poly.Count;i++)d=Math.Min(d,(p-VillaGeometry.Nearest(p,poly[i],poly[(i+1)%poly.Count])).Length);return d;}
 public static double Distance(IList<Point> a,IList<Point> b){if(VillaGeometry.Overlap(a,b))return 0;return Math.Min(a.Min(p=>Distance(p,b)),b.Min(p=>Distance(p,a)));}
}
}
