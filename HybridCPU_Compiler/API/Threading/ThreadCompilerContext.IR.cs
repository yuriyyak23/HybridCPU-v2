using HybridCPU.Compiler.Core.IR;

namespace HybridCPU.Compiler.Core.Threading
{
    public partial class HybridCpuThreadCompilerContext
    {
        /// <summary>
        /// Builds the normalized IR program for the current VT-local instruction buffer.
        /// </summary>
        public IrProgram BuildIrProgram()
        {
            var builder = new HybridCpuIrBuilder();
            return builder.BuildProgram(
                _virtualThreadId.Value,
                GetCompiledInstructions(),
                GetLabelDeclarations(),
                GetEntryPointDeclarations(),
                bundleAnnotations: GetBundleAnnotations(),
                domainTag: DomainTag);
        }
    }
}
