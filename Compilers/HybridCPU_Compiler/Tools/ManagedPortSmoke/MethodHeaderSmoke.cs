using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using HybridCPU.Compiler.Cil;
internal static class MethodHeaderSmoke { public static void Run() {
var a = new PersistedAssemblyBuilder(new AssemblyName("MethodHeader"), typeof(object).Assembly);
var t = a.DefineDynamicModule("fixture").DefineType("Fixture", TypeAttributes.Public);
var m = t.DefineMethod("Read", MethodAttributes.Public | MethodAttributes.Static, typeof(int), []);
var il = m.GetILGenerator(); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Ret);
t.CreateType(); using var stream = new MemoryStream(); a.Save(stream);
byte[] image = stream.ToArray();
using var pe = new PEReader(new MemoryStream(image)); var md = pe.GetMetadataReader();
var method = md.GetMethodDefinition(md.MethodDefinitions.Single(h => md.GetString(md.GetMethodDefinition(h).Name) == "Read"));
int start = pe.PEHeaders.MetadataStartOffset;
int version = BitConverter.ToInt32(image, start + 12);
int pos = start + 16 + ((version + 3) & ~3), streams = BitConverter.ToUInt16(image, start + 16 + ((version + 3) & ~3) + 2);
pos += 4; int heap = 0;
for (int n = 0; n < streams; n++) {
 int offset = BitConverter.ToInt32(image, pos); pos += 8;
 int end = Array.IndexOf(image, (byte)0, pos);
 string name = System.Text.Encoding.ASCII.GetString(image, pos, end - pos);
 pos = (end + 4) & ~3; if (name == "#Blob") heap = start + offset;
}
int header = heap + MetadataTokens.GetHeapOffset(method.Signature) + 1;
if (image[header] != 0) throw new Exception("Fixture drift");

var importer = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2);
var selector = new RestrictedCilMethodSelectorV1("Fixture", "Read");
var baseline = importer.ImportImage(image, selector);
if (baseline.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Positive rejected.");
foreach (byte value in new byte[] { 0x80, 0x81, 0x90, 0xa0, 0xc0 }) {
 var bytes=(byte[])image.Clone(); bytes[header]=value;
 var first=importer.ImportImage(bytes,selector);var second=importer.ImportImage(bytes,selector);
 if(first.Status!=RestrictedCilImportStatusV1.InvalidInput || first.Program is not null || first.Diagnostics.Count!=1 || first.Diagnostics[0].Code!="HCCIL0006" || !first.Diagnostics.SequenceEqual(second.Diagnostics))throw new Exception("Reserved method header admission drift.");
 Console.WriteLine($"method-header {value:x2}: {first.Diagnostics[0].Code}");
}
var reused=importer.ImportImage(image,selector);
foreach (byte value in Enumerable.Range(1, 15).Select(value => (byte)value)) {
 var bytes=(byte[])image.Clone(); bytes[header]=value;
 var first=importer.ImportImage(bytes,selector);var second=importer.ImportImage(bytes,selector);
 bool knownConvention=value <= 5 || value == 9;
 var expected=knownConvention ? RestrictedCilImportStatusV1.Unsupported : RestrictedCilImportStatusV1.InvalidInput;
 string code=knownConvention ? "HCCIL1010" : "HCCIL0006";
 if(first.Status!=expected || first.Program is not null || first.Diagnostics.Count!=1 ||
    first.Diagnostics[0].Code!=code || !first.Diagnostics.SequenceEqual(second.Diagnostics))
    throw new Exception($"Method convention {value:x2} admission drift.");
 if(knownConvention && first.Diagnostics[0].Message!="Vararg and unmanaged call conventions are not qualified.")
    throw new Exception("Existing unsupported convention reason drift.");
 Console.WriteLine($"method-convention {value:x2}: {first.Status}/{code}");
}
reused=importer.ImportImage(image,selector);
if(reused.Status!=RestrictedCilImportStatusV1.Success || System.Text.Json.JsonSerializer.Serialize(reused.Program)!=System.Text.Json.JsonSerializer.Serialize(baseline.Program))throw new Exception("Reuse drift.");
var budget=new RestrictedCilImporterV1(RestrictedCilImportBudgetsV1.Production with {MaximumPeBytes=image.Length-1},mode:RestrictedCilImportModeV1.ScalarControlFlowV2);
image[header]=0x80;if(budget.ImportImage(image,selector).Diagnostics[0].Code!="HCCIL2001")throw new Exception("Budget drift.");
Console.WriteLine("PASS real PE method reserved-header fail-closed, positive/repeat/reuse and PE budget");
} }
