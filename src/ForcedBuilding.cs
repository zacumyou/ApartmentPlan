using System;
using System.Linq;
namespace ApartmentPlan {
public partial class PlanSystem {
 Plan forcePlan;Parcel forceOriginal,forceCandidate;
 bool CanForceBuilding=>Mode=="replace"&&!LayoutPending&&!LayoutOutdated&&!NativeCommitted&&Draft!=null&&Draft==forcePlan&&ReplacementIndex>=0&&ReplacementIndex<Draft.Parcels.Count&&ReferenceEquals(Draft.Parcels[ReplacementIndex],forceOriginal)&&forceCandidate!=null;
 void ClearForcedBuilding(){forcePlan=null;forceOriginal=forceCandidate=null;}
 void OfferForcedBuilding(Parcel candidate){forcePlan=Draft;forceOriginal=Draft.Parcels[ReplacementIndex];forceCandidate=candidate;}
 void ForceBuilding(){
  if(!CanForceBuilding)return;var old=forceOriginal;var next=forceCandidate;
  // This changes only the selected planning lot. Native construction and deletion protections remain intact.
  RemoveReplacementPaving(old);Draft.Parcels[ReplacementIndex]=next;
  Draft.Homes+=next.Homes-old.Homes;Draft.Far=Draft.Parcels.Where(p=>p.Kind==0).Sum(p=>p.FloorArea)/Draft.Area*100;
  Draft.Shortfall[0]=Math.Max(0,Draft.Homes*Mod.Options.ParkingPerHome-Draft.Parcels.Sum(p=>p.ParkingSpaces));
  RememberBuildingAsset(next.AssetId);ClearForcedBuilding();RenderDraft();Status="AP.ForcedBuilding";
 }
}
}
