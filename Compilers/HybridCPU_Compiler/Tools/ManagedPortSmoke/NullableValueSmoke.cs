using HybridCPU.Compiler.Cil;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Target;
using HybridCPU.Compiler.Core.Target.Managed;
using HybridCPU.Compiler.Core.Target.Object;
using HybridCPU.Platform.Contracts;

internal static class NullableValueSmoke
{
    public static void Run()
    {
        RunParameterWords();
        RunParameterWords(presence:true);
        RunParameterWords(explicitDefault:true);
        RunParameterWords(dynamicDefault:true);
        RunParameterWords(computedDefault:true);
        RunParameterWords(computedConditional:true);
        RunHelperDefaultWords();
        var helper = HybridCpuManagedNullableValueEmitterV1.EmitObject();
        if (helper.Status != HybridCpuObjectStatusV1.Success || HybridCpuManagedAbiFamilyV1.SchemaMinor != 63 ||
            HybridCpuManagedRuntimeEcallContractV1.NullableValueInt64CheckedOperation != 40)
            throw new Exception("Nullable helper ABI identity");
        byte[] bytes = helper.Sections.Single(s => s.Name == ".text").Data;
        var words = new List<HybridCpuInstructionWord>();
        for (int offset = 0; offset < bytes.Length; offset += HybridCpuBundleSerializer.BundleSizeBytes) {
            var bundle = new HybridCpuInstructionBundle();
            if (!bundle.TryReadBytes(bytes, offset)) throw new Exception("Nullable helper serialized bundle");
            words.Add(bundle.GetInstruction(0));
        }
        bool Has(HybridCpuOpcode opcode, byte rd, byte rs, short immediate) => words.Any(word => (HybridCpuOpcode)word.OpCode == opcode &&
            HybridCpuInstructionWord.TryUnpackArchRegs(word.Word1, out byte actualRd, out byte actualRs, out _) && actualRd == rd && actualRs == rs && word.Immediate == unchecked((ushort)immediate));
        if (words.Count(w => (HybridCpuOpcode)w.OpCode == HybridCpuOpcode.ECALL) != 1 ||
            !Has(HybridCpuOpcode.ADDI, 16, 10, 0) || !Has(HybridCpuOpcode.ADDI, 18, 11, 0) ||
            !Has(HybridCpuOpcode.ADDI, 11, 0, 40) || !Has(HybridCpuOpcode.ADDI, 15, 0, 2) ||
            !Has(HybridCpuOpcode.ADDI, 2, 2, -16) || !Has(HybridCpuOpcode.ADDI, 2, 2, 16) ||
            !words.Any(w => (HybridCpuOpcode)w.OpCode == HybridCpuOpcode.BNE &&
                HybridCpuInstructionWord.TryUnpackArchRegs(w.Word1, out _, out byte rs1, out byte rs2) && rs1 == 12 && rs2 == 0))
            throw new Exception("Nullable helper physical ECALL arguments/operation/count/frame/managed-throw branch");
        byte[] pe = File.ReadAllBytes(typeof(NullableValueFixture).Assembly.Location);
        var request = new ManagedBodyWorldV1(ManagedBodyWorldModeV1.StandaloneRestrictedModule, pe, "nullable-value-smoke",
            [new(typeof(NullableValueFixture).FullName!, nameof(NullableValueFixture.Read))], [],
            MetadataOnlyModules: [new(File.ReadAllBytes(typeof(object).Assembly.Location), "core")]);
        var graph = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(request);
        if (graph.Status != RestrictedCilImportStatusV1.Success) throw new Exception(string.Join(';', graph.Diagnostics));
        var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(graph, graph.Graph!.RootIdentities.Single());
        if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success ||
            linked.LinkedRuntimeModuleIdentities?.Contains(HybridCpuManagedNullableValueEmitterV1.ModuleIdentity) != true ||
            !graph.Methods.Single().Import.RequiresNativeExceptionTransfer ||
            graph.TypeUniverse?.Rows.Any(r => r.StableIdentity == "System.InvalidOperationException" && r.Descriptor is not null) != true)
            throw new Exception("Nullable helper exact exception metadata/native EH/image: " + string.Join(';', linked.Diagnostics));
        var noMetadata = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(request with { MetadataOnlyModules = [] });
        if (noMetadata.Status == RestrictedCilImportStatusV1.Success &&
            new ScalarControlFlowV2ObjectLinkerV1().Link(noMetadata, noMetadata.Graph!.RootIdentities.Single()).Status == ScalarControlFlowV2LinkStatusV1.Success)
            throw new Exception("Nullable helper linked without exception metadata authority");
        Console.WriteLine("PASS checked nullable Value exact helper ABI/serialized ECALL/full-width args/frame/throw marker and exception-metadata/native-EH image gate");
    }
    private static void RunHelperDefaultWords()
    {
        byte[] pe=File.ReadAllBytes(typeof(NullableValueFixture).Assembly.Location);
        ScalarControlFlowV2LinkedProgramV1 Compile(string entry) {
            var graph=new RestrictedCilImporterV1(mode:RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                ManagedBodyWorldModeV1.StandaloneRestrictedModule,pe,"nullable-helper-default",[new(typeof(NullableValueFixture).FullName!,entry)],[],
                MetadataOnlyModules:[new(File.ReadAllBytes(typeof(object).Assembly.Location),"core")]));
            if(graph.Status!=RestrictedCilImportStatusV1.Success)throw new Exception(string.Join(';',graph.Diagnostics));
            var linked=new ScalarControlFlowV2ObjectLinkerV1().Link(graph,graph.Graph!.RootIdentities.Single());
            if(linked.Status!=ScalarControlFlowV2LinkStatusV1.Success)throw new Exception(string.Join(';',linked.Diagnostics));
            return linked;
        }
        var actual=Compile(nameof(NullableValueFixture.ReadHelper)); var scalar=Compile(nameof(NullableValueFixture.ReadHelperScalar));
        ulong Evaluate(ScalarControlFlowV2LinkedProgramV1 program,string method,ulong input,ulong fallback) {
            var identity=program.MethodObjects.Single(m=>m.MethodIdentity.Contains("."+method+"(",StringComparison.Ordinal)).MethodIdentity;
            var image=program.LinkedImage!; var entry=image.Symbols.Single(s=>s.Name==identity);
            try { return NativeConstantSmoke.Evaluate(image.ImageBytes,input,fallback,checked((int)(entry.Address-image.ImageBase)),image.ImageBase); }
            catch {
                Console.WriteLine("HELPER-ENTRY "+method+" "+entry.Address+" base="+image.ImageBase+" "+string.Join(';',image.Symbols.Select(s=>s.Name+":"+s.Address)));
                foreach(var section in image.Sections.Where(s=>s.Name==".text"))for(ulong at=section.Address;at<section.Address+section.Size;at+=(ulong)HybridCpuBundleSerializer.BundleSizeBytes) {
                    var bundle=new HybridCpuInstructionBundle();bundle.TryReadBytes(image.ImageBytes,checked((int)(at-image.ImageBase)));
                    for(int slot=0;slot<8;slot++) { var word=bundle.GetInstruction(slot);if((HybridCpuOpcode)word.OpCode==HybridCpuOpcode.Nope)continue;
                        HybridCpuInstructionWord.TryUnpackArchRegs(word.Word1,out byte rd,out byte rs1,out byte rs2);
                        Console.WriteLine($"HELPER-WORD {at-image.ImageBase}:{slot} {(HybridCpuOpcode)word.OpCode} x{rd},x{rs1},x{rs2} {(short)word.Immediate}");
                    }
                }
                throw;
            }
        }
        if(actual.MethodObjects.Single(m=>m.MethodIdentity.Contains(".AdjustHelper(",StringComparison.Ordinal)).CodeSha256!=
            scalar.MethodObjects.Single(m=>m.MethodIdentity.Contains(".AdjustHelperScalar(",StringComparison.Ordinal)).CodeSha256)throw new Exception("Nullable helper default independent scalar leaf bytes");
        foreach(int input in new[]{int.MinValue,-1,0,1,int.MaxValue})foreach(int fallback in new[]{int.MinValue,-7,0,int.MaxValue}) {
            foreach(bool present in new[]{false,true}) {
                ulong packed=present?((ulong)(uint)input<<1)|1UL:0;
                ulong expected=unchecked((ulong)(long)(present?input:unchecked(fallback+1)));
                if(Evaluate(actual,"ConsumeHelper",packed,unchecked((ulong)fallback))!=expected||
                    Evaluate(scalar,nameof(NullableValueFixture.ReadHelperScalar),packed,unchecked((ulong)fallback))!=expected)throw new Exception("Nullable helper linked scalar/payload/ABI words");
            }
            long callerExpected=input<0?unchecked(fallback+1):input;
            if(Evaluate(actual,nameof(NullableValueFixture.ReadHelper),unchecked((ulong)input),unchecked((ulong)fallback))!=unchecked((ulong)callerExpected)||
                NullableValueFixture.ReadHelper(input,fallback)!=callerExpected)throw new Exception("Nullable helper linked caller/host words");
        }
        Console.WriteLine("PASS Nullable<int> helper default linked call/return words, packed carrier preservation, scalar leaf bytes and independent caller/helper/CoreCLR boundaries");
    }
    private static void RunParameterWords(bool presence = false, bool explicitDefault = false, bool dynamicDefault = false, bool computedDefault = false, bool computedConditional = false)
    {
        computedDefault |= computedConditional;
        dynamicDefault |= computedDefault;
        byte[] pe=File.ReadAllBytes(typeof(NullableValueFixture).Assembly.Location);
        var graph=new RestrictedCilImporterV1(mode:RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
            ManagedBodyWorldModeV1.StandaloneRestrictedModule,pe,"nullable-parameter-words",[new(typeof(NullableValueFixture).FullName!,computedConditional?nameof(NullableValueFixture.ReadComputedConditional):computedDefault?nameof(NullableValueFixture.ReadComputed):dynamicDefault?nameof(NullableValueFixture.ReadDynamic):presence?nameof(NullableValueFixture.ReadPresence):explicitDefault?nameof(NullableValueFixture.ReadDefault):nameof(NullableValueFixture.ReadParameter))],[],
            MetadataOnlyModules:[new(File.ReadAllBytes(typeof(object).Assembly.Location),"core")]));
        if(graph.Status!=RestrictedCilImportStatusV1.Success)throw new Exception(string.Join(';',graph.Diagnostics));
        var helper=graph.Methods.Single(m=>m.Identity.MethodName==(computedConditional?"ConsumeComputedConditional":computedDefault?"ConsumeComputed":dynamicDefault?"ConsumeDynamic":presence?"ConsumePresence":explicitDefault?"ConsumeDefault":"ConsumeParameter"));
        var obj=(ScalarControlFlowV2MethodObjectV1)typeof(ScalarControlFlowV2ObjectLinkerV1).GetMethod("CompileMethod",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,[helper,0])!;
        if(obj.ObjectArtifact.Status!=HybridCpuObjectStatusV1.Success)throw new Exception("Nullable parameter HCO");
        byte[] code=obj.ObjectArtifact.Sections.Single(s=>s.Name==".text").Data;
        byte[]? scalarCode=null;
        if(presence || explicitDefault || dynamicDefault) {
            var scalarGraph=new RestrictedCilImporterV1(mode:RestrictedCilImportModeV1.ScalarControlFlowV2).ImportBodyWorld(new(
                ManagedBodyWorldModeV1.StandaloneRestrictedModule,pe,"nullable-parameter-scalar",[new(typeof(NullableValueFixture).FullName!,computedConditional?nameof(NullableValueFixture.PackedComputedConditional):computedDefault?nameof(NullableValueFixture.PackedComputed):dynamicDefault?nameof(NullableValueFixture.PackedDynamic):explicitDefault?nameof(NullableValueFixture.PackedDefault):nameof(NullableValueFixture.PackedPresence))],[]));
            if(scalarGraph.Status!=RestrictedCilImportStatusV1.Success)throw new Exception("Nullable presence scalar import");
            var scalarObject=(ScalarControlFlowV2MethodObjectV1)typeof(ScalarControlFlowV2ObjectLinkerV1).GetMethod("CompileMethod",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,[scalarGraph.Methods.Single(),0])!;
            if(scalarObject.ObjectArtifact.Status!=HybridCpuObjectStatusV1.Success)throw new Exception("Nullable presence scalar object");
            scalarCode=scalarObject.ObjectArtifact.Sections.Single(s=>s.Name==".text").Data;
        }
        foreach(int input in new[]{int.MinValue,-1,0,1,int.MaxValue})foreach(bool present in new[]{false,true})foreach(int fallback in dynamicDefault?new[]{int.MinValue,-7,0,int.MaxValue}:new[]{0}) {
            ulong packed=present?((ulong)(uint)input<<1)|1UL:0;
            long expected=presence?(present?1L:0L):present?input:computedConditional&&fallback==0?-7L:computedDefault?unchecked(fallback+1):dynamicDefault?fallback:explicitDefault?-7L:0L;
            if(NativeConstantSmoke.Evaluate(code,packed,unchecked((ulong)fallback))!=unchecked((ulong)expected)||
                scalarCode is not null && NativeConstantSmoke.Evaluate(scalarCode,packed,unchecked((ulong)fallback))!=unchecked((ulong)expected) ||
                (computedConditional?NullableValueFixture.PackedComputedConditional(unchecked((long)packed),fallback):computedDefault?NullableValueFixture.PackedComputed(unchecked((long)packed),fallback):dynamicDefault?NullableValueFixture.PackedDynamic(unchecked((long)packed),fallback):presence?NullableValueFixture.PackedPresence(unchecked((long)packed)):explicitDefault?NullableValueFixture.PackedDefault(unchecked((long)packed)):NullableValueFixture.PackedParameter(unchecked((long)packed)))!=expected ||
                (computedConditional?NullableValueFixture.ReadComputedConditional(input,fallback):computedDefault?NullableValueFixture.ReadComputed(input,fallback):dynamicDefault?NullableValueFixture.ReadDynamic(input,fallback):presence?NullableValueFixture.ReadPresence(input):explicitDefault?NullableValueFixture.ReadDefault(input):NullableValueFixture.ReadParameter(input))!=(presence?1L:(long)input))
                throw new Exception("Nullable parameter packed ABI/CoreCLR/scalar/serialized words");
        }
        var linked=new ScalarControlFlowV2ObjectLinkerV1().Link(graph,graph.Graph!.RootIdentities.Single());
        if(linked.Status!=ScalarControlFlowV2LinkStatusV1.Success)throw new Exception("Nullable parameter caller/helper link");
        Console.WriteLine("PASS private Nullable<int> parameter "+(computedConditional?"computed conditional default":computedDefault?"computed default":dynamicDefault?"dynamic default":presence?"presence":explicitDefault?"constant default":"value")+" packed I8 ABI -> encoded helper words, independent scalar/CoreCLR boundaries and caller/helper image");
    }
}

