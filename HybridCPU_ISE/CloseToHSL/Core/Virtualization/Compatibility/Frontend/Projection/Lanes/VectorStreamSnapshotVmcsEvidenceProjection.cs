namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class VectorStreamSnapshot
{
    public bool ContainsHostEvidence(Vmcs.V2.VmcsV2HostEvidenceKind evidence) => false;
}
