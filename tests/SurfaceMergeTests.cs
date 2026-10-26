using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ApartmentPlan;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
static class SurfaceMergeTests {
 static Parcel Rect(double x,double y,double w,double h)=>new Parcel{Kind=8,X=x,Y=y,W=w,D=h};
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static bool Crosses(Point a,Point b,Point c,Point d){
  if(Math.Max(a.X,b.X)<Math.Min(c.X,d.X)||Math.Max(c.X,d.X)<Math.Min(a.X,b.X)||Math.Max(a.Y,b.Y)<Math.Min(c.Y,d.Y)||Math.Max(c.Y,d.Y)<Math.Min(a.Y,b.Y))return false;
  return Layout.Cross(b-a,c-a)*Layout.Cross(b-a,d-a)<=0&&Layout.Cross(d-c,a-c)*Layout.Cross(d-c,b-c)<=0;
 }
 static void Verify(Parcel[] before,List<List<Point>> after){
  double area=ParkingSurfaceGeometry.Union(before).Sum(r=>r.Area);
  Check(Math.Abs(area-after.Sum(p=>Math.Abs(Layout.SignedArea(p))))<Math.Max(.001,area*.000001),"surface area changed");
  foreach(var p in after){
   Check(p.Count>=3&&p.Count<=SurfaceMerge.NodeBudget,"node budget");
   for(int i=0;i<p.Count;i++)for(int j=i+1;j<p.Count;j++){
    if(j==i+1||i==0&&j==p.Count-1)continue;
    Check(!Crosses(p[i],p[(i+1)%p.Count],p[j],p[(j+1)%p.Count]),"self-intersecting surface");
   }
  }
  // Compare occupancy throughout the footprint, including holes and gaps.
  double x0=before.Min(p=>p.X),y0=before.Min(p=>p.Y),x1=before.Max(p=>p.X+p.W),y1=before.Max(p=>p.Y+p.D);
  for(int y=0;y<101;y++)for(int x=0;x<101;x++){
   var point=new Point(x0+(x+.371)*(x1-x0)/101,y0+(y+.617)*(y1-y0)/101);
   bool covered=before.Any(r=>point.X>r.X&&point.X<r.X+r.W&&point.Y>r.Y&&point.Y<r.Y+r.D);
   int count=after.Count(p=>Layout.Inside(point,p));Check(covered==(count>0)&&count<=1,"coverage/hole/overlap changed");
  }
 }
 internal static void Run(){
  var cases=new Dictionary<string,Parcel[]>{
   ["L shape"]=new[]{Rect(0,0,20,10),Rect(0,10,10,10)},
   ["T junction"]=new[]{Rect(0,0,30,10),Rect(10,10,10,20)},
   ["overlap"]=new[]{Rect(0,0,20,10),Rect(10,5,20,10)},
   ["hole"]=new[]{Rect(0,0,30,10),Rect(0,10,10,10),Rect(20,10,10,10),Rect(0,20,30,10)},
   ["point contact"]=new[]{Rect(0,0,10,10),Rect(10,10,10,10)},
   ["gap"]=new[]{Rect(0,0,10,10),Rect(10.01,0,10,10)},
   ["stairs"]=Enumerable.Range(0,80).Select(i=>Rect(i*2,0,2,2+i*2)).ToArray()
  };
  var examples=new List<object>();
  foreach(var item in cases){var after=SurfaceMerge.Create(item.Value);Verify(item.Value,after);
   if(item.Key=="L shape"||item.Key=="T junction"||item.Key=="overlap")Check(after.Count==1,item.Key+" should merge");
   if(item.Key=="hole"||item.Key=="point contact"||item.Key=="gap")Check(after.Count==2,item.Key+" must retain separation");
   Console.WriteLine("PASS "+item.Key+": "+item.Value.Length+" -> "+after.Count);examples.Add(new{Name=item.Key,Before=item.Value,After=after});
  }
  var plan=JObject.Parse(File.ReadAllText("ApartmentPlan/docs/connection-update-1.json"))["Plan"].ToObject<Plan>();
  var before=ParkingSurfaceGeometry.Paving(plan,true).ToArray();var timer=System.Diagnostics.Stopwatch.StartNew();
  var merged=SurfaceMerge.Create(before);double ms=timer.Elapsed.TotalMilliseconds;Verify(before,merged);
  var grassBefore=plan.GrassPockets.ToArray();var grass=SurfaceMerge.Create(grassBefore);if(grassBefore.Length>0)Verify(grassBefore,grass);
  Check(merged.Count<before.Length,"representative fragmentation did not decrease");
  File.WriteAllText("ApartmentPlan/docs/surface-merge.json",JsonConvert.SerializeObject(new{Plan=plan,Before=before,After=merged,GrassBefore=grassBefore,GrassAfter=grass,Examples=examples,MergeMs=ms}));
  Console.WriteLine("PASS captured plan: pavement "+before.Length+" -> "+merged.Count+", grass "+grassBefore.Length+" -> "+grass.Count+", area="+before.Sum(r=>r.Area).ToString("F3")+", merge ms="+ms.ToString("F1")+", max nodes="+merged.Max(p=>p.Count));
 }
}
