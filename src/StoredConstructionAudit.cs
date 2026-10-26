using System.Collections.Generic;
using Game.Tools;
namespace ApartmentPlan {
public partial class PlanSystem {
 readonly List<int> storedAudits=new List<int>();int storedAuditTicks;
 void QueueStoredAudit(int id){if(!storedAudits.Contains(id))storedAudits.Add(id);storedAuditTicks=180;}
 void TickStoredAudit(){
  if(storedAudits.Count==0||--storedAuditTicks>0||World.GetExistingSystemManaged<ToolSystem>()?.activeTool is ConstructionTool)return;
  int id=storedAudits[0];storedAudits.RemoveAt(0);storedAuditTicks=180;var stored=Saved.Find(p=>p.Info.Number==id);if(stored==null||!IsBuilt(stored))return;
  // Read-only, scoped to this compound. Closing its tool must not cancel audit.
  string previous=Status;
  try{Trace.Event("construction.stored.audit","id="+id);World.GetOrCreateSystemManaged<ConstructionTool>().AuditStored(stored);}
  catch(System.Exception error){Trace.Failure("construction.stored.audit.failed",error);}
  finally{Status=previous;}
 }
}
}
