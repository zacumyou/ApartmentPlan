using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
public static class SurfaceOutline {
 // Each free convex corner has four chords. Never round seams against another
 // parcel, bay, road or lot: doing so opens unwanted holes in joined paving.
 public static List<Point> Create(Parcel parcel,IList<Parcel> contacts,double radius){
  var result=new List<Point>();var c=parcel.Corners;double r=Math.Max(0,Math.Min(radius,Math.Min(parcel.W,parcel.D)/2));
  for(int i=0;i<4;i++){
   var p=c[i];var incoming=c[(i+3)%4]-p;incoming=incoming*(1/incoming.Length);var outgoing=c[(i+1)%4]-p;outgoing=outgoing*(1/outgoing.Length);
   bool touching=contacts.Any(q=>!ReferenceEquals(q,parcel)&&q.Kind!=4&&q.Kind!=5&&Distance(q,p)<r+.05);
   if(r<.1||touching){result.Add(p);continue;}
   var center=p+(incoming+outgoing)*r;var start=p+incoming*r-center;double angle=Math.Atan2(start.Y,start.X);
   for(int k=0;k<=4;k++){double theta=angle+k*Math.PI/8;result.Add(center+new Point(Math.Cos(theta),Math.Sin(theta))*r);}
  }return result;
 }
 static double Distance(Parcel q,Point p){double x=Math.Max(q.X-p.X,Math.Max(0,p.X-q.X-q.W)),y=Math.Max(q.Y-p.Y,Math.Max(0,p.Y-q.Y-q.D));return Math.Sqrt(x*x+y*y);}
}
}
