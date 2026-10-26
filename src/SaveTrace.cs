using Game;
namespace ApartmentPlan {
// These mark the serialization pipeline, not successful disk I/O.
public partial class BeforePlanSerialization:GameSystemBase {
 protected override void OnUpdate(){var s=World.GetExistingSystemManaged<PlanSystem>();Trace.Event("city.serialize.begin","plans="+s?.Saved.Count+" draft="+(s?.Draft!=null));}
}
public partial class AfterPlanSerialization:GameSystemBase {
 protected override void OnUpdate(){Trace.Event("city.serialize.end");}
}
}
