using System;
using System.IO;
using ApartmentPlan;
namespace Unity.Entities {public interface IComponentData{} public interface IBufferElementData{}}
namespace Unity.Mathematics {public struct float3 {public float x,y,z;public float3(float a,float b,float c){x=a;y=b;z=c;}}}
namespace Colossal.Serialization.Entities {
 public interface IWriter{void Write(int v);void Write(float v);void Write(Unity.Mathematics.float3 v);}
 public interface IReader{void Read(out int v);void Read(out float v);void Read(out Unity.Mathematics.float3 v);}
 public interface ISerializable{void Serialize<T>(T w)where T:IWriter;void Deserialize<T>(T r)where T:IReader;}
}
class Codec:Colossal.Serialization.Entities.IWriter,Colossal.Serialization.Entities.IReader {
 readonly MemoryStream stream=new MemoryStream();BinaryWriter w;BinaryReader r;public Codec(){w=new BinaryWriter(stream);r=new BinaryReader(stream);}public void Reset(){stream.Position=0;}
 public void Write(int v)=>w.Write(v);public void Write(float v)=>w.Write(v);public void Write(Unity.Mathematics.float3 v){Write(v.x);Write(v.y);Write(v.z);}
 public void Read(out int v){v=r.ReadInt32();}public void Read(out float v){v=r.ReadSingle();}public void Read(out Unity.Mathematics.float3 v){v=new Unity.Mathematics.float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}
 public static void Test(){var codec=new Codec();var info=new SavedPlan{Version=1,Number=3,Area=160000,Far=187.5f,Homes=900};info.Serialize(codec);var shape=new PlanShape{A=new Unity.Mathematics.float3(100,12,200),B=new Unity.Mathematics.float3(100,12,215),Width=8,Kind=1};shape.Serialize(codec);codec.Reset();var copy=new SavedPlan();copy.Deserialize(codec);var line=new PlanShape();line.Deserialize(codec);if(copy.Version!=1||copy.Number!=3||copy.Area!=160000||copy.Far!=187.5f||copy.Homes!=900||line.A.x!=100||line.A.y!=12||line.B.z!=215||line.Width!=8||line.Kind!=1)throw new Exception("Serialization field order");Console.WriteLine("PASS legacy saved schema field roundtrip (test codec, not game reload)");var current=new Codec();info.Version=2;shape.Kind=10;info.Serialize(current);shape.Serialize(current);current.Reset();copy.Deserialize(current);line.Deserialize(current);if(copy.Version!=2||line.Kind!=10)throw new Exception("invisible road save roundtrip");Console.WriteLine("PASS version 2 invisible-road shape roundtrip without changing field order");}
}
