using System;
using System.Linq;
using System.Reflection;
using Unity.Entities;
namespace ApartmentPlan {
public partial class ConstructionTool {
 // Optional integration: use Anarchy's own toggle handler, never write its backing fields.
 void EnablePreviewAnarchy(){
  try{
   var type=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name=="Anarchy").Select(a=>a.GetType("Anarchy.Systems.Common.AnarchyUISystem",false)).FirstOrDefault(t=>t!=null);
   if(type==null)return;
   var system=World.GetExistingSystemManaged(type);if(system==null)return;
   var enabled=type.GetProperty("AnarchyEnabled",BindingFlags.Public|BindingFlags.Instance);
   var add=type.GetMethod("TryAddTool",new[]{typeof(string)});
   var toggle=type.GetMethod("AnarchyToggled",BindingFlags.NonPublic|BindingFlags.Instance,null,Type.EmptyTypes,null);
   if(enabled?.PropertyType!=typeof(bool)||add==null||toggle==null||toggle.ReturnType!=typeof(void)){Trace.Event("integration.anarchy","unsupported API; unchanged");return;}
   add.Invoke(system,new object[]{toolID});
   if(!(bool)enabled.GetValue(system))toggle.Invoke(system,null);
   Trace.Event("integration.anarchy","preview enabled="+enabled.GetValue(system));
  }catch(Exception e){Trace.Failure("integration.anarchy.failed",e);}
 }
}
}
