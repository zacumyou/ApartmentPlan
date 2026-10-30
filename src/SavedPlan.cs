using Colossal.Serialization.Entities;
using Unity.Entities;
using Unity.Mathematics;
namespace ApartmentPlan {
// Keeps compound ownership/edit metadata while removing its saved-list entry.
public struct PlanListState:IComponentData,ISerializable {
 public int Hidden;
 public void Serialize<T>(T w) where T:IWriter{w.Write(Hidden);}
 public void Deserialize<T>(T r) where T:IReader{r.Read(out Hidden);}
}
public struct PlanConstructionState:IComponentData,ISerializable {
 public int Applied;
 public void Serialize<T>(T w) where T:IWriter{w.Write(Applied);}
 public void Deserialize<T>(T r) where T:IReader{r.Read(out Applied);}
}
// Metadata entities only. No building, network, area, parking-lane or simulation components.
public struct SavedPlan:IComponentData,ISerializable {
 public int Version,Number;public float Area,Far;public int Homes;
 public void Serialize<T>(T w) where T:IWriter{w.Write(Version);w.Write(Number);w.Write(Area);w.Write(Far);w.Write(Homes);}
 public void Deserialize<T>(T r) where T:IReader{r.Read(out Version);r.Read(out Number);r.Read(out Area);r.Read(out Far);r.Read(out Homes);}
}
public struct PlanShape:IBufferElementData,ISerializable {
 public float3 A,B;public float Width;public int Kind;
 public void Serialize<T>(T w) where T:IWriter{w.Write(A);w.Write(B);w.Write(Width);w.Write(Kind);}
 public void Deserialize<T>(T r) where T:IReader{r.Read(out A);r.Read(out B);r.Read(out Width);r.Read(out Kind);}
}
}
