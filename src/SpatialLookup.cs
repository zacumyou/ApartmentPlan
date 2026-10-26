using System;
using System.Collections.Generic;
namespace ApartmentPlan {
// Broad phase only. Callers still perform the original exact geometry checks.
internal sealed class SpatialLookup<T> {
 readonly double size;
 readonly Dictionary<(int,int),List<T>> cells=new Dictionary<(int,int),List<T>>();
 readonly List<T> large=new List<T>();
 internal SpatialLookup(double cellSize){size=cellSize;}
 int Cell(double value)=>(int)Math.Floor(value/size);
 internal void Add(T value,double minX,double minZ,double maxX,double maxZ){
  int x0=Cell(minX),z0=Cell(minZ),x1=Cell(maxX),z1=Cell(maxZ);
  if((long)(x1-x0+1)*(z1-z0+1)>4096){large.Add(value);return;}
  for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){
   if(!cells.TryGetValue((x,z),out var list))cells[(x,z)]=list=new List<T>();list.Add(value);
  }
 }
 internal IEnumerable<T> Within(double minX,double minZ,double maxX,double maxZ){
  var seen=new HashSet<T>();int x0=Cell(minX),z0=Cell(minZ),x1=Cell(maxX),z1=Cell(maxZ);
  for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)if(cells.TryGetValue((x,z),out var list))foreach(var value in list)if(seen.Add(value))yield return value;
  foreach(var value in large)if(seen.Add(value))yield return value;
 }
 internal IEnumerable<T> At(double x,double z){
  if(cells.TryGetValue((Cell(x),Cell(z)),out var list))foreach(var value in list)yield return value;
  foreach(var value in large)yield return value;
 }
}
}