public static class NullableValueFixture
{
    public static long Read(long input) { long? value = input < 0 ? null : input; return value.Value; }
    private static long ConsumeParameter(int? value)=>(long)value.GetValueOrDefault();
    public static long ReadParameter(int input) { int? value=input;return ConsumeParameter(value); }
    public static long PackedParameter(long packed)=>(long)(int)(packed>>>1);
    private static long ConsumePresence(int? value)=>value.HasValue?1L:0L;
    public static long ReadPresence(int input) { int? value=input;return ConsumePresence(value); }
    public static long PackedPresence(long packed)=>(packed&1L)!=0?1L:0L;
    private static long ConsumeDefault(int? value)=>(long)value.GetValueOrDefault(-7);
    public static long ReadDefault(int input) { int? value=input;return ConsumeDefault(value); }
    public static long PackedDefault(long packed)=>(packed&1L)!=0?(long)(int)(packed>>>1):-7L;
    private static long ConsumeDynamic(int? value,int fallback)=>(long)value.GetValueOrDefault(fallback);
    public static long ReadDynamic(int input,int fallback) { int? value=input;return ConsumeDynamic(value,fallback); }
    public static long PackedDynamic(long packed,int fallback)=>(packed&1L)!=0?(long)(int)(packed>>>1):(long)fallback;
    private static long ConsumeComputed(int? value,int fallback)=>(long)value.GetValueOrDefault(unchecked(fallback+1));
    public static long ReadComputed(int input,int fallback) { int? value=input;return ConsumeComputed(value,fallback); }
    public static long PackedComputed(long packed,int fallback)=>(packed&1L)!=0?(long)(int)(packed>>>1):(long)unchecked(fallback+1);
    private static long ConsumeComputedConditional(int? value,int fallback)=>(long)value.GetValueOrDefault(fallback==0?-7:unchecked(fallback+1));
    public static long ReadComputedConditional(int input,int fallback) { int? value=input;return ConsumeComputedConditional(value,fallback); }
    public static long PackedComputedConditional(long packed,int fallback)=>(packed&1L)!=0?(long)(int)(packed>>>1):(long)(fallback==0?-7:unchecked(fallback+1));
    private static int AdjustHelper(int fallback)=>unchecked(fallback+1);
    private static long ConsumeHelper(int? value,int fallback)=>(long)value.GetValueOrDefault(AdjustHelper(fallback));
    public static long ReadHelper(int input,int fallback) { int? value=input<0?null:input;return ConsumeHelper(value,fallback); }
    private static int AdjustHelperScalar(int fallback)=>unchecked(fallback+1);
    public static long ReadHelperScalar(long packed,int fallback) { int next=AdjustHelperScalar(fallback); return (packed&1L)!=0?(long)(int)(packed>>>1):(long)next; }
}
