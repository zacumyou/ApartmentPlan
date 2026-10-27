using System.Collections.Generic;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
namespace ApartmentPlan {
public partial class ConstructionTool {
 readonly List<Dictionary<string,int>> failureEvidence=new List<Dictionary<string,int>>();int evidenceTotal;
 void ClearEvidence(){failureEvidence.Clear();evidenceTotal=0;}
 int EvidenceKind(Entity e){if(!EntityManager.Exists(e))return 0;if(EntityManager.HasComponent<Game.Net.Edge>(e))return 1;if(EntityManager.HasComponent<Game.Net.Node>(e))return 2;if(EntityManager.HasComponent<Game.Buildings.Building>(e))return 3;if(EntityManager.HasComponent<Game.Objects.Tree>(e))return 4;if(EntityManager.HasBuffer<Game.Areas.Node>(e))return 5;return 6;}
 void Evidence(Entity e,int reason,int flags=0){
  evidenceTotal++;if(failureEvidence.Count>=24)return;
  var record=new Dictionary<string,int>{{"reason",reason},{"ordinal",evidenceTotal},{"kind",EvidenceKind(e)},{"flags",flags}};
  if(EntityManager.Exists(e)){
   if(EntityManager.HasComponent<Owner>(e))record["ownerKind"]=EvidenceKind(EntityManager.GetComponentData<Owner>(e).m_Owner);
   if(EntityManager.HasBuffer<Game.Net.ConnectedEdge>(e))record["connected"]=EntityManager.GetBuffer<Game.Net.ConnectedEdge>(e,true).Length;
   if(EntityManager.HasComponent<Game.Net.Curve>(e))record["lengthCm"]=(int)(Colossal.Mathematics.MathUtils.Length(EntityManager.GetComponentData<Game.Net.Curve>(e).m_Bezier)*100);
   if(EntityManager.HasComponent<PrefabRef>(e)){var p=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
    if(EntityManager.HasComponent<NetGeometryData>(p))record["roadWidthCm"]=(int)(EntityManager.GetComponentData<NetGeometryData>(p).m_DefaultWidth*100);
    if(EntityManager.HasComponent<BuildingData>(p)){var size=EntityManager.GetComponentData<BuildingData>(p).m_LotSize;record["widthCm"]=size.x*800;record["depthCm"]=size.y*800;}
   }
   if(EntityManager.HasBuffer<Game.Notifications.IconElement>(e))foreach(var icon in EntityManager.GetBuffer<Game.Notifications.IconElement>(e,true)){
    if(!EntityManager.HasComponent<PrefabRef>(icon.m_Icon))continue;var p=EntityManager.GetComponentData<PrefabRef>(icon.m_Icon).m_Prefab;
    if(EntityManager.HasComponent<ToolErrorData>(p)){record["errorCode"]=(int)EntityManager.GetComponentData<ToolErrorData>(p).m_Error;break;}
   }
  }
  failureEvidence.Add(record);
 }
}
}
