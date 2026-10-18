using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using HybridCPU.Compiler.Cil;
internal static class CalliHeaderSmoke { public static void Run() {
var a=new PersistedAssemblyBuilder(new AssemblyName("CalliHeader"),typeof(object).Assembly);
var t=a.DefineDynamicModule("f").DefineType("Fixture",TypeAttributes.Public);
var m=t.DefineMethod("Read",MethodAttributes.Public|MethodAttributes.Static,typeof(int),[]);
var il=m.GetILGenerator();
il.EmitCalli(OpCodes.Calli,CallingConventions.Standard,typeof(void),Type.EmptyTypes,null);
il.Emit(OpCodes.Ldc_I4_1);
il.Emit(OpCodes.Ret);
t.CreateType();
using var s=new MemoryStream();
a.Save(s);
var image=s.ToArray();
using var pe=new PEReader(new MemoryStream(image));
var md=pe.GetMetadataReader();
var sig=md.GetStandaloneSignature(MetadataTokens.StandaloneSignatureHandle(1));
if (md.MethodDefinitions.Any(handle => md.GetMethodDefinition(handle).Signature == sig.Signature))
    throw new Exception("Calli fixture must not alias a MethodDef signature blob.");
var blob=md.GetBlobBytes(sig.Signature);
int start=pe.PEHeaders.MetadataStartOffset;
int version=BitConverter.ToInt32(image,start+12);
int pos=start+16+((version+3)&~3);
int streams=BitConverter.ToUInt16(image,pos+2);
pos+=4;
int heap=0;
for(int n=0;
n<streams;
n++){int offset=BitConverter.ToInt32(image,pos);
pos+=8;
int end=Array.IndexOf(image,(byte)0,pos);
string name=System.Text.Encoding.ASCII.GetString(image,pos,end-pos);
pos=(end+4)&~3;
if(name=="#Blob")heap=start+offset;
}int header=heap+MetadataTokens.GetHeapOffset(sig.Signature)+1;
if(image[header]!=0)throw new Exception("header fixture drift");
var importer=new RestrictedCilImporterV1(mode:RestrictedCilImportModeV1.ScalarControlFlowV2);
foreach(byte value in new byte[]{0,0x80,0x81,0xa0}) {
var bytes=(byte[])image.Clone();
bytes[header]=value;
var first=importer.ImportImage(bytes,new("Fixture","Read"));
var second=importer.ImportImage(bytes,new("Fixture","Read"));
if(first.Program is not null || first.Status!=(value==0?RestrictedCilImportStatusV1.Unsupported:RestrictedCilImportStatusV1.InvalidInput) || first.Diagnostics.Count!=1 || first.Diagnostics[0].Code!=(value==0?"HCCIL1610":"HCCIL1615") || !first.Diagnostics.SequenceEqual(second.Diagnostics))throw new Exception("Calli header admission drift");
}
if(importer.ImportImage(image,new("Fixture","Read")).Diagnostics[0].Code!="HCCIL1610")throw new Exception("Reuse drift");
var budget=new RestrictedCilImporterV1(RestrictedCilImportBudgetsV1.Production with {MaximumPeBytes=image.Length-1},mode:RestrictedCilImportModeV1.ScalarControlFlowV2);
image[header]=0x80;
if(budget.ImportImage(image,new("Fixture","Read")).Diagnostics[0].Code!="HCCIL2001")throw new Exception("Budget precedence drift");
Console.WriteLine("PASS real PE calli reserved-header admission, missing-binding precedence, deterministic rejection/reuse and PE budget");
} }
