using System;
using System.Collections.Generic;
using ApartmentPlan;
static class SnapTests {
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS snap "+name);}
 static bool Near(Point p,double x,double y)=>(p-new Point(x,y)).Length<.001;
 public static void Run(){
  var empty=new List<Point>();var road=new[]{new SnapEdge(new Point(0,0),new Point(100,0),1)};
  var first=BoundarySnap.Solve(new Point(33,3),empty,road,new Point(1,0),8,12,true,true);Check(Near(first.Position,33,0)&&first.Kind==1,"first point on road edge");
  var pts=new[]{new Point(0,0),new Point(40,0),new Point(40,40)};
  var close=BoundarySnap.Solve(new Point(6,2),pts,road,new Point(1,0),8,12,true,true);Check(close.Close&&Near(close.Position,0,0),"closure wins over road");
  var corner=BoundarySnap.Solve(new Point(2,39),pts,Array.Empty<SnapEdge>(),new Point(1,0),8,12,true,true);Check(Near(corner.Position,0,40)&&corner.Guides.Count==2,"two guide constraints make exact fourth corner");
  var crossing=BoundarySnap.Solve(new Point(42,2),new[]{new Point(40,40)},road,new Point(1,0),8,12,true,true);Check(Near(crossing.Position,40,0),"road and perpendicular intersection");
  var free=BoundarySnap.Solve(new Point(3,5),empty,Array.Empty<SnapEdge>(),new Point(1,0),8,12,false,false);Check(Near(free.Position,3,5)&&free.Kind==0,"disabled sources preserve cursor");
  var end=BoundarySnap.Solve(new Point(103,2),empty,road,new Point(1,0),8,12,false,false);Check(Near(end.Position,100,0),"finite edge cannot extend beyond road");
  Check(!Layout.CanAppend(new[]{new Point(0,0),new Point(40,0),new Point(40,40)},new Point(20,-10)),"crossing segment rejected");
  Check(Layout.ValidOpen(new[]{new Point(0,0),new Point(40,0),new Point(40,40)}),"open three point drawing valid");
 }
}
