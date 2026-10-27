using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
namespace ApartmentPlan {
public partial class PlanSystem {
 int diagnosticCopyState;
 Dictionary<string,int> layoutReasons=new Dictionary<string,int>();
 string failedLayoutInput;
 string failedLayoutStatus;
 readonly List<string> frontageDetails=new List<string>();
 static readonly HashSet<string> errorStatuses=new HashSet<string>{
  "AP.Error","AP.RoadPackMissing","AP.CatalogFailed","AP.NetworkUnavailable","AP.FrontageRecalculate","AP.NativeBlocked",
  "AP.ParkingDecalMissing","AP.ParkingDecalRecalculate","AP.AccessIncomplete","AP.ParkingAccessIncomplete",
  "AP.InvalidBoundary","AP.InvalidAsset","AP.EntranceFit","AP.CurveFit","AP.ZoneFit","AP.ManualRoadFit",
  "AP.ReplacementFit","AP.ReplacementFar","AP.ReplacementParking","AP.NoBuildingFit","AP.NeedBuildings",
  "AP.LegacyUneditable","AP.LegacyBuild","AP.DemolitionIncomplete","AP.NoOwnership"};
 bool StatusError=>!LayoutPending&&!layoutDebounce.Pending&&(errorStatuses.Contains(Status)||Status.StartsWith("AP.Tower.",StringComparison.Ordinal)||Status=="AP.Villa.NoConnection"||Status=="AP.Villa.NoThrough"||Status=="AP.Villa.EmptyBlock"||Status.StartsWith("AP.Limit")||Draft?.ParkingAccessFailures>0||Draft?.RequiredAccessFailures>0);
 void CopyDiagnostics(){
  try{
   var native=HasCurrentNativeReport?World.GetExistingSystemManaged<ConstructionTool>():null;
   var report=new {
    utc=DateTime.UtcNow,mod="ApartmentPlan",version=typeof(Mod).Assembly.GetName().Version.ToString(),
    build=typeof(Mod).Module.ModuleVersionId,gameVersion=UnityEngine.Application.version,
    status=Status,limit=LimitInfo,mode=Mode,layoutPending=LayoutPending,layoutOutdated=LayoutOutdated,
    main=Mod.Options.MainNetwork,aisle=Mod.Options.AisleNetwork,walk=Mod.Options.WalkNetwork,
    buildingPattern=Mod.Options.BuildingPattern,allowMainTJunctions=Mod.Options.AllowMainTJunctions,
    selected=Selected.ToArray(),boundary=Boundary,entrances=Entrances,
    buildings=Draft?.Parcels.Count(p=>p.Kind==0),parking=Draft?.Parcels.Sum(p=>p.ParkingSpaces),
    layout=Draft?.Diagnostics,layoutReasons,failedLayoutStatus,failedLayoutInput,accessFailures=Draft?.RequiredAccessFailures,parkingFailures=Draft?.ParkingAccessFailures,
    frontageFailures=Status=="AP.FrontageRecalculate"?frontageDetails:null,
    pedestrianAuditFile=World.GetExistingSystemManaged<ConstructionTool>()?.PedestrianAuditFile,
    nativeReport=native?.Report,nativeNotice=native?.Notice,nativeMetrics=Status=="AP.NativeBlocked"&&NativeFailureSnapshot!=null?NativeFailureSnapshot:native?.Metrics
   };
   UnityEngine.GUIUtility.systemCopyBuffer=Trace.Redact("Apartment Plan diagnostic report\n"+JsonConvert.SerializeObject(report,Formatting.Indented)+"\n\nRecent Apartment Plan events (bounded; not the full game log)\n"+Trace.Recent());
   diagnosticCopyState=1;
  }catch(Exception e){diagnosticCopyState=-1;Trace.Failure("diagnostic.copy.failed",e);}
  Revision++;
 }
}
}
