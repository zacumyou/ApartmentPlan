using System;
using System.Linq;
using System.Collections.Generic;
using Game.Prefabs;
using Unity.Entities;
using Unity.Collections;
namespace ApartmentPlan {
public partial class PlanSystem {
 internal bool Tower=>Mod.Options.PlanMode==2;
 internal readonly List<TowerSurfaceChoice> TowerSurfaces=new List<TowerSurfaceChoice>();
 internal sealed class TowerSurfaceChoice {public string Id,NameKey;}
 void ScanTowerSurfaces(){TowerSurfaces.Clear();var prefabs=World.GetOrCreateSystemManaged<PrefabSystem>();var ui=World.GetOrCreateSystemManaged<Game.UI.InGame.PrefabUISystem>();using(var q=EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SurfaceData>(),ComponentType.ReadOnly<AreaData>()))using(var es=q.ToEntityArray(Allocator.Temp))foreach(var e in es){if(!prefabs.TryGetPrefab<PrefabBase>(e,out var p)||p==null||!EntityManager.GetComponentData<AreaData>(e).m_Archetype.Valid)continue;ui.GetTitleAndDescription(e,out var key,out _);TowerSurfaces.Add(new TowerSurfaceChoice{Id=p.name,NameKey=key});}}
 object TowerState()=>new{rules=TowerRules.Parse(Mod.Options.TowerRulesJson).State(),parking=Mod.Options.TowerParkingAsset,surface=Mod.Options.TowerSurface,entranceSurface=Mod.Options.TowerEntranceSurface,paving=Mod.Options.TowerEntrancePaving,facility=Mod.Options.TowerFacility,turn=Mod.Options.TowerTurnStyle,surfaces=TowerSurfaces};
 bool TowerCommand(string command,string value){if(!command.StartsWith("tower",StringComparison.Ordinal))return false;if(!Tower||NativeCommitted)return true;
 switch(command){case "towerRule":var parts=value.Split('|');if(parts.Length!=2||!double.TryParse(parts[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var number))return true;var rules=TowerRules.Parse(Mod.Options.TowerRulesJson);if(!rules.Set(parts[0],number))return true;Mod.Options.TowerRulesJson=rules.Encode();break;case "towerParking":if(!Assets.Any(a=>a.Id==value))return true;Mod.Options.TowerParkingAsset=value;break;
 case "towerSurface":case "towerEntranceSurface":if(!TowerSurfaces.Any(a=>a.Id==value))return true;if(command=="towerSurface")Mod.Options.TowerSurface=value;else Mod.Options.TowerEntranceSurface=value;break;
 case "towerPaving":Mod.Options.TowerEntrancePaving=!Mod.Options.TowerEntrancePaving;break;
 case "towerFacility":Mod.Options.TowerFacility=!Mod.Options.TowerFacility;break;
 case "towerTurn":if(!int.TryParse(value,out int n)||n<0||n>2)return true;Mod.Options.TowerTurnStyle=n;break;default:return true;}
 Mod.Options.ApplyAndSave();Rebuild();Revision++;return true;}
}
}
