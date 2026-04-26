using HybridCPU.Compiler.Core.IR;

namespace HybridCPU.Compiler.Core.Threading
{
    public partial class HybridCpuThreadCompilerContext
    {
        private HybridCpuCompiledProgram? _canonicalCompiledProgram;

        private HybridCpuCompiledProgram GetOrCompileCanonicalProgram()
        {
            _canonicalCompiledProgram ??= HybridCpuCanonicalCompiler.CompileProgram(
                _virtualThreadId.Value,
                GetCompiledInstructions(),
                GetLabelDeclarations(),
                GetEntryPointDeclarations(),
                FrontendMode,
                bundleAnnotations: GetBundleAnnotations(),
                domainTag: DomainTag);
            return _canonicalCompiledProgram;
        }

        private HybridCpuCompiledProgram EmitCanonicalProgram(ulong baseAddress)
        {
            HybridCpuCompiledProgram emittedProgram = GetOrCompileCanonicalProgram().EmitVliwBundleImage(baseAddress);
            _canonicalCompiledProgram = emittedProgram;
            return emittedProgram;
        }

        private void InvalidateCanonicalCompileCache()
        {
            _canonicalCompiledProgram = null;
        }
    }
}
