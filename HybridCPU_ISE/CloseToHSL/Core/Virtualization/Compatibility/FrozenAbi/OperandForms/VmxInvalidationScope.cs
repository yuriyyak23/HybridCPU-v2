namespace YAKSys_Hybrid_CPU.Core
{
    public enum VmxInvalidationScope : byte
    {
        None = 0,
        SingleContext = 1,
        AllContexts = 2,
        SingleAddress = 3,
    }

    public enum VmxInvalidationScopeAdmissionDecision : byte
    {
        Allowed = 0,
        UnsupportedScope = 1,
        DescriptorValidationDenied = 2,
        CapabilityValidationDenied = 3,
        RuntimeInvalidationDenied = 4,
        TranslationAuthorityDenied = 5,
        DirectMmuMutationDenied = 6,
    }

    public readonly record struct VmxInvalidationScopeAdmissionRequest(
        VmxInvalidationScope Scope,
        bool DescriptorValidated,
        bool CapabilityValidated,
        bool RuntimeInvalidationValidated,
        bool TranslationAuthorityValidated,
        bool AttemptsDirectMmuMutation);

    public readonly record struct VmxInvalidationScopeAdmissionResult(
        VmxInvalidationScopeAdmissionDecision Decision,
        string Reason)
    {
        public bool IsAllowed => Decision == VmxInvalidationScopeAdmissionDecision.Allowed;

        public static VmxInvalidationScopeAdmissionResult Allowed { get; } =
            new(VmxInvalidationScopeAdmissionDecision.Allowed, "VMX invalidation scope admission allowed.");

        public static VmxInvalidationScopeAdmissionResult Denied(
            VmxInvalidationScopeAdmissionDecision decision,
            string reason) =>
            new(decision, reason);
    }

    public sealed class VmxInvalidationScopeAdmissionPolicy
    {
        public VmxInvalidationScopeAdmissionResult Validate(
            VmxInvalidationScopeAdmissionRequest request)
        {
            if (!IsSupportedScope(request.Scope))
            {
                return VmxInvalidationScopeAdmissionResult.Denied(
                    VmxInvalidationScopeAdmissionDecision.UnsupportedScope,
                    "VMX invalidation scope is not a supported frozen ABI alias.");
            }

            if (!request.DescriptorValidated)
            {
                return VmxInvalidationScopeAdmissionResult.Denied(
                    VmxInvalidationScopeAdmissionDecision.DescriptorValidationDenied,
                    "VMX invalidation requires descriptor validation.");
            }

            if (!request.CapabilityValidated)
            {
                return VmxInvalidationScopeAdmissionResult.Denied(
                    VmxInvalidationScopeAdmissionDecision.CapabilityValidationDenied,
                    "VMX invalidation requires capability validation.");
            }

            if (!request.RuntimeInvalidationValidated)
            {
                return VmxInvalidationScopeAdmissionResult.Denied(
                    VmxInvalidationScopeAdmissionDecision.RuntimeInvalidationDenied,
                    "VMX invalidation requires runtime-owned invalidation validation.");
            }

            if (!request.TranslationAuthorityValidated)
            {
                return VmxInvalidationScopeAdmissionResult.Denied(
                    VmxInvalidationScopeAdmissionDecision.TranslationAuthorityDenied,
                    "VMX invalidation requires translation-domain authority validation.");
            }

            if (request.AttemptsDirectMmuMutation)
            {
                return VmxInvalidationScopeAdmissionResult.Denied(
                    VmxInvalidationScopeAdmissionDecision.DirectMmuMutationDenied,
                    "VMX invalidation aliases cannot mutate MMU state directly.");
            }

            return VmxInvalidationScopeAdmissionResult.Allowed;
        }

        public bool CanAdmit(VmxInvalidationScopeAdmissionRequest request) =>
            Validate(request).IsAllowed;

        public static bool IsSupportedScope(VmxInvalidationScope scope) =>
            scope is VmxInvalidationScope.SingleContext
                or VmxInvalidationScope.AllContexts
                or VmxInvalidationScope.SingleAddress;
    }
}
