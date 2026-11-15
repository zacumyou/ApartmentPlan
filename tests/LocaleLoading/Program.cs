using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
var assembly=Assembly.Load(File.ReadAllBytes(args[0]));
if(!string.IsNullOrEmpty(assembly.Location))throw new Exception("Expected byte-loaded assembly with empty Location");
foreach(var language in new[]{"en-US","ko-KR"}){
 using var stream=assembly.GetManifestResourceStream("ApartmentPlan.lang."+language+".json")??throw new Exception("Missing resource "+language);
 using var json=JsonDocument.Parse(stream);
 foreach(var key in new[]{"AP.Layout","AP.Display","AP.Input","AP.Draw","AP.save"})if(!json.RootElement.TryGetProperty(key,out var v)||string.IsNullOrWhiteSpace(v.GetString()))throw new Exception("Missing key "+key);
 Console.WriteLine("PASS byte-loaded DLL Location empty; embedded "+language+" readable");
}
