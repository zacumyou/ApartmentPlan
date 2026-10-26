using System;
using Game.Buildings;
using Game.Citizens;
using Unity.Collections;
using Unity.Entities;
namespace ApartmentPlan {
public partial class PlanSystem {
 double populationChecked=-1000;
 internal double HouseholdAverage;
 internal int? EstimatedPopulation=>HouseholdAverage>0?(int?)Math.Round((Draft?.Homes??0)*HouseholdAverage):null;
 void UpdatePopulationEstimate(){
  double now=UnityEngine.Time.realtimeSinceStartup;if(now-populationChecked<60)return;populationChecked=now;
  // Bounded, evenly spaced sample of resident households; no per-frame citizen scan.
  using(var query=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Household>(),ComponentType.ReadOnly<HouseholdCitizen>(),ComponentType.ReadOnly<PropertyRenter>(),ComponentType.Exclude<TouristHousehold>(),ComponentType.Exclude<CommuterHousehold>(),ComponentType.Exclude<Game.Common.Deleted>(),ComponentType.Exclude<Game.Tools.Temp>()))
  using(var chunks=query.ToArchetypeChunkArray(Allocator.Temp)){
   query.CompleteDependency();int total=query.CalculateEntityCount(),stride=Math.Max(1,(int)Math.Ceiling(total/4096.0)),index=0,count=0,members=0;
   var buffers=GetBufferTypeHandle<HouseholdCitizen>(true);
   foreach(var chunk in chunks){var citizens=chunk.GetBufferAccessor(ref buffers);int first=(stride-index%stride)%stride;for(int i=first;i<chunk.Count;i+=stride){if(citizens[i].Length==0)continue;members+=citizens[i].Length;count++;}index+=chunk.Count;}
   HouseholdAverage=count>0?(double)members/count:0;Revision++;
  }
 }
}
}
