using System;
namespace ApartmentPlan {
internal static class TracePolicy {
 internal static bool Keep(string action,bool debug,bool failure=false){
  if(debug||failure)return true;
  if(action==null)return false;
  if(action.IndexOf("failed",StringComparison.OrdinalIgnoreCase)>=0||action.IndexOf("failure",StringComparison.OrdinalIgnoreCase)>=0||action.IndexOf("error",StringComparison.OrdinalIgnoreCase)>=0||action.IndexOf("rejected",StringComparison.OrdinalIgnoreCase)>=0||action.EndsWith(".missing",StringComparison.Ordinal)||action.IndexOf("unavailable",StringComparison.OrdinalIgnoreCase)>=0||action.IndexOf("blocked",StringComparison.OrdinalIgnoreCase)>=0||action.IndexOf("rejection",StringComparison.OrdinalIgnoreCase)>=0)return true;
  switch(action){
   case "session.start":case "session.end":case "mod.load.complete":case "load.complete.end":case "save.complete":case "plan.edit.open":case "plan.demolition.request":case "plan.deleted":case "plan.demolition.complete":case "plan.demolition.force":case "plan.replacement.cleared":case "plan.construction.recorded":case "construction.AP.NativeApplied":case "construction.AP.DemolitionApplied":case "construction.AP.NativeBlocked":case "unity.Warning":case "unity.Assert":case "unity.Exception":return true;
   default:return false;
  }
 }
}
}

