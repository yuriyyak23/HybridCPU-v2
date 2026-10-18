using HybridCPU.Compiler.Cil;
using HybridCPU.Compiler.Roslyn;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Target;
using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

internal static class NullableInt32Checks
{
    public static void RunParameter(bool presence = false, int? explicitDefault = null, bool dynamicDefault = false, string? computedDefault = null)
    {
        dynamicDefault |= computedDefault is not null;
        string directory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        ReadOnlyMemory<byte>[] references = [File.ReadAllBytes(typeof(object).Assembly.Location),
            File.ReadAllBytes(Path.Combine(directory,"System.Runtime.dll")), File.ReadAllBytes(Path.Combine(directory,"netstandard.dll")),
            File.ReadAllBytes(typeof(HybridCPU.Compiler.Profiles.HybridCpuKernelNoHeapAttribute).Assembly.Location)];
        foreach(string initializer in explicitDefault is not null && explicitDefault != -7 ? new[]{"input","null"} : new[]{"input","null","input==0?null:input","unchecked(input+1)"}) {
            string source="using HybridCPU.Compiler.Profiles; public static class Fixture { private static long Primitive(long bits)=>bits; private static long Consume(int? value)=>(long)value.GetValueOrDefault(); [HybridCpuKernelNoHeap] public static long Read(int input) { int? value="+initializer+"; int? copy=value; return Consume(copy); } }";
            if (presence) source=source.Replace("(long)value.GetValueOrDefault()", "value.HasValue?1L:0L");
            if(explicitDefault is not null) source=source.Replace("GetValueOrDefault()", "GetValueOrDefault("+explicitDefault.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)+")");
            if(dynamicDefault) source=source.Replace("Primitive(long bits)","Primitive(long bits,int fallback)").Replace("Consume(int? value)","Consume(int? value,int fallback)").Replace("GetValueOrDefault()","GetValueOrDefault(fallback)").Replace("Read(int input)","Read(int input,int fallback)").Replace("Consume(copy)","Consume(copy,fallback)");
            if(computedDefault is not null)source=source.Replace("GetValueOrDefault(fallback)","GetValueOrDefault("+computedDefault+")");
            if(computedDefault=="Adjust(fallback)")source=source.Replace("public static class Fixture {","public static class Fixture { private static int Adjust(int fallback)=>unchecked(fallback+1);");
            string getter=dynamicDefault?"(long)value.GetValueOrDefault("+(computedDefault??"fallback")+")":presence?"value.HasValue?1L:0L":explicitDefault is null?"(long)value.GetValueOrDefault()":"(long)value.GetValueOrDefault("+explicitDefault.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)+")";
            string parameters=dynamicDefault?"int? value,int fallback":"int? value";
            var request=new CompanionSourceRequestV1("NullableParameter",[new("/src/Parameter.cs",source)],references);
            ManagedSourceBodyWorldResultV1 Compile(CompanionSourceRequestV1 input,int calls=100,int? stack=null,int? blocks=null)=>ManagedSourceCompanionV1.CompileBodyWorld(input,
                new RestrictedCilImporterV1(mode:RestrictedCilImportModeV1.ScalarControlFlowV2,budgets:RestrictedCilImportBudgetsV1.Production with{MaximumCalls=calls,MaximumEvaluationStack=stack??RestrictedCilImportBudgetsV1.Production.MaximumEvaluationStack,MaximumBasicBlocks=blocks??RestrictedCilImportBudgetsV1.Production.MaximumBasicBlocks}),[new("Fixture","Read")],null,[0]);
            var actual=Compile(request);var repeat=Compile(request);
            if((presence || explicitDefault is not null || dynamicDefault) && actual.Source.Pe is not null) {
                using var emitted=new PEReader(new MemoryStream(actual.Source.Pe)); var metadata=emitted.GetMetadataReader();
                var helper=metadata.GetMethodDefinition(metadata.MethodDefinitions.Single(h=>metadata.GetString(metadata.GetMethodDefinition(h).Name)=="Consume"));
                Console.WriteLine("NULLABLE-GETTER-PARAMETER-IL "+getter+" "+Convert.ToHexString(emitted.GetMethodBody(helper.RelativeVirtualAddress).GetILBytes()!));
            }
            if(actual.Source.Pe is null||actual.Compilation?.Status!=RestrictedCilImportStatusV1.Success)
                throw new Exception("Nullable parameter: "+string.Join(';',actual.Source.Diagnostics)+"; "+string.Join(';',actual.Compilation?.Diagnostics??[]));
            ScalarControlFlowV2LinkedProgramV1 Link(ManagedSourceBodyWorldResultV1 value)=>new ScalarControlFlowV2ObjectLinkerV1().Link(value.Compilation!,value.Compilation!.Graph!.RootIdentities.Single());
            var image=Link(actual);var again=Link(repeat);
            var scalar=Compile(request with{Documents=[new("/src/Parameter.cs",source.Replace("Consume("+parameters+")=>"+getter,"Consume("+(dynamicDefault?"long value,int fallback":"long value")+")=>"+(dynamicDefault?"(value & 1L)!=0?(long)(int)(value >>> 1):(long)("+(computedDefault??"fallback")+")":presence?"(value & 1L)!=0?1L:0L":explicitDefault is null?"(long)(int)(value >>> 1)":"(value & 1L)!=0?(long)(int)(value >>> 1):"+explicitDefault.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)+"L"))
                .Replace("int? value="+initializer+"; int? copy=value;", "long value="+(initializer=="null"?"0L":initializer=="input==0?null:input"?"input==0?0L:(((long)input<<32)>>>31)|1L":"(((long)("+initializer+")<<32)>>>31)|1L")+"; long copy=value;"))]});
            // Scalar I8 mask constants add conv.i8 expansions absent from bool getter IL.
            // Presence equivalence is evaluated independently on serialized objects in ManagedPortSmoke.
            if(scalar.Compilation?.Status!=RestrictedCilImportStatusV1.Success || Link(scalar).Status!=ScalarControlFlowV2LinkStatusV1.Success || !presence && explicitDefault is null && !dynamicDefault && image.MethodObjects.Single(m=>m.MethodIdentity.Contains(".Consume",StringComparison.Ordinal)).CodeSha256!=
                Link(scalar).MethodObjects.Single(m=>m.MethodIdentity.Contains(".Consume",StringComparison.Ordinal)).CodeSha256) {
                Console.WriteLine("SCALAR-STATUS "+scalar.Compilation?.Status+" "+string.Join(';',scalar.Compilation?.Diagnostics??[]));
                foreach(var compilation in new[]{actual.Compilation,scalar.Compilation})if(compilation?.Status==RestrictedCilImportStatusV1.Success)
                    foreach(var instruction in compilation.Methods.Single(m=>m.Identity.MethodName=="Consume").Import.Program!.Instructions)
                        Console.WriteLine("PARAMETER-IR "+instruction.Opcode+" "+string.Join(',',instruction.Operands)+" "+string.Join(',',instruction.Annotation.Defs));
                throw new Exception("Nullable parameter independent scalar helper bytes");
            }
            if(image.Status!=ScalarControlFlowV2LinkStatusV1.Success||!image.RestrictedImage!.PackageBytes.SequenceEqual(again.RestrictedImage!.PackageBytes)||
                !actual.Source.Pe.SequenceEqual(repeat.Source.Pe!)||!actual.Source.Pdb!.SequenceEqual(repeat.Source.Pdb!)||actual.SourceMaps.Any(p=>repeat.SourceMaps[p.Key].Digest!=p.Value.Digest))throw new Exception("Nullable parameter repeat/maps/image");
            var host=System.Reflection.Assembly.Load(actual.Source.Pe).GetType("Fixture")!.GetMethod("Read")!;
            foreach(int input in new[]{int.MinValue,-1,0,1,int.MaxValue}) foreach(int fallback in dynamicDefault?new[]{int.MinValue,-7,0,int.MaxValue}:new[]{0}) {
                long expected=initializer=="null"||initializer.Contains("input==0")&&input==0?0L:initializer.Contains("unchecked")?(long)unchecked(input+1):(long)input;
                if(presence)expected=initializer=="null"||initializer.Contains("input==0")&&input==0?0L:1L;
                if(explicitDefault is not null && (initializer=="null"||initializer.Contains("input==0")&&input==0)) expected=explicitDefault.Value;
                if(dynamicDefault && (initializer=="null"||initializer.Contains("input==0")&&input==0))expected=fallback;
                if(computedDefault is not null && (initializer=="null"||initializer.Contains("input==0")&&input==0))expected=computedDefault switch {
                    "unchecked(fallback+1)"=>unchecked(fallback+1),
                    "Adjust(fallback)"=>unchecked(fallback+1),
                    "fallback==0?-7:unchecked(fallback+1)"=>fallback==0?-7:unchecked(fallback+1),
                    "(fallback ^ -7) & 255"=>(fallback ^ -7)&255,
                    _=>throw new Exception("Missing independent computed default oracle") };
                if(!Equals(host.Invoke(null,dynamicDefault?[input,fallback]:[input]),expected))throw new Exception("Nullable parameter independent host boundary oracle");
            }
            if(initializer!="input")continue;
            foreach(string helper in new[]{"public static long Consume(int? value)=>(long)value.GetValueOrDefault();",
                "private static long Consume(int? value) { value=null; return (long)value.GetValueOrDefault(); }",
                "private static long Consume(int? value) { System.GC.KeepAlive(value); return value.GetValueOrDefault(); }",
                "private static long Consume(int? value) { try { return value.GetValueOrDefault(); } finally { System.GC.KeepAlive(value); } }",
                "private static long Consume(int? value) => value.Value;",
                "private static long Consume(int? value) => value.HasValue?7L:3L;",
                "private static long Consume(long? value)=>value.GetValueOrDefault();"}) {
                string deniedHelper=presence?helper.Replace("value.GetValueOrDefault()","(value.HasValue?1:0)"):explicitDefault is null?helper:helper.Replace("GetValueOrDefault()","GetValueOrDefault("+explicitDefault.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)+")");
                if(dynamicDefault)deniedHelper=deniedHelper.Replace("Consume(int? value)","Consume(int? value,int fallback)").Replace("Consume(long? value)","Consume(long? value,int fallback)").Replace("GetValueOrDefault()","GetValueOrDefault(fallback)");
                var denied=Compile(request with{Documents=[new("/src/Parameter.cs",source.Replace("private static long Consume("+parameters+")=>"+getter+";",deniedHelper).Replace("[HybridCpuKernelNoHeap]",""))]});
                if(denied.Compilation?.Status==RestrictedCilImportStatusV1.Success)throw new Exception("Nullable parameter signature/body gate: "+helper);
            }
            var root=new RestrictedCilImporterV1(mode:RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule,actual.Source.Pe,"nullable-root",[new("Fixture","Consume")],[],MetadataOnlyModules:[new(references[0],"core")]));
            if(dynamicDefault)foreach(string bad in new[]{source.Replace(getter,"(long)value.GetValueOrDefault(fallback=1)"),source.Replace("Consume(int? value,int fallback)","Consume(int? value,long fallback)").Replace(getter,"(long)value.GetValueOrDefault((int)("+(computedDefault??"fallback")+"))")}) {
                var denied=Compile(request with{Documents=[new("/src/Parameter.cs",bad.Replace("[HybridCpuKernelNoHeap]",""))]});
                if(denied.Compilation?.Status==RestrictedCilImportStatusV1.Success)throw new Exception("Nullable fallback mutation/width gate");
            }
            if(computedDefault is not null)foreach(string bad in new[]{"(long)value.GetValueOrDefault(10/fallback)","(long)value.GetValueOrDefault(checked(fallback+1))","(long)value.GetValueOrDefault(System.Math.Abs(fallback))"}) {
                var denied=Compile(request with{Documents=[new("/src/Parameter.cs",source.Replace(getter,bad).Replace("[HybridCpuKernelNoHeap]",""))]});
                if(denied.Compilation?.Status==RestrictedCilImportStatusV1.Success)throw new Exception("Nullable computed default fault/call proof gate");
            }
            if(computedDefault=="Adjust(fallback)")foreach(string badHelper in new[]{
                "private static int Adjust(int fallback) { System.GC.KeepAlive(fallback); return fallback; }",
                "private static int Adjust(int fallback) { try { return fallback; } finally { System.GC.KeepAlive(fallback); } }",
                "private static int Adjust(int fallback) { while(fallback>0)fallback--; return fallback; }",
                "private static int Adjust(int fallback)=>Adjust(fallback);",
                "private static int counter; private static int Adjust(int fallback)=>counter++;"}) {
                var denied=Compile(request with{Documents=[new("/src/Parameter.cs",source.Replace("private static int Adjust(int fallback)=>unchecked(fallback+1);",badHelper).Replace("[HybridCpuKernelNoHeap]",""))]});
                if(denied.Source.Pe is null||denied.Compilation?.Status==RestrictedCilImportStatusV1.Success)throw new Exception("Nullable helper default effect/EH/loop/cycle/memory gate");
            }
            if(root.Status==RestrictedCilImportStatusV1.Success)throw new Exception("Nullable private ingress became a root ABI");
            byte[] changed=actual.Source.Pe.ToArray();
            using(var pe=new System.Reflection.PortableExecutable.PEReader(new MemoryStream(changed))) {
                var md=pe.GetMetadataReader();
                var read=md.MethodDefinitions.Single(h=>md.GetString(md.GetMethodDefinition(h).Name)=="Read");
                int consume=System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(md.MethodDefinitions.Single(h=>md.GetString(md.GetMethodDefinition(h).Name)=="Consume"));
                int primitive=System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(md.MethodDefinitions.Single(h=>md.GetString(md.GetMethodDefinition(h).Name)=="Primitive"));
                int rva=md.GetMethodDefinition(read).RelativeVirtualAddress;var il=pe.GetMethodBody(rva).GetILBytes()!;
                int call=Enumerable.Range(0,il.Length-4).Single(i=>il[i]==0x28&&System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(i+1,4))==consume);
                var section=pe.PEHeaders.SectionHeaders.Single(s=>rva>=s.VirtualAddress&&rva<s.VirtualAddress+s.VirtualSize);
                int start=section.PointerToRawData+rva-section.VirtualAddress;
                int header=(changed[start]&3)==2?1:(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(changed.AsSpan(start,2))>>12)*4;
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(changed.AsSpan(start+header+call+1,4),primitive);
            }
            var mismatched=new RestrictedCilImporterV1(mode:RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule,changed,"nullable-primitive-negative",[new("Fixture","Read")],[],MetadataOnlyModules:[new(references[0],"core")]));
            if(mismatched.Status==RestrictedCilImportStatusV1.Success)throw new Exception("Packed nullable escaped into primitive I8 helper");
            foreach(int calls in new[]{0,1,2}) {
                var budgeted=Compile(request,calls);
                if(budgeted.Compilation?.Status!=(calls==2?RestrictedCilImportStatusV1.Success:RestrictedCilImportStatusV1.BudgetExhausted))throw new Exception("Nullable parameter original calls "+calls+": "+budgeted.Compilation?.Status+"; "+string.Join(';',budgeted.Compilation?.Diagnostics??[]));
            }
            int requiredStack=computedDefault is null||computedDefault=="Adjust(fallback)"?2:3;
            if(explicitDefault is not null || dynamicDefault)foreach(int stack in new[]{requiredStack-1,requiredStack}) {
                var budgeted=Compile(request,stack:stack);
                if(budgeted.Compilation?.Status!=(stack==requiredStack?RestrictedCilImportStatusV1.Success:RestrictedCilImportStatusV1.BudgetExhausted))throw new Exception("Nullable parameter erased receiver/default original MaxStack");
            }
            if(computedDefault?.Contains('?')==true && Compile(request,blocks:1).Compilation?.Status!=RestrictedCilImportStatusV1.BudgetExhausted)
                throw new Exception("Nullable computed default CFG proof budget precedence");
        }
        Console.WriteLine("PASS private exact Nullable<int> parameter/"+(computedDefault is not null?"computed-default "+computedDefault:dynamicDefault?"dynamic-default":presence?"HasValue":explicitDefault is null?"GetValueOrDefault":"explicit-default "+explicitDefault)+" packed ingress, NoHeap source/image/host/repeat/maps and signature/body/root/budget controls");
    }
    public static void Run()
    {
        RunParameter();
        RunParameter(presence:true);
        RunParameter(explicitDefault:-7);
        RunParameter(explicitDefault:int.MinValue);
        RunParameter(explicitDefault:int.MaxValue);
        RunParameter(dynamicDefault:true);
        RunComputedParameters();
        RunParameter(computedDefault:"Adjust(fallback)");
        ReadOnlyMemory<byte>[] references = [File.ReadAllBytes(typeof(object).Assembly.Location)];
        foreach (var (body, oracle) in new[] {
            ("int? n = text?.Length; return n ?? 0;", "return text == null ? 0 : text.Length;"),
            ("int? n = input < 0 ? null : input; return n ?? -7;", "return input < 0 ? -7 : input;"),
            ("int? n = input; int? copy = n; return copy.GetValueOrDefault();", "return input;"),
            ("int? n = null; if (input != 0) n = input; return n.HasValue ? n.GetValueOrDefault() : -7;", "return input == 0 ? -7 : input;"),
            ("int? n = input; if (input < 0) n = null; return n ?? 0;", "return input < 0 ? 0 : input;") }) {
            CompanionSourceRequestV1 Request(string expression) => new("NullableLocal", [new("/src/Nullable.cs", "public static class Fixture { public static int Read(string text, int input) { " + expression + " } }")], references);
            ManagedSourceBodyWorldResultV1 Compile(CompanionSourceRequestV1 request) => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = Compile(Request(body)); var scalar = Compile(Request(oracle));
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success || scalar.Compilation?.Status != RestrictedCilImportStatusV1.Success)
                throw new Exception("Nullable local source/import: " + body + "; " + string.Join(';', actual.Source.Diagnostics) + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success) throw new Exception("Nullable local link: " + string.Join(';', linked.Diagnostics));
            var required = new HashSet<(HybridCpuOpcode, long)> { (HybridCpuOpcode.SLLI, 32), (HybridCpuOpcode.SRLI, 31), (HybridCpuOpcode.ORI, 1), (HybridCpuOpcode.SRLI, 1), (HybridCpuOpcode.ADDIW, 0) };
            byte[] bytes = linked.MethodObjects.Single().ObjectArtifact.Sections.Single(s => s.Name == ".text").Data;
            for (int offset = 0; offset < bytes.Length; offset += HybridCpuBundleSerializer.BundleSizeBytes) {
                var bundle = new HybridCpuInstructionBundle();
                if (!bundle.TryReadBytes(bytes, offset)) throw new Exception("Nullable serialized bundle");
                for (int slot = 0; slot < HybridCpuInstructionBundle.SlotCount; slot++) {
                    var word = bundle.GetInstruction(slot); var opcode = (HybridCpuOpcode)word.OpCode;
                    if (!required.Contains((opcode, word.Immediate))) continue;
                    if (slot > 3 || !HybridCpuInstructionWord.TryUnpackArchRegs(word.Word1, out byte rd, out byte rs, out _) || rd is 0 or >= 32 || rs >= 32)
                        throw new Exception("Nullable scalar encoded physical operands/ALU lane");
                    required.Remove((opcode, word.Immediate));
                }
            }
            if (required.Count != 0) throw new Exception("Nullable packed scalar encoding lost exact shifts/flag/word extension: " + string.Join(';', required));
            var actualMethod = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            var scalarMethod = System.Reflection.Assembly.Load(scalar.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (string? text in new[] { null, "", "\0\u8000\uffff" }) foreach (int input in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
                if (!Equals(actualMethod.Invoke(null, [text, input]), scalarMethod.Invoke(null, [text, input]))) throw new Exception("Nullable independent scalar host oracle");
            if (body.StartsWith("int? n = text", StringComparison.Ordinal)) {
                foreach (int mutation in new[] { 0, 1, 2, 3, 4 }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        if (mutation == 0) {
                            var type = md.GetTypeReference(md.TypeReferences.Single(h => md.GetString(md.GetTypeReference(h).Name) == "Nullable`1"));
                            int name = pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.String) + MetadataTokens.GetHeapOffset(type.Name);
                            changed[name] = (byte)'X';
                        } else if (mutation == 1) {
                            var member = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                            int blob = pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(member.Signature);
                            changed[blob + 1 + 2] = 0x0a;
                        } else {
                            var method = md.GetMethodDefinition(md.MethodDefinitions.Single(h => md.GetString(md.GetMethodDefinition(h).Name) == "Read"));
                            var ilBody = pe.GetMethodBody(method.RelativeVirtualAddress);
                            if (mutation == 2) {
                                var local = md.GetStandaloneSignature(ilBody.LocalSignature).Signature;
                                byte[] signature = md.GetBlobBytes(local);
                                int item = Array.LastIndexOf(signature, (byte)8);
                                if (item < 0) throw new Exception("Nullable local mutation fixture");
                                int blob = pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(local);
                                changed[blob + 1 + item] = 9;
                            } else if (mutation == 3) {
                                byte[] il = ilBody.GetILBytes()!;
                                int address = Enumerable.Range(0, il.Length - 2).First(i => il[i] == 0x12 && il[i + 2] == 0xfe);
                                var section = pe.PEHeaders.SectionHeaders.Single(s => method.RelativeVirtualAddress >= s.VirtualAddress && method.RelativeVirtualAddress < s.VirtualAddress + s.VirtualSize);
                                int start = section.PointerToRawData + method.RelativeVirtualAddress - section.VirtualAddress;
                                int header = (changed[start] & 3) == 2 ? 1 : (BinaryPrimitives.ReadUInt16LittleEndian(changed.AsSpan(start, 2)) >> 12) * 4;
                                changed[start + header + address + 1] = 255;
                            } else {
                                byte[] il = ilBody.GetILBytes()!;
                                int address = Enumerable.Range(0, il.Length - 2).Single(i => il[i] == 0x12 && il[i + 2] == 0x28);
                                var section = pe.PEHeaders.SectionHeaders.Single(s => method.RelativeVirtualAddress >= s.VirtualAddress && method.RelativeVirtualAddress < s.VirtualAddress + s.VirtualSize);
                                int start = section.PointerToRawData + method.RelativeVirtualAddress - section.VirtualAddress;
                                int header = (changed[start] & 3) == 2 ? 1 : (BinaryPrimitives.ReadUInt16LittleEndian(changed.AsSpan(start, 2)) >> 12) * 4;
                                new byte[] { 0x11, il[address + 1], 0x6a, 0x69, 0, 0, 0 }.CopyTo(changed, start + header + address);
                            }
                        }
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable name/signature/carrier/local-index negative");
                    if (mutation == 4 && !denied.Diagnostics.Any(d => d.Code == "HCCIL1845")) throw new Exception("Nullable carrier escaped as primitive integer");
                }
                foreach (int limit in new[] { 0, 1, 2, 3 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(Request(body), new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = limit }), [new("Fixture", "Read")], null, [0]);
                    if (limit == 3 ? budgeted.Compilation?.Status != RestrictedCilImportStatusV1.Success :
                        budgeted.Compilation?.Status != RestrictedCilImportStatusV1.BudgetExhausted)
                        throw new Exception("Nullable operations bypassed original call budget boundary: " + limit);
                }
            }
        }
        foreach (string body in new[] {
            "System.DayOfWeek? n = (System.DayOfWeek)input; return (int)n.GetValueOrDefault();",
            "int? n = input; object boxed = n; return boxed == null ? 0 : 1;",
            "try { int? n = input; return n ?? 0; } finally { input = input + 1; }" }) {
            var denied = ManagedSourceCompanionV1.CompileBodyWorld(new("NullableDenied", [new("/src/Nullable.cs",
                "public static class Fixture { public static int Read(string text, int input) { " + body + " } }")], references),
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            if (denied.Source.Pe is null || denied.Compilation?.Status == RestrictedCilImportStatusV1.Success || (denied.Compilation?.Diagnostics.Count ?? 0) == 0)
                throw new Exception("Nullable value/I8/boxing/EH gate widened: " + body);
        }
        Console.WriteLine("PASS exact Nullable<int> local source/CFG/image and independent scalar host boundary oracle; malformed/opaque/budget/value/I8/boxing/EH controls");
        foreach (var (body, oracle) in new[] {
            ("long? n = input < 0 ? input : other; return n ?? -7L;", "return input < 0 ? input : other;"),
            ("long? n = input; long? copy = n; return copy.GetValueOrDefault();", "return input;"),
            ("long? n = null; if (input != 0) n = input; return n.HasValue ? n.GetValueOrDefault() : -7L;", "return input == 0 ? -7L : input;"),
            ("long? n = input; if (input < 0) n = null; return n.GetValueOrDefault(other);", "return input < 0 ? other : input;"),
            ("long? n = input < 0 ? null : input; return n ?? -7L;", "return input < 0 ? -7L : input;"),
            ("long? n = input < 0 ? null : input; return n.GetValueOrDefault(long.MinValue);", "return input < 0 ? long.MinValue : input;"),
            ("long? n = text == null ? null : (long)text.Length; return n ?? 0L;", "return text == null ? 0L : (long)text.Length;") }) {
            CompanionSourceRequestV1 RequestWide(string expression) => new("NullableWide", [new("/src/Nullable.cs", "public static class Fixture { public static long Read(string text, long input, long other) { " + expression + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileWide(string expression) => ManagedSourceCompanionV1.CompileBodyWorld(RequestWide(expression),
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileWide(body); var scalar = CompileWide(oracle);
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success || scalar.Compilation?.Status != RestrictedCilImportStatusV1.Success) {
                if (actual.Source.Pe is { } emitted) {
                    using var pe = new PEReader(new MemoryStream(emitted)); var md = pe.GetMetadataReader();
                    var failedMethod = md.GetMethodDefinition(md.MethodDefinitions.Single(h => md.GetString(md.GetMethodDefinition(h).Name) == "Read"));
                    Console.WriteLine("NULLABLE-I8-FAIL-IL=" + Convert.ToHexString(pe.GetMethodBody(failedMethod.RelativeVirtualAddress).GetILBytes()!));
                    foreach (var h in md.MemberReferences) { var m = md.GetMemberReference(h); Console.WriteLine("MEMBER " + md.GetString(m.Name) + "=" + Convert.ToHexString(md.GetBlobBytes(m.Signature))); }
                }
                throw new Exception("Nullable I8 import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            }
            var image = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            if (image.Status != ScalarControlFlowV2LinkStatusV1.Success) throw new Exception("Nullable I8 link: " + string.Join(';', image.Diagnostics));
            var method = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            var expected = System.Reflection.Assembly.Load(scalar.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (string? text in new[] { null, "", "\0\u8000\uffff" }) foreach (long input in new[] { long.MinValue, (long)int.MinValue - 1, -1L, 0, 1, (long)int.MaxValue + 1, long.MaxValue })
                foreach (long other in new[] { long.MinValue, -7L, 0, long.MaxValue })
                    if (!Equals(method.Invoke(null, [text, input, other]), expected.Invoke(null, [text, input, other]))) throw new Exception("Nullable I8 independent host oracle");
            Console.WriteLine("NULLABLE-I8-SOURCE " + body);
            if (body.StartsWith("long? n = input; long? copy", StringComparison.Ordinal)) {
                foreach (int mutation in new[] { 0, 1, 2, 3 }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        if (mutation == 0) {
                            var type = md.GetTypeReference(md.TypeReferences.Single(h => md.GetString(md.GetTypeReference(h).Name) == "Nullable`1"));
                            changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.String) + MetadataTokens.GetHeapOffset(type.Name)] = (byte)'X';
                        } else if (mutation == 1) {
                            var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                            changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(getter.Signature) + 1 + 2] = 8;
                        } else {
                            var definition = md.GetMethodDefinition(md.MethodDefinitions.Single(h => md.GetString(md.GetMethodDefinition(h).Name) == "Read"));
                            var ilBody = pe.GetMethodBody(definition.RelativeVirtualAddress);
                            if (mutation == 2) {
                                var signature = md.GetStandaloneSignature(ilBody.LocalSignature).Signature;
                                byte[] declaration = md.GetBlobBytes(signature); int item = Array.LastIndexOf(declaration, (byte)0x0a);
                                if (item < 0) throw new Exception("Nullable I8 local mutation fixture");
                                changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(signature) + 1 + item] = 8;
                            } else {
                                byte[] il = ilBody.GetILBytes()!;
                                int getter = Enumerable.Range(0, il.Length - 6).Last(i => il[i] == 0x12 && il[i + 2] == 0x28);
                                var section = pe.PEHeaders.SectionHeaders.Single(s => definition.RelativeVirtualAddress >= s.VirtualAddress && definition.RelativeVirtualAddress < s.VirtualAddress + s.VirtualSize);
                                int start = section.PointerToRawData + definition.RelativeVirtualAddress - section.VirtualAddress;
                                int header = (changed[start] & 3) == 2 ? 1 : (BinaryPrimitives.ReadUInt16LittleEndian(changed.AsSpan(start, 2)) >> 12) * 4;
                                new byte[] { 0x11, il[getter + 1], 0x6a, 0, 0, 0, 0 }.CopyTo(changed, start + header + getter);
                            }
                        }
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-wide-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    Console.WriteLine("NULLABLE-I8-NEGATIVE mutation=" + mutation + "; status=" + denied.Status + "; codes=" + string.Join(';', denied.Diagnostics.Select(d => d.Code)));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0 || mutation == 3 && !denied.Diagnostics.Any(d => d.Code == "HCCIL1845"))
                        throw new Exception("Nullable I8 wrong nominal/signature/carrier/opaque gate");
                }
                foreach (int limit in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(RequestWide(body), new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = limit }), [new("Fixture", "Read")], null, [0]);
                    if (limit == 2 ? budgeted.Compilation?.Status != RestrictedCilImportStatusV1.Success : budgeted.Compilation?.Status != RestrictedCilImportStatusV1.BudgetExhausted)
                        throw new Exception("Nullable I8 original call budget boundary");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<long> locals full-I8/presence SSA, conditional/copy/reassignment/accessor/default source/image and independent scalar host oracle");
        foreach (string width in new[] { "int", "long" }) foreach (bool conditional in new[] { false, true })
            foreach (string argument in new[] { "input + 1", "input < 0 ? input - 7 : input + 9", "input / (input - input)", "input += 1" }) {
            string body = width + "? n = " + (conditional ? "input < 0 ? null : input" : "input") +
                "; " + width + " result = n.GetValueOrDefault(" + argument + "); return result ^ input;";
            string oracle = width + " original = input; bool present = " + (conditional ? "input >= 0" : "true") +
                "; " + width + " fallback = " + (argument == "input += 1" ? "(input += 1)" : argument) + "; return (present ? original : fallback) ^ input;";
            CompanionSourceRequestV1 RequestComputed(string text) => new("ComputedNullable", [new("/src/Computed.cs",
                "public static class Fixture { public static " + width + " Read(" + width + " input) { " + text + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileComputed(CompanionSourceRequestV1 request, int calls = 256) => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                    budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
            var actual = CompileComputed(RequestComputed(body)); var scalar = CompileComputed(RequestComputed(oracle));
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success || scalar.Compilation?.Status != RestrictedCilImportStatusV1.Success)
                throw new Exception("Computed nullable import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var again = CompileComputed(RequestComputed(body));
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) ||
                !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) || !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) ||
                actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest)) throw new Exception("Computed nullable repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            var expected = System.Reflection.Assembly.Load(scalar.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            static object? Outcome(System.Reflection.MethodInfo method, object input) {
                try { return method.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { return fault.InnerException!.GetType(); }
            }
            foreach (long boundary in new[] { long.MinValue, (long)int.MinValue - 1, -1L, 0, 1, (long)int.MaxValue + 1, long.MaxValue }) {
                object input = width == "int" ? (object)unchecked((int)boundary) : boundary;
                if (!Equals(Outcome(host, input), Outcome(expected, input))) throw new Exception("Computed nullable eager result/fault oracle");
            }
            if (!conditional && argument == "input + 1") {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = CompileComputed(RequestComputed(body), calls);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Computed nullable call budget " + calls);
                }
                byte[] changed = actual.Source.Pe!.ToArray();
                using (var pe = new PEReader(new MemoryStream(changed))) {
                    var md = pe.GetMetadataReader();
                    var member = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                    changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(member.Signature) + 1 + 4] = 8;
                }
                var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                    ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "computed-nullable-signature", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Computed nullable exact signature gate");
            }
        }
        Console.WriteLine("PASS computed nullable default scalar/CFG/eager argument stores/fault oracle, repeat PE/PDB/maps/image and exact signature/call budget controls");
        foreach (string width in new[] { "int", "long" }) {
            var request = new CompanionSourceRequestV1("ComputedNullableBudget", [new("/src/Budget.cs",
                "public static class Fixture { public static " + width + " Read(string text, " + width + " input) { " + width +
                "? n = input; return n.GetValueOrDefault((" + width + ")text.Length + 1); } }")], references);
            foreach (int calls in new[] { 0, 1, 2, 3 }) {
                var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                    budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                if (budgeted.Compilation?.Status != (calls == 3 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                    throw new Exception("Computed nullable helper proof budget precedence: " + calls + "; " + string.Join(';', budgeted.Compilation?.Diagnostics ?? []));
            }
        }
        foreach (string width in new[] { "int", "long" }) foreach (bool conditional in new[] { false, true }) {
            string source = "public static class Fixture { public static " + width + " Read(" + width + " input) { " + width + "? n = " +
                (conditional ? "input < 0 ? null : input" : "input") + "; return n.Value; } }";
            var request = new CompanionSourceRequestV1("NullableValue", [new("/src/Value.cs", source)], references);
            ManagedSourceBodyWorldResultV1 CompileValue() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileValue(); var repeat = CompileValue();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Checked nullable import: " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var image = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeatedImage = new ScalarControlFlowV2ObjectLinkerV1().Link(repeat.Compilation!, repeat.Compilation!.Graph!.RootIdentities.Single());
            if (image.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Compilation.Methods.Single().Import.RequiresNativeExceptionTransfer ||
                !actual.Source.Pe!.SequenceEqual(repeat.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(repeat.Source.Pdb!) ||
                !image.RestrictedImage!.PackageBytes.SequenceEqual(repeatedImage.RestrictedImage!.PackageBytes) ||
                actual.SourceMaps.Any(p => repeat.SourceMaps[p.Key].Digest != p.Value.Digest)) throw new Exception("Checked nullable image/repeat: " + string.Join(';', image.Diagnostics));
            var method = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (long boundary in new[] { long.MinValue, (long)int.MinValue - 1, -1L, 0, 1, (long)int.MaxValue + 1, long.MaxValue }) {
                object input = width == "int" ? (object)unchecked((int)boundary) : boundary;
                object? outcome;
                try { outcome = method.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                bool missing = conditional && (width == "int" ? (int)input < 0 : (long)input < 0);
                if (!Equals(outcome, missing ? typeof(InvalidOperationException) : input)) throw new Exception("Checked nullable scalar result/fault oracle");
            }
        }
        Console.WriteLine("PASS checked Nullable<int/long>.Value result/InvalidOperationException source/image/native-EH and repeated PE/PDB/maps/image");
        foreach (string width in new[] { "int", "long" }) foreach (bool outerPresent in new[] { false, true })
            foreach (string getter in new[] { "fallback.Value", "fallback.GetValueOrDefault()", "fallback.HasValue ? fallback.Value + 1 : 7",
                "fallback.GetValueOrDefault(input + 1)", "fallback.GetValueOrDefault(fallback.GetValueOrDefault(input + 1))" }) {
            string body = width + "? length = " + (outerPresent ? "input" : "null") + "; " + width +
                "? fallback = input < 0 ? null : input; return length.GetValueOrDefault(" + getter + ");";
            var request = new CompanionSourceRequestV1("NestedNullable", [new("/src/Nested.cs", "public static class Fixture { public static " + width +
                " Read(" + width + " input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileNested(int calls = 256, int stack = 256) => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                    budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls, MaximumEvaluationStack = stack }), [new("Fixture", "Read")], null, [0]);
            var actual = CompileNested(); var again = CompileNested();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nested nullable import: " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) ||
                !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) || !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) ||
                actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest)) throw new Exception("Nested nullable repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (long boundary in new[] { long.MinValue, (long)int.MinValue - 1, -1L, 0, 1, (long)int.MaxValue + 1, long.MaxValue }) {
                object input = width == "int" ? (object)unchecked((int)boundary) : boundary;
                long scalar = width == "int" ? (int)input : (long)input;
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected;
                if (scalar < 0 && getter == "fallback.Value") expected = typeof(InvalidOperationException);
                else {
                    long fallback = getter.StartsWith("fallback.HasValue") ? scalar < 0 ? 7 : unchecked(scalar + 1) :
                        scalar < 0 ? getter.Contains("input + 1") ? unchecked(scalar + 1) : 0 : scalar;
                    long result = outerPresent ? scalar : fallback;
                    expected = width == "int" ? (object)unchecked((int)result) : result;
                }
                if (!Equals(outcome, expected)) throw new Exception("Nested nullable independent eager result/fault oracle");
            }
            if (outerPresent && getter == "fallback.Value") {
                foreach (int calls in new[] { 0, 1, 2, 3, 4 }) {
                    var budgeted = CompileNested(calls);
                    if (budgeted.Compilation?.Status != (calls == 4 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nested nullable original call budget " + calls);
                }
            }
            if (outerPresent && getter.Contains("input + 1")) {
                int callsRequired = getter.Contains("fallback.GetValueOrDefault(fallback.") ? 5 : 4;
                foreach (int calls in new[] { 0, callsRequired - 1, callsRequired }) {
                    var budgeted = CompileNested(calls);
                    if (budgeted.Compilation?.Status != (calls == callsRequired ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nested computed nullable original call budget " + calls);
                }
                int originalStack = host.GetMethodBody()!.MaxStackSize;
                foreach (int stack in new[] { originalStack - 1, originalStack }) {
                    var budgeted = CompileNested(stack: stack);
                    if (budgeted.Compilation?.Status != (stack == originalStack ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nested computed nullable original MaxStack boundary " + stack);
                }
            }
        }
        Console.WriteLine("PASS nested nullable leaf default arguments independent present/empty/eager Value fault oracle, repeat/maps/image and original call budgets");
        foreach (string argument in new[] { "input + 1", "input < 0 ? input - 7 : input + 9", "input += 1", "input / (input - input)" }) {
            var request = new CompanionSourceRequestV1("NullableComputedConstructor", [new("/src/Initializer.cs",
                "public static class Fixture { public static long Read(int input) { int? length = " + argument + "; return (long)length.GetValueOrDefault() + input; } }")], references);
            ManagedSourceBodyWorldResultV1 CompileCtor(int calls = 256, int stack = 256) => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                    budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls, MaximumEvaluationStack = stack }), [new("Fixture", "Read")], null, [0]);
            var actual = CompileCtor(); var again = CompileCtor();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Computed I4 nullable constructor: " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Computed I4 nullable constructor repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (int input in new[] { int.MinValue, -1, 0, 1, int.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                int result = argument.Contains('?') ? input < 0 ? unchecked(input - 7) : unchecked(input + 9) : unchecked(input + 1);
                object expected = argument.Contains('/') ? typeof(DivideByZeroException) : (long)result + (argument.Contains("+=") ? result : input);
                if (!Equals(outcome, expected)) throw new Exception("Computed I4 nullable constructor independent host oracle");
            }
            if (argument == "input + 1") {
                foreach (int calls in new[] { 0, 1, 2 }) if (CompileCtor(calls).Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                    throw new Exception("Computed I4 nullable original call budget");
                int maxStack = host.GetMethodBody()!.MaxStackSize;
                foreach (int stack in new[] { maxStack - 1, maxStack }) if (CompileCtor(stack: stack).Compilation?.Status != (stack == maxStack ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                    throw new Exception("Computed I4 nullable original MaxStack");
            }
        }
        Console.WriteLine("PASS computed Nullable<int> in-place initializer arithmetic/forward CFG/eager stores/division faults, original budgets and repeat PE/PDB/maps/image");
        foreach (string body in new[] {
            "uint? n = input; uint? copy = n; return (long)copy.GetValueOrDefault();",
            "uint? n = input + 1U; return (long)n.GetValueOrDefault();",
            "uint? n = input == 0 ? null : input; return (long)n.GetValueOrDefault(0x80000000U);",
            "uint? n = null; if (input != 0) n = input; return (long)n.GetValueOrDefault(input + 1U);",
            "uint? n = input == 0 ? null : input; return (long)n.Value;" }) {
            var request = new CompanionSourceRequestV1("NullableUnsigned", [new("/src/Unsigned.cs",
                "public static class Fixture { public static long Read(uint input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable uint import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable uint repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (uint input in new uint[] { 0, 1, 0x7fffffffU, 0x80000000U, uint.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input == 0 ? typeof(InvalidOperationException) :
                    body.Contains("n = input + 1U") ? (long)unchecked(input + 1U) :
                    input == 0 && body.Contains("0x80000000U") ? 0x80000000L :
                    input == 0 && body.Contains("if (input != 0)") ? 1L : (long)input;
                if (!Equals(outcome, expected)) throw new Exception("Nullable uint independent zero-extension/host fault oracle");
            }
            if (body.StartsWith("uint? n = input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable uint original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = 8;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-u4-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable uint nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<uint> local/copy/join/initializer/default/Value full U4 payload and zero-extension, host boundary/fault and repeat/maps/image");
        foreach (string body in new[] {
            "ulong? n = input; ulong? copy = n; return unchecked((long)copy.GetValueOrDefault());",
            "ulong? n = input + 1UL; return unchecked((long)n.GetValueOrDefault());",
            "ulong? n = input == 0 ? null : input; return unchecked((long)n.GetValueOrDefault(0x8000000000000000UL));",
            "ulong? n = null; if (input != 0) n = input; return unchecked((long)n.GetValueOrDefault(input + 1UL));",
            "ulong? n = input == 0 ? null : input; return unchecked((long)n.Value);" }) {
            var request = new CompanionSourceRequestV1("NullableUnsignedWide", [new("/src/UnsignedWide.cs",
                "public static class Fixture { public static long Read(ulong input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileWideUnsigned(int calls = 256) => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                    budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
            var actual = CompileWideUnsigned(); var again = CompileWideUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable ulong import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable ulong repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (ulong input in new ulong[] { 0, 1, 0x7fffffffUL, 0x80000000UL, 0x7fffffffffffffffUL, 0x8000000000000000UL, ulong.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input == 0 ? typeof(InvalidOperationException) :
                    unchecked((long)(body.Contains("n = input + 1UL") ? input + 1UL : input == 0 && body.Contains("0x8000000000000000UL") ? 0x8000000000000000UL :
                    input == 0 && body.Contains("if (input != 0)") ? 1UL : input));
                if (!Equals(outcome, expected)) throw new Exception("Nullable ulong independent full-bit/host fault oracle");
            }
            if (body.StartsWith("ulong? n = input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) if (CompileWideUnsigned(calls).Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                    throw new Exception("Nullable ulong original call budget");
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + (wrongNominal ? bytes.Length - 1 : 2)] = 0x0a;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-u8-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable ulong nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<ulong> local/copy/join/initializer/default/Value full U8/presence, host boundary/fault, nominal/signature/budget and repeat/maps/image");
        foreach (string body in new[] {
            "ushort? n = input; ushort? copy = n; return (long)copy.GetValueOrDefault();",
            "ushort? n = unchecked((ushort)(input + 1)); return (long)n.GetValueOrDefault();",
            "ushort? n = input == 0 ? null : input; return (long)n.GetValueOrDefault(0x8000);",
            "ushort? n = null; if (input != 0) n = input; return (long)n.GetValueOrDefault(unchecked((ushort)(input + 1)));",
            "ushort? n = input == 0 ? null : input; return (long)n.Value;" }) {
            var request = new CompanionSourceRequestV1("NullableUnsignedNarrow", [new("/src/Narrow.cs",
                "public static class Fixture { public static long Read(ushort input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable ushort import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable ushort repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (ushort input in new ushort[] { 0, 1, 0x7fff, 0x8000, ushort.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input == 0 ? typeof(InvalidOperationException) :
                    body.Contains("n = unchecked((ushort)(input + 1))") ? (long)unchecked(unchecked((ushort)(input + 1))) :
                    input == 0 && body.Contains("0x8000") ? 0x8000L :
                    input == 0 && body.Contains("if (input != 0)") ? 1L : (long)input;
                if (!Equals(outcome, expected)) throw new Exception("Nullable ushort independent zero-extension/host fault oracle");
            }
            if (body.StartsWith("ushort? n = input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable ushort original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = 6;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-u2-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable ushort nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<ushort> local/copy/join/initializer/default/Value bounded U2 payload and zero-extension, host boundary/fault and repeat/maps/image");
        foreach (string body in new[] {
            "short? n = input; short? copy = n; return (long)copy.GetValueOrDefault();",
            "short? n = unchecked((short)(input + 1)); return (long)n.GetValueOrDefault();",
            "short? n = input < 0 ? null : input; return (long)n.GetValueOrDefault(short.MinValue);",
            "short? n = null; if (input != 0) n = input; return (long)n.GetValueOrDefault(unchecked((short)(input + 1)));",
            "short? n = input < 0 ? null : input; return (long)n.Value;" }) {
            var request = new CompanionSourceRequestV1("NullableSignedNarrow", [new("/src/SignedNarrow.cs",
                "public static class Fixture { public static long Read(short input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable short import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable short repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (short input in new short[] { short.MinValue, -256, -1, 0, 1, 255, short.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input < 0 ? typeof(InvalidOperationException) :
                    body.Contains("n = unchecked((short)(input + 1))") ? (long)unchecked(unchecked((short)(input + 1))) :
                    input < 0 && body.Contains("short.MinValue") ? (long)short.MinValue :
                    input == 0 && body.Contains("if (input != 0)") ? 1L : (long)input;
                if (!Equals(outcome, expected)) throw new Exception("Nullable short independent zero-extension/host fault oracle");
            }
            if (body.StartsWith("short? n = input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable short original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = 7;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-i2-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable short nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<short> local/copy/join/initializer/default/Value bounded I2 payload and sign-extension, host boundary/fault and repeat/maps/image");
        foreach (string body in new[] {
            "byte? n = input; byte? copy = n; return (long)copy.GetValueOrDefault();",
            "byte? n = unchecked((byte)(input + 1)); return (long)n.GetValueOrDefault();",
            "byte? n = input == 0 ? null : input; return (long)n.GetValueOrDefault(0x80);",
            "byte? n = null; if (input != 0) n = input; return (long)n.GetValueOrDefault(unchecked((byte)(input + 1)));",
            "byte? n = input == 0 ? null : input; return (long)n.Value;" }) {
            var request = new CompanionSourceRequestV1("NullableUnsignedByte", [new("/src/Byte.cs",
                "public static class Fixture { public static long Read(byte input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable byte import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable byte repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (byte input in new byte[] { 0, 1, 0x7f, 0x80, byte.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input == 0 ? typeof(InvalidOperationException) :
                    body.Contains("n = unchecked((byte)(input + 1))") ? (long)unchecked(unchecked((byte)(input + 1))) :
                    input == 0 && body.Contains("0x80") ? 0x80L :
                    input == 0 && body.Contains("if (input != 0)") ? 1L : (long)input;
                if (!Equals(outcome, expected)) throw new Exception("Nullable byte independent zero-extension/host fault oracle");
            }
            if (body.StartsWith("byte? n = input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable byte original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = 4;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-u1-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable byte nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<byte> local/copy/join/initializer/default/Value bounded U1 payload and zero-extension, host boundary/fault and repeat/maps/image");
        foreach (string body in new[] {
            "sbyte? n = input; sbyte? copy = n; return (long)copy.GetValueOrDefault();",
            "sbyte? n = unchecked((sbyte)(input + 1)); return (long)n.GetValueOrDefault();",
            "sbyte? n = input < 0 ? null : input; return (long)n.GetValueOrDefault(sbyte.MinValue);",
            "sbyte? n = null; if (input != 0) n = input; return (long)n.GetValueOrDefault(unchecked((sbyte)(input + 1)));",
            "sbyte? n = input < 0 ? null : input; return (long)n.Value;" }) {
            var request = new CompanionSourceRequestV1("NullableSignedByte", [new("/src/SignedByte.cs",
                "public static class Fixture { public static long Read(sbyte input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable sbyte import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable sbyte repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (sbyte input in new sbyte[] { sbyte.MinValue, -127, -1, 0, 1, 126, sbyte.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input < 0 ? typeof(InvalidOperationException) :
                    body.Contains("n = unchecked((sbyte)(input + 1))") ? (long)unchecked(unchecked((sbyte)(input + 1))) :
                    input < 0 && body.Contains("sbyte.MinValue") ? (long)sbyte.MinValue :
                    input == 0 && body.Contains("if (input != 0)") ? 1L : (long)input;
                if (!Equals(outcome, expected)) throw new Exception("Nullable sbyte independent sign-extension/host fault oracle");
            }
            if (body.StartsWith("sbyte? n = input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable sbyte original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = 5;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-i1-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable sbyte nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<sbyte> local/copy/join/initializer/default/Value bounded I1 payload and sign-extension, host boundary/fault and repeat/maps/image");
        foreach (string body in new[] {
            "char? n = input; char? copy = n; return (long)copy.GetValueOrDefault();",
            "char? n = unchecked((char)(input + 1)); return (long)n.GetValueOrDefault();",
            "char? n = input == 0 ? null : input; return (long)n.GetValueOrDefault((char)0x8000);",
            "char? n = null; if (input != 0) n = input; return (long)n.GetValueOrDefault(unchecked((char)(input + 1)));",
            "char? n = input == 0 ? null : input; return (long)n.Value;" }) {
            var request = new CompanionSourceRequestV1("NullableCharacter", [new("/src/Character.cs",
                "public static class Fixture { public static long Read(char input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable char import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable char repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (char input in new char[] { '\0', '\u0001', '\u7fff', '\u8000', '\ud800', '\udfff', char.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input == 0 ? typeof(InvalidOperationException) :
                    body.Contains("n = unchecked((char)(input + 1))") ? (long)unchecked(unchecked((char)(input + 1))) :
                    input == 0 && body.Contains("0x8000") ? 0x8000L :
                    input == 0 && body.Contains("if (input != 0)") ? 1L : (long)input;
                if (!Equals(outcome, expected)) throw new Exception("Nullable char independent zero-extension/host fault oracle");
            }
            if (body.StartsWith("char? n = input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable char original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = 7;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-char-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable char nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<char> local/copy/join/initializer/default/Value bounded Char/U2 payload and zero-extension, host boundary/fault and repeat/maps/image");
        foreach (string body in new[] {
            "bool? n = input; bool? copy = n; return copy.GetValueOrDefault() ? 1L : 0L;",
            "bool? n = !input; return n.GetValueOrDefault() ? 1L : 0L;",
            "bool? n = input ? null : input; return n.GetValueOrDefault(true) ? 1L : 0L;",
            "bool? n = null; if (input) n = input; return n.GetValueOrDefault(!input) ? 1L : 0L;",
            "bool? n = input ? null : input; return n.Value ? 1L : 0L;" }) {
            var request = new CompanionSourceRequestV1("NullableBoolean", [new("/src/Boolean.cs",
                "public static class Fixture { public static long Read(bool input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable bool import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable bool repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (bool input in new[] { false, true }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input ? typeof(InvalidOperationException) :
                    body.Contains("n = !input") ? (!input ? 1L : 0L) :
                    body.Contains("if (input)") ? 1L : input ? 1L : 0L;
                if (!Equals(outcome, expected)) throw new Exception("Nullable bool independent logical/host fault oracle");
            }
            if (body.StartsWith("bool? n = input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable bool original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = 5;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-bool-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable bool nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<bool> local/copy/join/initializer/default/Value Boolean nominal and CLI byte payload, host logical/fault and repeat/maps/image");
        foreach (string body in new[] {
            "Choice? n = (Choice)input; Choice? copy = n; return (long)copy.GetValueOrDefault();",
            "Choice? n = unchecked((Choice)(input + 1)); return (long)n.GetValueOrDefault();",
            "Choice? n = input < 0 ? null : (Choice)input; return (long)n.GetValueOrDefault((Choice)int.MinValue);",
            "Choice? n = null; if (input != 0) n = (Choice)input; return (long)n.GetValueOrDefault(unchecked((Choice)(input + 1)));",
            "Choice? n = input < 0 ? null : (Choice)input; return (long)n.Value;" }) {
            var request = new CompanionSourceRequestV1("NullableEnumInt32", [new("/src/EnumInt32.cs",
                "public enum Choice : int { Zero = 0 } public enum Other : int { Zero = 0 } public static class Fixture { public static long Read(int input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable enum import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable enum repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (int input in new int[] { int.MinValue, -256, -1, 0, 1, 255, int.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input < 0 ? typeof(InvalidOperationException) :
                    body.Contains("n = unchecked((Choice)(input + 1))") ? (long)unchecked(input + 1) :
                    input < 0 && body.Contains("int.MinValue") ? (long)int.MinValue :
                    input == 0 && body.Contains("if (input != 0)") ? 1L : (long)input;
                if (!Equals(outcome, expected)) throw new Exception("Nullable enum independent zero-extension/host fault oracle");
            }
            if (body.StartsWith("Choice? n = (Choice)input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable enum original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = wrongNominal
                            ? checked((byte)(MetadataTokens.GetRowNumber(md.TypeDefinitions.Single(h => md.GetString(md.GetTypeDefinition(h).Name) == "Other")) << 2))
                            : (byte)7;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-enum-i4-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable enum nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<Choice> local/copy/join/initializer/default/Value bounded I4 payload and sign-extension, host boundary/fault and repeat/maps/image");
        foreach (string body in new[] {
            "WideChoice? n = (WideChoice)input; WideChoice? copy = n; return (long)copy.GetValueOrDefault();",
            "WideChoice? n = unchecked((WideChoice)(input + 1)); return (long)n.GetValueOrDefault();",
            "WideChoice? n = input < 0 ? null : (WideChoice)input; return (long)n.GetValueOrDefault((WideChoice)long.MinValue);",
            "WideChoice? n = null; if (input != 0) n = (WideChoice)input; return (long)n.GetValueOrDefault(unchecked((WideChoice)(input + 1)));",
            "WideChoice? n = input < 0 ? null : (WideChoice)input; return (long)n.Value;" }) {
            var request = new CompanionSourceRequestV1("NullableEnumInt64", [new("/src/EnumInt64.cs",
                "public enum WideChoice : long { Zero = 0 } public enum WideOther : long { Zero = 0 } public static class Fixture { public static long Read(long input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable wide enum import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable wide enum repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (long input in new long[] { long.MinValue, (long)int.MinValue - 1, -1, 0, 1, (long)int.MaxValue + 1, long.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input < 0 ? typeof(InvalidOperationException) :
                    body.Contains("n = unchecked((WideChoice)(input + 1))") ? (long)unchecked(input + 1) :
                    input < 0 && body.Contains("long.MinValue") ? (long)long.MinValue :
                    input == 0 && body.Contains("if (input != 0)") ? 1L : (long)input;
                if (!Equals(outcome, expected)) throw new Exception("Nullable wide enum independent zero-extension/host fault oracle");
            }
            if (body.StartsWith("WideChoice? n = (WideChoice)input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable wide enum original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = wrongNominal
                            ? checked((byte)(MetadataTokens.GetRowNumber(md.TypeDefinitions.Single(h => md.GetString(md.GetTypeDefinition(h).Name) == "WideOther")) << 2))
                            : (byte)7;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-enum-i8-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable wide enum nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<WideChoice> local/copy/join/initializer/default/Value bounded I8/presence and sign-extension, host boundary/fault and repeat/maps/image");
        foreach (string body in new[] {
            "UnsignedChoice? n = (UnsignedChoice)input; UnsignedChoice? copy = n; return unchecked((long)copy.GetValueOrDefault());",
            "UnsignedChoice? n = unchecked((UnsignedChoice)(input + 1UL)); return unchecked((long)n.GetValueOrDefault());",
            "UnsignedChoice? n = input == 0 ? null : (UnsignedChoice)input; return unchecked((long)n.GetValueOrDefault((UnsignedChoice)0x8000000000000000UL));",
            "UnsignedChoice? n = null; if (input != 0) n = (UnsignedChoice)input; return unchecked((long)n.GetValueOrDefault(unchecked((UnsignedChoice)(input + 1UL))));",
            "UnsignedChoice? n = input == 0 ? null : (UnsignedChoice)input; return unchecked((long)n.Value);" }) {
            var request = new CompanionSourceRequestV1("NullableEnumUInt64", [new("/src/EnumUInt64.cs",
                "public enum UnsignedChoice : ulong { Zero = 0 } public enum UnsignedOther : ulong { Zero = 0 } public static class Fixture { public static long Read(ulong input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable unsigned wide enum import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable unsigned wide enum repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (ulong input in new ulong[] { 0, 1, 0x7fffffffUL, 0x80000000UL, 0x7fffffffffffffffUL, 0x8000000000000000UL, ulong.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input == 0 ? typeof(InvalidOperationException) :
                    body.Contains("n = unchecked((UnsignedChoice)(input + 1UL))") ? unchecked((long)(input + 1UL)) :
                    input == 0 && body.Contains("0x8000000000000000UL") ? long.MinValue :
                    input == 0 && body.Contains("if (input != 0)") ? 1L : unchecked((long)input);
                if (!Equals(outcome, expected)) throw new Exception("Nullable unsigned wide enum independent full-width host/fault oracle");
            }
            if (body.StartsWith("UnsignedChoice? n = (UnsignedChoice)input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable unsigned wide enum original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = wrongNominal
                            ? checked((byte)(MetadataTokens.GetRowNumber(md.TypeDefinitions.Single(h => md.GetString(md.GetTypeDefinition(h).Name) == "UnsignedOther")) << 2))
                            : (byte)7;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-enum-u8-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable unsigned wide enum nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<UnsignedChoice> local/copy/join/initializer/default/Value bounded U8/presence and explicit widening, host boundary/fault and repeat/maps/image");
        foreach (string body in new[] {
            "WordChoice? n = (WordChoice)input; WordChoice? copy = n; return unchecked((long)copy.GetValueOrDefault());",
            "WordChoice? n = unchecked((WordChoice)(input + 1U)); return unchecked((long)n.GetValueOrDefault());",
            "WordChoice? n = input == 0 ? null : (WordChoice)input; return unchecked((long)n.GetValueOrDefault((WordChoice)0x80000000U));",
            "WordChoice? n = null; if (input != 0) n = (WordChoice)input; return unchecked((long)n.GetValueOrDefault(unchecked((WordChoice)(input + 1U))));",
            "WordChoice? n = input == 0 ? null : (WordChoice)input; return unchecked((long)n.Value);" }) {
            var request = new CompanionSourceRequestV1("NullableEnumUInt32", [new("/src/EnumUInt32.cs",
                "public enum WordChoice : uint { Zero = 0 } public enum WordOther : uint { Zero = 0 } public static class Fixture { public static long Read(uint input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable unsigned word enum import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable unsigned word enum repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (uint input in new uint[] { 0, 1, 0x7fffffffU, 0x80000000U, uint.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input == 0 ? typeof(InvalidOperationException) :
                    body.Contains("n = unchecked((WordChoice)(input + 1U))") ? unchecked((long)(input + 1U)) :
                    input == 0 && body.Contains("0x80000000U") ? 0x80000000L :
                    input == 0 && body.Contains("if (input != 0)") ? 1L : unchecked((long)input);
                if (!Equals(outcome, expected)) throw new Exception("Nullable unsigned word enum independent full-width host/fault oracle");
            }
            if (body.StartsWith("WordChoice? n = (WordChoice)input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable unsigned word enum original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = wrongNominal
                            ? checked((byte)(MetadataTokens.GetRowNumber(md.TypeDefinitions.Single(h => md.GetString(md.GetTypeDefinition(h).Name) == "WordOther")) << 2))
                            : (byte)7;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-enum-u4-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable unsigned word enum nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<WordChoice> local/copy/join/initializer/default/Value bounded U4/zero-extension and explicit widening, host boundary/fault and repeat/maps/image");
        foreach (string body in new[] {
            "NarrowChoice? n = (NarrowChoice)input; NarrowChoice? copy = n; return unchecked((long)copy.GetValueOrDefault());",
            "NarrowChoice? n = unchecked((NarrowChoice)(input + 1)); return unchecked((long)n.GetValueOrDefault());",
            "NarrowChoice? n = input == 0 ? null : (NarrowChoice)input; return unchecked((long)n.GetValueOrDefault((NarrowChoice)0x8000));",
            "NarrowChoice? n = null; if (input != 0) n = (NarrowChoice)input; return unchecked((long)n.GetValueOrDefault(unchecked((NarrowChoice)(input + 1))));",
            "NarrowChoice? n = input == 0 ? null : (NarrowChoice)input; return unchecked((long)n.Value);" }) {
            var request = new CompanionSourceRequestV1("NullableEnumUInt16", [new("/src/EnumUInt16.cs",
                "public enum NarrowChoice : ushort { Zero = 0 } public enum NarrowOther : ushort { Zero = 0 } public static class Fixture { public static long Read(ushort input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable unsigned narrow enum import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable unsigned narrow enum repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (ushort input in new ushort[] { 0, 1, 0x7fff, 0x8000, ushort.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input == 0 ? typeof(InvalidOperationException) :
                    body.Contains("n = unchecked((NarrowChoice)(input + 1))") ? (long)unchecked((ushort)(input + 1)) :
                    input == 0 && body.Contains("0x8000") ? 0x8000L :
                    input == 0 && body.Contains("if (input != 0)") ? 1L : unchecked((long)input);
                if (!Equals(outcome, expected)) throw new Exception("Nullable unsigned narrow enum independent full-width host/fault oracle");
            }
            if (body.StartsWith("NarrowChoice? n = (NarrowChoice)input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable unsigned narrow enum original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = wrongNominal
                            ? checked((byte)(MetadataTokens.GetRowNumber(md.TypeDefinitions.Single(h => md.GetString(md.GetTypeDefinition(h).Name) == "NarrowOther")) << 2))
                            : (byte)7;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-enum-u2-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable unsigned narrow enum nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<NarrowChoice> local/copy/join/initializer/default/Value bounded U2/zero-extension and explicit widening, host boundary/fault and repeat/maps/image");
        foreach (string body in new[] {
            "SignedChoice? n = (SignedChoice)input; SignedChoice? copy = n; return unchecked((long)copy.GetValueOrDefault());",
            "SignedChoice? n = unchecked((SignedChoice)(input + 1)); return unchecked((long)n.GetValueOrDefault());",
            "SignedChoice? n = input < 0 ? null : (SignedChoice)input; return unchecked((long)n.GetValueOrDefault((SignedChoice)short.MinValue));",
            "SignedChoice? n = null; if (input != 0) n = (SignedChoice)input; return unchecked((long)n.GetValueOrDefault(unchecked((SignedChoice)(input + 1))));",
            "SignedChoice? n = input < 0 ? null : (SignedChoice)input; return unchecked((long)n.Value);" }) {
            var request = new CompanionSourceRequestV1("NullableEnumInt16", [new("/src/EnumInt16.cs",
                "public enum SignedChoice : short { Zero = 0 } public enum SignedOther : short { Zero = 0 } public static class Fixture { public static long Read(short input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable signed narrow enum import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable signed narrow enum repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (short input in new short[] { short.MinValue, -256, -1, 0, 1, 255, short.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input < 0 ? typeof(InvalidOperationException) :
                    body.Contains("n = unchecked((SignedChoice)(input + 1))") ? (long)unchecked((short)(input + 1)) :
                    input < 0 && body.Contains("short.MinValue") ? (long)short.MinValue :
                    input == 0 && body.Contains("if (input != 0)") ? 1L : unchecked((long)input);
                if (!Equals(outcome, expected)) throw new Exception("Nullable signed narrow enum independent full-width host/fault oracle");
            }
            if (body.StartsWith("SignedChoice? n = (SignedChoice)input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable signed narrow enum original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = wrongNominal
                            ? checked((byte)(MetadataTokens.GetRowNumber(md.TypeDefinitions.Single(h => md.GetString(md.GetTypeDefinition(h).Name) == "SignedOther")) << 2))
                            : (byte)7;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-enum-i2-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable signed narrow enum nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<SignedChoice> local/copy/join/initializer/default/Value bounded I2/sign-extension and explicit widening, host boundary/fault and repeat/maps/image");
        foreach (string body in new[] {
            "ByteChoice? n = (ByteChoice)input; ByteChoice? copy = n; return unchecked((long)copy.GetValueOrDefault());",
            "ByteChoice? n = unchecked((ByteChoice)(input + 1)); return unchecked((long)n.GetValueOrDefault());",
            "ByteChoice? n = input == 0 ? null : (ByteChoice)input; return unchecked((long)n.GetValueOrDefault((ByteChoice)0x80));",
            "ByteChoice? n = null; if (input != 0) n = (ByteChoice)input; return unchecked((long)n.GetValueOrDefault(unchecked((ByteChoice)(input + 1))));",
            "ByteChoice? n = input == 0 ? null : (ByteChoice)input; return unchecked((long)n.Value);" }) {
            var request = new CompanionSourceRequestV1("NullableEnumUInt8", [new("/src/EnumUInt8.cs",
                "public enum ByteChoice : byte { Zero = 0 } public enum ByteOther : byte { Zero = 0 } public static class Fixture { public static long Read(byte input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable byte enum import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable byte enum repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (byte input in new byte[] { 0, 1, 0x7f, 0x80, byte.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input == 0 ? typeof(InvalidOperationException) :
                    body.Contains("n = unchecked((ByteChoice)(input + 1))") ? (long)unchecked((byte)(input + 1)) :
                    input == 0 && body.Contains("0x80") ? 0x80L :
                    input == 0 && body.Contains("if (input != 0)") ? 1L : unchecked((long)input);
                if (!Equals(outcome, expected)) throw new Exception("Nullable byte enum independent full-width host/fault oracle");
            }
            if (body.StartsWith("ByteChoice? n = (ByteChoice)input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable byte enum original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = wrongNominal
                            ? checked((byte)(MetadataTokens.GetRowNumber(md.TypeDefinitions.Single(h => md.GetString(md.GetTypeDefinition(h).Name) == "ByteOther")) << 2))
                            : (byte)7;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-enum-u1-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable byte enum nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<ByteChoice> local/copy/join/initializer/default/Value bounded U1/zero-extension and explicit widening, host boundary/fault and repeat/maps/image");
        foreach (string body in new[] {
            "SByteChoice? n = (SByteChoice)input; SByteChoice? copy = n; return unchecked((long)copy.GetValueOrDefault());",
            "SByteChoice? n = unchecked((SByteChoice)(input + 1)); return unchecked((long)n.GetValueOrDefault());",
            "SByteChoice? n = input < 0 ? null : (SByteChoice)input; return unchecked((long)n.GetValueOrDefault((SByteChoice)sbyte.MinValue));",
            "SByteChoice? n = null; if (input != 0) n = (SByteChoice)input; return unchecked((long)n.GetValueOrDefault(unchecked((SByteChoice)(input + 1))));",
            "SByteChoice? n = input < 0 ? null : (SByteChoice)input; return unchecked((long)n.Value);" }) {
            var request = new CompanionSourceRequestV1("NullableEnumInt8", [new("/src/EnumInt8.cs",
                "public enum SByteChoice : sbyte { Zero = 0 } public enum SByteOther : sbyte { Zero = 0 } public static class Fixture { public static long Read(sbyte input) { " + body + " } }")], references);
            ManagedSourceBodyWorldResultV1 CompileUnsigned() => ManagedSourceCompanionV1.CompileBodyWorld(request,
                new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2), [new("Fixture", "Read")], null, [0]);
            var actual = CompileUnsigned(); var again = CompileUnsigned();
            if (actual.Compilation?.Status != RestrictedCilImportStatusV1.Success) throw new Exception("Nullable signed byte enum import: " + body + "; " + string.Join(';', actual.Compilation?.Diagnostics ?? []));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(actual.Compilation, actual.Compilation.Graph!.RootIdentities.Single());
            var repeated = new ScalarControlFlowV2ObjectLinkerV1().Link(again.Compilation!, again.Compilation!.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success || !actual.Source.Pe!.SequenceEqual(again.Source.Pe!) || !actual.Source.Pdb!.SequenceEqual(again.Source.Pdb!) ||
                !linked.RestrictedImage!.PackageBytes.SequenceEqual(repeated.RestrictedImage!.PackageBytes) || actual.SourceMaps.Any(p => again.SourceMaps[p.Key].Digest != p.Value.Digest))
                throw new Exception("Nullable signed byte enum repeat/image");
            var host = System.Reflection.Assembly.Load(actual.Source.Pe!).GetType("Fixture")!.GetMethod("Read")!;
            foreach (sbyte input in new sbyte[] { sbyte.MinValue, -127, -1, 0, 1, 126, sbyte.MaxValue }) {
                object? outcome;
                try { outcome = host.Invoke(null, [input]); } catch (System.Reflection.TargetInvocationException fault) { outcome = fault.InnerException!.GetType(); }
                object expected = body.Contains("n.Value") && input < 0 ? typeof(InvalidOperationException) :
                    body.Contains("n = unchecked((SByteChoice)(input + 1))") ? (long)unchecked((sbyte)(input + 1)) :
                    input < 0 && body.Contains("sbyte.MinValue") ? (long)sbyte.MinValue :
                    input == 0 && body.Contains("if (input != 0)") ? 1L : unchecked((long)input);
                if (!Equals(outcome, expected)) throw new Exception("Nullable signed byte enum independent full-width host/fault oracle");
            }
            if (body.StartsWith("SByteChoice? n = (SByteChoice)input;")) {
                foreach (int calls in new[] { 0, 1, 2 }) {
                    var budgeted = ManagedSourceCompanionV1.CompileBodyWorld(request, new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2,
                        budgets: RestrictedCilImportBudgetsV1.Production with { MaximumCalls = calls }), [new("Fixture", "Read")], null, [0]);
                    if (budgeted.Compilation?.Status != (calls == 2 ? RestrictedCilImportStatusV1.Success : RestrictedCilImportStatusV1.BudgetExhausted))
                        throw new Exception("Nullable signed byte enum original call budget");
                }
                foreach (bool wrongNominal in new[] { false, true }) {
                    byte[] changed = actual.Source.Pe!.ToArray();
                    using (var pe = new PEReader(new MemoryStream(changed))) {
                        var md = pe.GetMetadataReader();
                        var getter = md.GetMemberReference(md.MemberReferences.Single(h => md.GetString(md.GetMemberReference(h).Name) == "GetValueOrDefault"));
                        BlobHandle blob = wrongNominal ? md.GetTypeSpecification((TypeSpecificationHandle)getter.Parent).Signature : getter.Signature;
                        byte[] bytes = md.GetBlobBytes(blob);
                        int index = wrongNominal ? bytes.Length - 1 : 2;
                        changed[pe.PEHeaders.MetadataStartOffset + md.GetHeapMetadataOffset(HeapIndex.Blob) + MetadataTokens.GetHeapOffset(blob) + 1 + index] = wrongNominal
                            ? checked((byte)(MetadataTokens.GetRowNumber(md.TypeDefinitions.Single(h => md.GetString(md.GetTypeDefinition(h).Name) == "SByteOther")) << 2))
                            : (byte)7;
                    }
                    var denied = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                        ManagedBodyWorldModeV1.StandaloneRestrictedModule, changed, "nullable-enum-i1-negative", [new("Fixture", "Read")], [], MetadataOnlyModules: [new(references[0], "core")]));
                    if (denied.Status == RestrictedCilImportStatusV1.Success || denied.Diagnostics.Count == 0) throw new Exception("Nullable signed byte enum nominal/signature gate");
                }
            }
        }
        Console.WriteLine("PASS exact Nullable<SByteChoice> local/copy/join/initializer/default/Value bounded I1/sign-extension and explicit widening, host boundary/fault and repeat/maps/image");
    }
    public static void RunComputedParameters()
    {
        foreach(string expression in new[]{"unchecked(fallback+1)","fallback==0?-7:unchecked(fallback+1)","(fallback ^ -7) & 255"})RunParameter(computedDefault:expression);
    }
}
