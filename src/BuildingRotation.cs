using System;
using System.Linq;
namespace ApartmentPlan {
public partial class PlanSystem {
 void RemoveReplacementPaving(Parcel old){
  if(old.ReplacementBounds==null)return;var b=old.ReplacementBounds;
  // Only strips inside the original, exclusively occupied building slot.
  var remove=Draft.Parcels.Where(p=>p.Kind==8&&p.X>=b.X-.001&&p.Y>=b.Y-.001&&p.X+p.W<=b.X+b.W+.001&&p.Y+p.D<=b.Y+b.D+.001).ToList();
  foreach(var p in remove){int index=Draft.Parcels.IndexOf(p);if(index<ReplacementIndex)ReplacementIndex--;Draft.Parcels.Remove(p);}
 }
 void RotateBuilding(int direction){
  if(Mode!="replace"||LayoutPending||LayoutOutdated||Draft==null||ReplacementIndex<0||ReplacementIndex>=Draft.Parcels.Count)return;
  var old=Draft.Parcels[ReplacementIndex];var asset=Assets.Find(a=>a.Id==old.AssetId);if(asset==null)return;
  ClearForcedBuilding();var bounds=old.ReplacementBounds??old;bool half=Math.Abs(direction)==2;double w=half?old.W:old.D,d=half?old.D:old.W;
  var f=BuildingPlacement.Forward(old,asset);var next=Newtonsoft.Json.JsonConvert.DeserializeObject<Parcel>(Newtonsoft.Json.JsonConvert.SerializeObject(old));
  next.X=bounds.X+(bounds.W-w)/2;next.Y=bounds.Y+(bounds.D-d)/2;next.W=w;next.D=d;
  next.PlacementForward=ReplacementRules.Turn(f,direction);
  next.ReplacementBounds=new Parcel{X=bounds.X,Y=bounds.Y,W=bounds.W,D=bounds.D,Frontage=old.Frontage};
  if(w>bounds.W+.001||d>bounds.D+.001){OfferForcedBuilding(next);Status="AP.RotationFit";return;}
  Draft.Parcels[ReplacementIndex]=next;
  try{
   var rejected=new System.Collections.Generic.List<Parcel>();FrontageNetwork.Missing(Draft,Assets,Network("aisle")?.Width??8,Mod.Options.ParkingPavingMargin,null,rejected);
   if(rejected.Contains(next)){Draft.Parcels[ReplacementIndex]=old;OfferForcedBuilding(next);Status="AP.RotationAccess";return;}
  }finally{Draft.Parcels[ReplacementIndex]=old;}
  RemoveReplacementPaving(old);Draft.Parcels[ReplacementIndex]=next;Draft.Parcels.AddRange(ReplacementRules.Vacated(bounds,next));RenderDraft();Status="AP.Rotated";
 }
}
}
