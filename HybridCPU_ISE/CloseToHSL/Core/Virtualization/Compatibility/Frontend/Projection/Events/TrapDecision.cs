namespace YAKSys_Hybrid_CPU.Core
{
    public readonly record struct TrapDecision(
        bool ShouldExit,
        VmExitReason ExitReason,
        VmxExitQualification ExitQualification,
        TrapRequest Request)
    {
        public static TrapDecision NoExit(TrapRequest request) =>
            new(false, VmExitReason.None, VmxExitQualification.None, request);

        public static TrapDecision Exit(
            TrapRequest request,
            VmExitReason reason) =>
            new(true, reason, EncodeQualification(request), request);

        private static VmxExitQualification EncodeQualification(TrapRequest request)
        {
            ushort leaf = unchecked((ushort)(((byte)request.TargetKind << 8) | (byte)request.AccessType));
            ulong descriptor = request.TargetKind == TrapTargetKind.CompatibilityOperation
                ? request.Target
                : request.Target | ((request.Auxiliary & 0xFFFFUL) << 16);
            return new VmxExitQualification(leaf, VmxInvalidationScope.None, descriptor);
        }
    }
}
