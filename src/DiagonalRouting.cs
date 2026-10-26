using System;
using System.Collections.Generic;

namespace ApartmentPlan {
// Only shortcut an S dogleg. Court rows, junction endpoints and building axes
// are unchanged. The caller checks the entire new road width before accepting.
internal static class DiagonalRouting {
 internal static List<Point> Shorten(IList<Point> input,double width,double grid,Func<Point,Point,bool> clear){
  var points=new List<Point>(input);
  for(int i=1;i+2<points.Count;i++){
   var ab=points[i]-points[i-1];var bc=points[i+1]-points[i];var cd=points[i+2]-points[i+1];
   double a=ab.Length,b=bc.Length,c=cd.Length;if(a<.01||b<Math.Max(width,grid*2)||c<.01)continue;
   var u=ab*(1/a);var v=bc*(1/b);var w=cd*(1/c);
   if(Math.Abs(SiteFrame.Dot(u,v))>.001||SiteFrame.Dot(u,w)<.999)continue;
   double lead=Math.Max(width,grid*2),left=a-lead,right=c-lead;
   if(left<0||right<0||left+right<b)continue;
   double trim=Math.Floor(Math.Min(left,Math.Max(b-right,b/2))/grid)*grid;
   if(trim<b-right-.001||trim<.001||b-trim<.001)continue;
   var start=points[i]-u*trim;var end=points[i+1]+w*(b-trim);
   if(!clear(start,end))continue;
   points[i]=start;points[i+1]=end;i++;
  }
  return points;
 }
}
internal sealed partial class ResidentialLayout {
 bool ClearDiagonal(Point a,Point b,double width){
  foreach(int id in DiagonalCells(a,b,width)){
   if(id<0)return false;int x=id%nx,y=id/nx,k=map[x,y];
   if((mainJunctionBuffer?[x,y]??false)||reserved[x,y]||k==Outside||k==Fixed||k==Reserve||k==3||k==5||edgeGreen[x,y]&&k!=1&&!EntranceApproach(x,y))return false;
  }
  return true;
 }
 IEnumerable<int> DiagonalCells(Point a,Point b,double width){
  double half=width/2;var delta=b-a;double length2=SiteFrame.Dot(delta,delta);
  for(int y=Y(Math.Min(a.Y,b.Y)-half-cell);y<=Y(Math.Max(a.Y,b.Y)+half+cell);y++)
   for(int x=X(Math.Min(a.X,b.X)-half-cell);x<=X(Math.Max(a.X,b.X)+half+cell);x++){
    var center=new Point(x0+(x+.5)*cell,y0+(y+.5)*cell);
    double t=length2<1e-8?0:Math.Max(0,Math.Min(1,SiteFrame.Dot(center-a,delta)/length2));
    if((center-a-delta*t).Length>half+cell*.707107)continue;
    yield return x<0||y<0||x>=nx||y>=ny?-1:y*nx+x;
   }
 }
}
}
