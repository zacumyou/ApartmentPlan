using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
internal static class SiteTerrainMask {
 internal static bool Contains(Point p,IList<Point> boundary,IList<ManualZone> zones)=>Layout.Inside(p,boundary)&&!(zones?.Any(z=>z.Kind==5&&Layout.Inside(p,z.Points))??false);
 internal static byte[] Raster(IList<Point> boundary,IList<ManualZone> zones,double x,double y,double size,int resolution){
  var result=new byte[resolution*resolution];for(int j=0;j<resolution;j++)for(int i=0;i<resolution;i++)if(Contains(new Point(x+(i+.5)*size/resolution,y+(j+.5)*size/resolution),boundary,zones))result[j*resolution+i]=255;
  return result;
 }
}
}
