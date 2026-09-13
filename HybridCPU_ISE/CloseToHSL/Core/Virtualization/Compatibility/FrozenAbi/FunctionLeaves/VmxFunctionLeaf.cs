namespace YAKSys_Hybrid_CPU.Core
{
    public enum VmxFunctionLeaf : ushort
    {
        None = 0,
        CapabilityQuery = 1,
        Lane7QueryCaps = 7,
        Lane7Submit = 8,
    }

    public enum VmxFunctionLeafAdmissionDecision : byte
    {
        Allowed = 0,
        UnsupportedLeaf = 1,
        FrozenAbiRequired = 2,
        DescriptorValidationDenied = 3,
        CapabilityValidationDenied = 4,
        Lane7GrantDenied = 5,
        FastPathGrantDenied = 6,
    }

    public readonly record struct VmxFunctionLeafAdmissionRequest(
        VmxFunctionLeaf Leaf,
        bool IsFrozenAbiSurface,
        bool DescriptorValidated,
        bool CapabilityValidated,
        bool Lane7GrantValidated,
        bool UsesFastPath);

    public readonly record struct VmxFunctionLeafAdmissionResult(
        VmxFunctionLeafAdmissionDecision Decision,
        string Reason)
    {
        public bool IsAllowed => Decision == VmxFunctionLeafAdmissionDecision.Allowed;

        public static VmxFunctionLeafAdmissionResult Allowed { get; } =
            new(VmxFunctionLeafAdmissionDecision.Allowed, "VMFUNC leaf admission allowed.");

        public static VmxFunctionLeafAdmissionResult Denied(
            VmxFunctionLeafAdmissionDecision decision,
            string reason) =>
            new(decision, reason);
    }

    public sealed class VmxFunctionLeafAdmissionPolicy
    {
        public VmxFunctionLeafAdmissionResult Validate(
            VmxFunctionLeafAdmissionRequest request)
        {
            if (!IsSupportedLeaf(request.Leaf))
            {
                return VmxFunctionLeafAdmissionResult.Denied(
                    VmxFunctionLeafAdmissionDecision.UnsupportedLeaf,
                    "VMFUNC leaf is not declared in the frozen compatibility alias set.");
            }

            if (!request.IsFrozenAbiSurface)
            {
                return VmxFunctionLeafAdmissionResult.Denied(
                    VmxFunctionLeafAdmissionDecision.FrozenAbiRequired,
                    "VMFUNC leaves are valid only on the frozen compatibility ABI surface.");
            }

            if (!request.DescriptorValidated)
            {
                return VmxFunctionLeafAdmissionResult.Denied(
                    VmxFunctionLeafAdmissionDecision.DescriptorValidationDenied,
                    "VMFUNC leaf admission requires descriptor validation.");
            }

            if (!request.CapabilityValidated)
            {
                return VmxFunctionLeafAdmissionResult.Denied(
                    VmxFunctionLeafAdmissionDecision.CapabilityValidationDenied,
                    "VMFUNC leaf admission requires capability validation.");
            }

            if (RequiresLane7Grant(request.Leaf) && !request.Lane7GrantValidated)
            {
                return VmxFunctionLeafAdmissionResult.Denied(
                    VmxFunctionLeafAdmissionDecision.Lane7GrantDenied,
                    "Lane7 VMFUNC leaves require an explicit Lane7 grant.");
            }

            if (request.UsesFastPath && !request.CapabilityValidated)
            {
                return VmxFunctionLeafAdmissionResult.Denied(
                    VmxFunctionLeafAdmissionDecision.FastPathGrantDenied,
                    "VMFUNC fast paths require prevalidated capability grants.");
            }

            return VmxFunctionLeafAdmissionResult.Allowed;
        }

        public bool CanAdmit(VmxFunctionLeafAdmissionRequest request) =>
            Validate(request).IsAllowed;

        public static bool IsSupportedLeaf(VmxFunctionLeaf leaf) =>
            leaf is VmxFunctionLeaf.CapabilityQuery
                or VmxFunctionLeaf.Lane7QueryCaps
                or VmxFunctionLeaf.Lane7Submit;

        public static bool RequiresLane7Grant(VmxFunctionLeaf leaf) =>
            leaf is VmxFunctionLeaf.Lane7QueryCaps
                or VmxFunctionLeaf.Lane7Submit;
    }
}
