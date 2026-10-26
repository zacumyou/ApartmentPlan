using System;
using System.Collections.Generic;
namespace ApartmentPlan {
public static class BoundaryEditing {
 public static int Hit(IList<Point> points,Point cursor,bool closed,out bool edge,double radius=3.5){
  edge=false;int result=-1;double best=radius;
  for(int i=0;i<points.Count;i++){double d=(points[i]-cursor).Length;if(d<best){best=d;result=i;}}
  if(result>=0||!closed)return result;
  for(int i=0;i<points.Count;i++){double d=(BoundarySnap.Nearest(cursor,new SnapEdge(points[i],points[(i+1)%points.Count],0))-cursor).Length;if(d<best){best=d;result=i;edge=true;}}
  return result;
 }
 public static int Insert(List<Point> points,Point cursor,double radius=3.5){
  int index=Hit(points,cursor,true,out bool edge,radius);if(index<0||!edge||points.Count>=128)return -1;
  var point=BoundarySnap.Nearest(cursor,new SnapEdge(points[index],points[(index+1)%points.Count],0));
  var candidate=new List<Point>(points);candidate.Insert(index+1,point);
  if(!Layout.Valid(candidate))return -1;points.Insert(index+1,point);return index+1;
 }
 public static void Move(IList<Point> original,IList<Point> target,int index,bool edge,Point delta){
  for(int i=0;i<original.Count;i++)target[i]=original[i];
  target[index]=original[index]+delta;if(edge)target[(index+1)%target.Count]=original[(index+1)%target.Count]+delta;
 }
 public static Point Follow(IList<Point> before,IList<Point> after,Point entrance){
  int index=0;double best=double.MaxValue,fraction=0;
  for(int i=0;i<before.Count;i++){
   var a=before[i];var d=before[(i+1)%before.Count]-a;double length2=BoundarySnap.Dot(d,d);
   double t=length2<1e-8?0:Math.Max(0,Math.Min(1,BoundarySnap.Dot(entrance-a,d)/length2));
   double distance=(a+d*t-entrance).Length;if(distance>=best)continue;best=distance;index=i;fraction=t;
  }
  return after[index]+(after[(index+1)%after.Count]-after[index])*fraction;
 }
}
}
