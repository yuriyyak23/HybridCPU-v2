using System.Buffers.Binary;
using DoomSharp.HybridCpu.Windows;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Target;
using HybridCPU.Compiler.Core.Target.Link;
using HybridCPU.Compiler.Core.Target.Managed;
using HybridCPU.Compiler.Core.Target.Object;
using HybridCPU.Compiler.Core.Target.Runtime;

if(args.Length == 2 && args[0] == "test-frame")
    Environment.Exit(FramebufferRegression.Run(args[1]));
if(args.Length != 1) throw new ArgumentException("Supply a scratch directory under TempEnv.");
string root = Path.GetFullPath(args[0]);
if(!root.Split(Path.DirectorySeparatorChar).Contains("TempEnv", StringComparer.OrdinalIgnoreCase))
    throw new ArgumentException("Test files must be under TempEnv.");
Directory.CreateDirectory(root);
int checks = 0;
void Check(bool value, string message) { if(!value) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
void RejectWad(byte[] bytes, string name)
{
    string path = Path.Combine(root, name + ".wad"); File.WriteAllBytes(path, bytes);
    var result = ImageAdmission.Inspect("", path);
    Check(result.Wad is null && result.Issues.Any(i=>i.Code=="HCDOOMGUI1011") && !result.CanExecute, name);
}
byte[] wad = new byte[28];
"IWAD"u8.CopyTo(wad);
BinaryPrimitives.WriteInt32LittleEndian(wad.AsSpan(4), 1);
BinaryPrimitives.WriteInt32LittleEndian(wad.AsSpan(8), 12);
"MARKER"u8.CopyTo(wad.AsSpan(20)); // A zero-size marker at offset zero is legal.
string wadPath=Path.Combine(root,"valid.wad"); File.WriteAllBytes(wadPath,wad);
var valid=ImageAdmission.InspectWad(wadPath);
Check(valid is {LumpCount:1,Bytes:28} && valid.Sha256.Length==64,"valid WAD including zero-size marker");
var malformed=(byte[])wad.Clone(); malformed[0]=0; RejectWad(malformed,"bad magic");
malformed=(byte[])wad.Clone(); BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(4),int.MaxValue); RejectWad(malformed,"count overflow");
malformed=(byte[])wad.Clone(); BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(8),-1); RejectWad(malformed,"negative directory");
malformed=(byte[])wad.Clone(); BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(16),int.MaxValue); RejectWad(malformed,"lump exceeds file");
RejectWad(wad[..20],"truncated directory");
var missing=ImageAdmission.Inspect("", "");
Check(!missing.CanExecute && missing.Issues[0].Code=="HCDOOMGUI1001","missing image fails before execution");
Check(ImageAdmission.Inspect("game.dll",wadPath).Issues[0].Code=="HCDOOMGUI1002","no CoreCLR DLL fallback");
string imagePath=Path.Combine(root,"fixture.hcexe"); File.WriteAllBytes(imagePath,[0,1,2,3]);
Check(ImageAdmission.Inspect(imagePath,wadPath).Issues.Any(i=>i.Code=="HCSTART2003"),"real inspector rejects bad image magic");
// Build a tiny native fixture directly. This is not a Doom publish or AOT compiler run.
byte[] code = new HybridCpuBundleSerializer().SerializeProgram([new HybridCpuInstructionBundle()]);
var obj = new HybridCpuObjectWriterV1().Write(new(
    [new(".text",HybridCpuObjectSectionKind.Code,256,code,(ulong)code.Length)],
    [new("main",HybridCpuSymbolBinding.Global,HybridCpuSymbolVisibility.Default,".text",0,(ulong)code.Length,true)],
    [],HybridCpuTargetPlatformContractV1.Default.ContractDigest,HybridCpuManagedAbiFamilyV1.Default.ContractDigest));
var link=new HybridCpuStaticLinkerV1().Link([new("fixture",obj.Bytes)]);
var image=new HybridCpuRestrictedImageBuilderV1().Build(new(link,"main"));
Check(image.Status==HybridCpuStartupStatusV1.Success,"native inspector fixture construction");
File.WriteAllBytes(imagePath,image.PackageBytes);
var admitted=ImageAdmission.Inspect(imagePath,wadPath);
Check(admitted.Image?.Status==HybridCpuStartupStatusV1.Success && admitted.Wad!=null,"valid image and WAD inspection");
Check(!admitted.CanExecute && admitted.Issues.Any(i=>i.Code=="HCDOOMGUI1003") && admitted.Issues.Any(i=>i.Code=="HCDOOMGUI1100"),"valid scalar package cannot enable Doom launch");
byte[] corrupt=(byte[])image.PackageBytes.Clone(); corrupt[^1]^=1; File.WriteAllBytes(imagePath,corrupt);
Check(ImageAdmission.Inspect(imagePath,wadPath).Issues.Any(i=>i.Code=="HCSTART2006"),"checksum tampering rejected");
using var cancellation=new CancellationTokenSource(); cancellation.Cancel();
try { ImageAdmission.Inspect(imagePath,wadPath,cancellation.Token); throw new Exception("Cancellation swallowed"); }
catch(OperationCanceledException) { Check(true,"cancellation does not become a ready result"); }
Console.WriteLine($"PASS {checks} host admission checks; no guest execution.");
