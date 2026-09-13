using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Reference-owned containment for committed ISA-visible core state.
/// Publication authority remains with the existing retire, CSR, control-flow
/// and context-transition owners.
/// </summary>
internal sealed class ArchitecturalState
{
    internal ArchContextState[] Contexts = null!;
    internal CsrFile Csr = null!;
    internal ulong PodId;
    internal ulong PodAffinityMask;
    internal ulong MemoryDomainCertificate;
    internal ulong NocRouteConfiguration;
    internal Processor.CPU_Core.FPExceptionContext[] FloatingPointContexts = null!;
    internal Processor.CPU_Core.FlagsRegister CoreFlags;
    internal List<Processor.CPU_Core.FlagsRegister> FlagsContextStack = null!;
    internal List<ulong> CallContextStack = null!;
    internal List<ulong> InterruptContextStack = null!;
    internal Processor.StackMemory Stack = null!;

    internal ulong PredicateRegister0;
    internal ulong PredicateRegister1;
    internal ulong PredicateRegister2;
    internal ulong PredicateRegister3;
    internal ulong PredicateRegister4;
    internal ulong PredicateRegister5;
    internal ulong PredicateRegister6;
    internal ulong PredicateRegister7;
    internal ulong PredicateRegister8;
    internal ulong PredicateRegister9;
    internal ulong PredicateRegister10;
    internal ulong PredicateRegister11;
    internal ulong PredicateRegister12;
    internal ulong PredicateRegister13;
    internal ulong PredicateRegister14;
    internal ulong PredicateRegister15;

    internal Processor.CPU_Core.RVV_Config VectorConfig;
    internal Processor.CPU_Core.VectorExceptionStatus VectorExceptionStatus;
    internal Processor.CPU_Core.VectorContext SavedVectorContext;
}
