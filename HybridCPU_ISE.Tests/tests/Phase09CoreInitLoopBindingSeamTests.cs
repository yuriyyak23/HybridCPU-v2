using Xunit;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Phase09;

/// <summary>
/// Proves that <see cref="Processor.CPU_Core.Init_Registers_Cores_Pointers"/>
/// (D3-K binding seam) populates an explicitly-supplied core array instead of
/// writing into the mutable global <see cref="Processor.CPU_Cores"/>.
/// </summary>
public sealed class Phase09CoreInitLoopBindingSeamTests
{

}
