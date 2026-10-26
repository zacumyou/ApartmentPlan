using System;
namespace ApartmentPlan {
// Nonblocking gate: a frame delay alone is not evidence of a completed GPU readback.
internal sealed class TerrainSettlement {
 readonly double started;int stable;
 internal TerrainSettlement(double now){started=now;}
 internal bool Observe(double now,double maximumError,int samples){
  if(samples==0||double.IsNaN(maximumError)||double.IsInfinity(maximumError))throw new InvalidOperationException("Terrain verification samples unavailable");
  if(now-started>30)throw new InvalidOperationException("Terrain flattening did not settle within 30 seconds; maximum height error="+maximumError+"m across "+samples+" samples. No roads or buildings were applied");
  stable=maximumError<=.35?stable+1:0;
  return now-started>=.6&&stable>=3;
 }
}
}
