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
// Independently readable fields needed by the list, hit testing and ownership checks.
public sealed class PlanEditSummary {
 public string Name;public bool NativeCommitted,Pending;public int? Buildings,Parking;
 public List<Point> Boundary;public List<ManualZone> Exclusions;
 public static PlanEditSummary From(PlanEditData d)=>d==null?null:new PlanEditSummary{
  Name=d.Name,NativeCommitted=d.NativeCommitted,Pending=d.Pending!=null,Boundary=d.Boundary,
  Exclusions=d.Zones?.Where(z=>z.Kind==5).ToList(),
  Buildings=d.AppliedPlans?.Sum(p=>p.Parcels.Count(r=>r.Kind==0)),
  Parking=d.AppliedPlans?.Sum(p=>p.Parcels.Sum(r=>r.ParkingSpaces))??d.Draft?.Parcels.Sum(r=>r.ParkingSpaces)};
}
// The negative version tag cannot be a legacy JSON byte. Pack four octets per
// existing ECS buffer word, preserving the registered city component layout.
public static class PlanArchive {
 public const int Tag=-180,Limit=2000000;
 static int Words(int bytes)=>(bytes+3)/4;
 static void Pack(int[] words,int start,byte[] bytes){
  for(int i=0;i<bytes.Length;i++)words[start+i/4]|=bytes[i]<<((i%4)*8);
 }
 static byte[] Unpack(Func<int,int> word,int start,int length){
  var bytes=new byte[length];
  for(int i=0;i<length;i+=4){
   int value=word(start+i/4);
   for(int j=0;j<4&&i+j<length;j++)bytes[i+j]=(byte)(value>>(8*j));
  }
  return bytes;
 }
 public static int[] Encode(PlanEditData data,byte[] json){
  if(json.Length>Limit)throw new InvalidOperationException("Plan edit data exceeds budget");
  var header=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(PlanEditSummary.From(data),PlanEditCodec.Settings));
  byte[] compressed;
  using(var output=new System.IO.MemoryStream()){
   using(var stream=new System.IO.Compression.GZipStream(output,System.IO.Compression.CompressionMode.Compress,true))stream.Write(json,0,json.Length);
   compressed=output.ToArray();
  }
  var words=new int[4+Words(header.Length)+Words(compressed.Length)];
  words[0]=Tag;words[1]=header.Length;words[2]=compressed.Length;words[3]=json.Length;
  Pack(words,4,header);Pack(words,4+Words(header.Length),compressed);return words;
 }
 static void Validate(Func<int,int> word,int count){
  if(count<4||word(0)!=Tag||word(1)<=0||word(1)>Limit||word(2)<=0||word(2)>Limit+4096||
     word(3)<=0||word(3)>Limit||count!=4+Words(word(1))+Words(word(2)))throw new System.IO.InvalidDataException();
 }
 public static PlanEditSummary Header(Func<int,int> word,int count){
  Validate(word,count);
  return JsonConvert.DeserializeObject<PlanEditSummary>(Encoding.UTF8.GetString(Unpack(word,4,word(1))),PlanEditCodec.Settings);
 }
 public static PlanEditData Read(Func<int,int> word,int count){
  Validate(word,count);var json=new byte[word(3)];
  using(var input=new System.IO.MemoryStream(Unpack(word,4+Words(word(1)),word(2))))
  using(var stream=new System.IO.Compression.GZipStream(input,System.IO.Compression.CompressionMode.Decompress)){
   int offset=0;
   while(offset<json.Length){int n=stream.Read(json,offset,json.Length-offset);if(n==0)throw new System.IO.EndOfStreamException();offset+=n;}
   if(stream.ReadByte()!=-1)throw new System.IO.InvalidDataException();
  }
  return PlanEditCodec.Decode(json);
 }
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
