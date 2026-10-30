using System;
using System.Linq;
using Newtonsoft.Json;
namespace ApartmentPlan {
public partial class PlanSystem {
 void ReflowBuilding(Asset asset){
  var candidate=JsonConvert.DeserializeObject<Plan>(JsonConvert.SerializeObject(Draft));
  if(!BuildingReflow.Try(candidate,ReplacementIndex,asset,Network("aisle")?.Width??8,0,0,out var building)){Status="AP.ReflowNoSpace";return;}
  var old=Draft.Parcels[ReplacementIndex];building.FloorArea=Math.Min(asset.Width*asset.Depth,asset.Footprint>0?asset.Footprint:asset.Width*asset.Depth*Mod.Options.FootprintPercent/100.0)*Math.Max(1,Math.Floor(asset.Height/Mod.Options.FloorHeight));
  candidate.Homes+=building.Homes-old.Homes;candidate.Far=candidate.Parcels.Where(p=>p.Kind==0).Sum(p=>p.FloorArea)/candidate.Area*100;
  var rejected=new System.Collections.Generic.List<Parcel>();FrontageNetwork.Missing(candidate,Assets,Network("aisle")?.Width??8,Mod.Options.ParkingPavingMargin,null,rejected);
  if(Mod.Options.RequireBuildingRoad&&rejected.Contains(building)){Status="AP.RotationAccess";return;}
  candidate.Shortfall[0]=Math.Max(0,candidate.Homes*Mod.Options.ParkingPerHome-candidate.Parcels.Sum(p=>p.ParkingSpaces));Draft=candidate;ReplacementIndex=Draft.Parcels.IndexOf(building);RememberBuildingAsset(asset.Id);RenderDraft();Status="AP.ReflowDone";
 }
}
}
