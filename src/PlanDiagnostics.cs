using System;
using System.Collections.Generic;
using System.Linq;
namespace ApartmentPlan {
internal static class PlanDiagnostics {
 internal static string Status(Plan p,bool selected,bool villa,bool pending=false,bool outdated=false){
  if(pending)return "AP.Calculating";if(outdated)return "AP.CalculationNeeded";if(p==null)return "AP.Draw";
  return !selected?"AP.SelectAssets":p.ParkingAccessFailures>0?"AP.ParkingAccessIncomplete":p.RequiredAccessFailures>0?(villa?"AP.Villa.NoConnection":"AP.AccessIncomplete"):!p.Parcels.Any(r=>r.Kind==0)?"AP.NoBuildingFit":p.VillaPedestrianWarnings>0?"AP.Villa.PedestrianWarning":p.VillaRejectedConnections.Count>0?"AP.Villa.RejectedApproach":p.Limited?"AP.LimitParcelsTrimmed":p.Shortfall.Skip(2).Any(x=>x>1)?"AP.Shortfall":"AP.Ready";
 }
 internal static List<Dictionary<string,int>> Evidence(Plan p,out int total){
  var result=new List<Dictionary<string,int>>();total=0;if(p==null)return result;
  if(p.RequiredAccessFailures>0){total++;result.Add(new Dictionary<string,int>{{"reason",80},{"ordinal",-1},{"connected",p.RequiredAccessFailures},{"edges",p.UnreachableAccess.Count}});}
  total+=p.AccessEvidenceTotal;foreach(var evidence in p.AccessEvidence){if(result.Count>=24)break;result.Add(new Dictionary<string,int>(evidence));}
  int ordinal=0;foreach(var c in p.ParkingConnections){ordinal++;if(c.Valid)continue;total++;if(result.Count>=24)continue;
   int reason=c.Failure=="No vehicle network"?81:c.Failure=="Parking axis or stall dimensions inconsistent"?82:c.Failure=="No full-width aisle with end clearance"?83:c.Failure=="Aisle disconnected or vehicle passage too narrow"?84:85;
   result.Add(new Dictionary<string,int>{{"reason",reason},{"ordinal",ordinal},{"widthCm",Cm(c.Bay?.W??0)},{"depthCm",Cm(c.Bay?.D??0)},{"connected",c.Bay?.ParkingSpaces??0},{"edges",c.Ports.Count}});
  }return result;
 }
 static int Cm(double value)=>double.IsNaN(value)||double.IsInfinity(value)?0:(int)Math.Max(0,Math.Min(int.MaxValue,Math.Round(value*100)));
}
}
