using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Colossal.Serialization.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Unity.Entities;
namespace ApartmentPlan {
// Optional companion buffer: existing SavedPlan/PlanShape byte order is unchanged.
public struct PlanEditByte:IBufferElementData,ISerializable {
 public int Value;
 public void Serialize<T>(T w) where T:IWriter {w.Write(Value);}
 public void Deserialize<T>(T r) where T:IReader {r.Read(out Value);}
}
public sealed class PlanEditData {
 public PlanEditData Pending;public List<Plan> AppliedPlans;
 public string Name;
 public List<VillaConnection> VillaConnections=new List<VillaConnection>();public int EditingNumber=-1;
 public int Version=1;public List<Point> Boundary,Entrances;public List<ManualZone> Zones;public List<Parcel> Ramps;
 public Plan Draft;public string[] Selected;public float Elevation;public bool NativeCommitted;
 public List<ManualRoad> Roads=new List<ManualRoad>();
 public Dictionary<string,object> Settings;
}
sealed class PlanFieldContract:DefaultContractResolver {
 protected override List<MemberInfo> GetSerializableMembers(Type t)=>base.GetSerializableMembers(t).Where(m=>m is FieldInfo).ToList();
}
public static class PlanEditCodec {
 public static readonly JsonSerializerSettings Settings=new JsonSerializerSettings{ContractResolver=new PlanFieldContract(),MaxDepth=64};
 public static byte[] Encode(PlanEditData data)=>Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data,Settings));
 public static PlanEditData Decode(byte[] bytes)=>JsonConvert.DeserializeObject<PlanEditData>(Encoding.UTF8.GetString(bytes),Settings);
}
}
