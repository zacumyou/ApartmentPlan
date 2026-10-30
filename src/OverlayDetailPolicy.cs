using System;
namespace ApartmentPlan {
internal static class OverlayDetailPolicy {
 internal static int TreeStride(int count)=>Math.Max(1,(int)Math.Ceiling(count/96.0));
 // Only decorative details use this threshold; boundaries/warnings stay visible.
 internal static bool Visible(double size,double depth,double pixelsPerUnit,bool orthographic,double minimumPixels){
  if(size<=0)return false;
  if(pixelsPerUnit<=0||double.IsNaN(pixelsPerUnit)||double.IsInfinity(pixelsPerUnit))return true;
  return size*pixelsPerUnit/(orthographic?1:Math.Max(1,depth))>=minimumPixels;
 }
}
}
