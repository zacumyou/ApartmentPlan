using System;
using System.Collections.Generic;
namespace ApartmentPlan {
internal sealed class PlanPickGrid {
 const int PickCellSize=128;
 readonly Dictionary<(int,int),List<int>> pickCells=new Dictionary<(int,int),List<int>>();
 readonly List<int> candidates=new List<int>(),oversize=new List<int>();
 readonly HashSet<int> seen=new HashSet<int>();
 static int PickCell(double value)=>(int)Math.Floor(value/PickCellSize);
 public void Clear(){pickCells.Clear();oversize.Clear();}
 public void Add(int index,Parcel bounds){
  if(bounds.W>8192||bounds.D>8192){oversize.Add(index);return;}
   for(int x=PickCell(bounds.X-.01);x<=PickCell(bounds.X+bounds.W+.01);x++)
    for(int y=PickCell(bounds.Y-.01);y<=PickCell(bounds.Y+bounds.D+.01);y++){
     if(!pickCells.TryGetValue((x,y),out var list))pickCells[(x,y)]=list=new List<int>();
     list.Add(index);
    }

 }
 void Include(int index){if(seen.Add(index))candidates.Add(index);}
 // Steep and shallow rays follow the same cells as the original terrain solver.
 public List<int> Query(Point a,Point b){
  candidates.Clear();seen.Clear();foreach(int index in oversize)Include(index);
  int x=PickCell(a.X),y=PickCell(a.Y),endX=PickCell(b.X),endY=PickCell(b.Y);
  int sx=System.Math.Sign(endX-x),sy=System.Math.Sign(endY-y);
  double stepX=sx==0?double.PositiveInfinity:PickCellSize/System.Math.Abs((double)b.X-a.X);
  double stepY=sy==0?double.PositiveInfinity:PickCellSize/System.Math.Abs((double)b.Y-a.Y);
  double tx=sx==0?double.PositiveInfinity:((x+(sx>0?1:0))*PickCellSize-a.X)/((double)b.X-a.X);
  double ty=sy==0?double.PositiveInfinity:((y+(sy>0?1:0))*PickCellSize-a.Y)/((double)b.Y-a.Y);
  while(true){
   if(pickCells.TryGetValue((x,y),out var list))foreach(int index in list)Include(index);
   if(x==endX&&y==endY)break;
   if(y==endY||x!=endX&&tx<ty){x+=sx;tx+=stepX;}else{y+=sy;ty+=stepY;}
  }
  candidates.Sort();return candidates;
 }
}
}
