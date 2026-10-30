using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public static class BuildingReflow {
 static Parcel Box(double x,double y,double w,double d,int kind)=>new Parcel{X=x,Y=y,W=w,D=d,Kind=kind};
 static List<Parcel> Cut(Parcel source,Parcel cut){
  double x=Math.Max(source.X,cut.X),y=Math.Max(source.Y,cut.Y),right=Math.Min(source.X+source.W,cut.X+cut.W),top=Math.Min(source.Y+source.D,cut.Y+cut.D);
  if(right<=x||top<=y)return new List<Parcel>{source};
  return ReplacementRules.Vacated(source,Box(x,y,right-x,top-y,source.Kind)).Select(q=>{q.Kind=source.Kind;q.InvisibleRoad=source.InvisibleRoad;return q;}).ToList();
 }
 // A bounded local replan. Existing buildings, roads, parking and facility lots
 // remain fixed; only park/paving and the selected building slot may be consumed.
 public static bool Try(Plan plan,int index,Asset asset,double roadWidth,double gap,double parallel,out Parcel placed,Func<Parcel,bool> accept=null){
  placed=null;if(index<0||index>=plan.Parcels.Count)return false;
  var old=plan.Parcels[index];var boundary=plan.Boundary.Select(plan.Local).ToList();
  var free=plan.Parcels.Where(p=>ReferenceEquals(p,old)||p.Kind==4||p.Kind==8).ToList();
  var obstacles=plan.Parcels.Except(free).ToList();
  bool Room(Parcel p,bool road=false){
   if(!Layout.Contains(p,boundary))return false;
   if(obstacles.Any(q=>Layout.Overlap(p,q)&&!(road&&q.Kind==1)))return false;
   if(!road&&plan.Parcels.Any(q=>q.Kind==0&&!ReferenceEquals(q,old)&&!FrontageRules.Spaced(p,q,parallel,gap)))return false;
   return true;
  }
  var graph=ConstructionGraph.Create(plan).Where(e=>e.Use=="aisle"||e.Use=="aisleWide"||e.Use=="main").ToList();
  var positions=new List<Point>{new Point(old.X+old.W/2,old.Y+old.D/2)};
  foreach(var p in free.Where(p=>p.Kind==4||p.Kind==8).OrderBy(p=>Math.Pow(p.X-old.X,2)+Math.Pow(p.Y-old.Y,2)).Take(96)){
   positions.Add(new Point(p.X+p.W/2,p.Y+p.D/2));positions.Add(new Point(p.X+asset.Width/2,p.Y+asset.Depth/2));
  }
  int attempts=0;foreach(var center in positions)foreach(int turn in new[]{0,1,2,3}){
   bool swap=turn%2==1;double w=swap?asset.Depth:asset.Width,d=swap?asset.Width:asset.Depth;
   var b=Box(center.X-w/2,center.Y-d/2,w,d,0);if(!Room(b)||accept!=null&&!accept(b))continue;
   var f=new[]{new Point(0,-1),new Point(1,0),new Point(0,1),new Point(-1,0)}[turn];var side=new Point(f.Y,-f.X);
   var front=center+f*(asset.Depth/2+roadWidth/2+1);var start=front-side*(asset.Width/2);var end=front+side*(asset.Width/2);
   Parcel Ribbon(Point a,Point z)=>Box(Math.Min(a.X,z.X)-roadWidth/2,Math.Min(a.Y,z.Y)-roadWidth/2,Math.Abs(z.X-a.X)+roadWidth,Math.Abs(z.Y-a.Y)+roadWidth,1);
   var frontage=Ribbon(start,end);frontage.InvisibleRoad=true;if(!Room(frontage,true)||Layout.Overlap(frontage,b))continue;
   foreach(var e in graph.OrderBy(e=>Math.Min((plan.Local(e.A)-front).Length,(plan.Local(e.B)-front).Length)).Take(16))foreach(var target in new[]{plan.Local(VillaGeometry.Nearest(plan.World(front),e.A,e.B)),plan.Local(e.A),plan.Local(e.B)}){
    if((target-front).Length>80)continue;
    foreach(bool xFirst in new[]{true,false}){
     if(++attempts>1024)return false;
     var bend=xFirst?new Point(target.X,front.Y):new Point(front.X,target.Y);
     var points=new[]{front,bend,target};var roads=new List<Parcel>{frontage};bool ok=true;
     for(int n=1;n<points.Length;n++){if((points[n]-points[n-1]).Length<.01)continue;var road=Ribbon(points[n-1],points[n]);road.InvisibleRoad=true;if(!Room(road,true)||Layout.Overlap(road,b)){ok=false;break;}roads.Add(road);}
     if(!ok)continue;
     // All validation above is read-only. Commit only the complete candidate.
     var pieces=new List<Parcel>(plan.Parcels);pieces.Remove(old);
     foreach(var cut in new[]{b}.Concat(roads)){
      var updated=new List<Parcel>();foreach(var p in pieces){if(p.Kind==4||p.Kind==8)updated.AddRange(Cut(p,cut));else updated.Add(p);}pieces=updated;
     }
     if(!Layout.Overlap(old,b))pieces.Add(Box(old.X,old.Y,old.W,old.D,8));
     else foreach(var rem in Cut(Box(old.X,old.Y,old.W,old.D,8),b))pieces.Add(rem);
     // Road geometry already in the graph is retained; carve new paving strips to avoid duplicate parcel area.
     foreach(var road in roads){var additions=new List<Parcel>{road};foreach(var existing in pieces){var next=new List<Parcel>();foreach(var a in additions)next.AddRange(Cut(a,existing));additions=next;}pieces.AddRange(additions);}
     b.AssetId=asset.Id;b.Homes=asset.Homes;b.PlacementForward=f;b.Frontage=frontage;
     pieces.Add(b);plan.Parcels=pieces;
     plan.Routes.Add(new PlanRoute{Use="aisle",Width=roadWidth,Points=new List<Point>{start,end}});
     plan.Routes.Add(new PlanRoute{Use="aisle",Width=roadWidth,Points=points.ToList()});
     placed=b;return true;
    }
   }
  }
  return false;
 }
}
}
