using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using HybridCPU.Compiler.Cil;
internal static class LocalSignatureCountSmoke { public static void Run() {
var a=new PersistedAssemblyBuilder(new AssemblyName("LocalCount"),typeof(object).Assembly);
var t=a.DefineDynamicModule("fixture").DefineType("Fixture",TypeAttributes.Public);
var m=t.DefineMethod("Read",MethodAttributes.Public|MethodAttributes.Static,typeof(int),[]);
var il=m.GetILGenerator();il.DeclareLocal(typeof(int));il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Stloc_0);il.Emit(OpCodes.Ldloc_0);il.Emit(OpCodes.Ret);
t.CreateType();using var stream=new MemoryStream();a.Save(stream);byte[] image=stream.ToArray();
using var pe=new PEReader(new MemoryStream(image));var md=pe.GetMetadataReader();
var method=md.GetMethodDefinition(md.MethodDefinitions.Single(h=>md.GetString(md.GetMethodDefinition(h).Name)=="Read"));
var sig=md.GetStandaloneSignature(pe.GetMethodBody(method.RelativeVirtualAddress).LocalSignature).Signature;
if(!md.GetBlobBytes(sig).SequenceEqual(new byte[]{7,1,8}))throw new Exception("Fixture drift");
int offset=pe.PEHeaders.MetadataStartOffset+md.GetHeapMetadataOffset(HeapIndex.Blob)+MetadataTokens.GetHeapOffset(sig)+2;

var selector=new RestrictedCilMethodSelectorV1("Fixture","Read");
var importer=new RestrictedCilImporterV1(mode:RestrictedCilImportModeV1.ScalarControlFlowV2);
var baseline=importer.ImportImage(image,selector);
if(baseline.Status!=RestrictedCilImportStatusV1.Success)throw new Exception("Local positive rejected");
foreach(byte count in new byte[]{2,127}) {
 var bytes=(byte[])image.Clone();bytes[offset]=count;
 var first=importer.ImportImage(bytes,selector);var repeated=importer.ImportImage(bytes,selector);
 if(first.Status!=RestrictedCilImportStatusV1.InvalidInput || first.Program is not null || first.Diagnostics.Count!=1 ||
 first.Diagnostics[0].Code!="HCCIL0029" || !first.Diagnostics.SequenceEqual(repeated.Diagnostics))throw new Exception("Local count rejection drift");
}
var reused=importer.ImportImage(image,selector);
if(reused.Status!=RestrictedCilImportStatusV1.Success || System.Text.Json.JsonSerializer.Serialize(reused.Program)!=System.Text.Json.JsonSerializer.Serialize(baseline.Program))throw new Exception("Local reuse drift");
var budget=new RestrictedCilImporterV1(RestrictedCilImportBudgetsV1.Production with {MaximumPeBytes=image.Length-1},mode:RestrictedCilImportModeV1.ScalarControlFlowV2);
image[offset]=127;if(budget.ImportImage(image,selector).Diagnostics[0].Code!="HCCIL2001")throw new Exception("Local PE budget precedence drift");
Console.WriteLine("PASS real PE local count byte admission, HCCIL0029/repeat/reuse and PE budget");
} }
