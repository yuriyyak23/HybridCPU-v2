namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class DomainLegalityService
{
    public DomainValidationResult Validate(
        DomainRuntimeContext context,
        DomainRuntimeOperation operation,
        ulong requiredCapabilityMask = 0)
    {
        CapabilityBoundaryRequirement capabilityRequirement =
            requiredCapabilityMask == 0
                ? CapabilityBoundaryRequirement.None
                : CapabilityBoundaryRequirement.TypedGrant(
                    requiredCapabilityMask,
                    CapabilityGrantScope.CompatibilityProjection);

        return Validate(context, operation, capabilityRequirement);
    }

    public DomainValidationResult Validate(
        DomainRuntimeContext context,
        DomainRuntimeOperation operation,
        CapabilityBoundaryRequirement capabilityRequirement)
    {
        DomainValidationResult contextValidation =
            DomainValidationResult.RequireRuntimeContext(context);
        if (!contextValidation.IsValid)
        {
            return contextValidation;
        }

        DomainValidationResult operationValidation =
            DomainValidationResult.RequireOperation(
                context,
                operation,
                capabilityRequirement);
        if (!operationValidation.IsValid)
        {
            return operationValidation;
        }

        BundleLegalityDescriptor? descriptor = context.Execution!.BundleLegality;
        if (descriptor is null)
        {
            return DomainValidationResult.Fail(
                DomainValidationFailureReason.MissingBundleLegalityDescriptor,
                "Domain legality requires an execution-domain bundle legality descriptor.");
        }

        if (!descriptor.IsRuntimeAuthoritative)
        {
            return DomainValidationResult.Fail(
                DomainValidationFailureReason.RuntimeAuthorityMissing,
                "Compatibility projection or compiler evidence cannot own runtime legality.");
        }

        if (operation.Source == DomainRuntimeOperationSource.CompatibilityFrontend &&
            !descriptor.CanProjectToCompatibilityFrontend)
        {
            return DomainValidationResult.Fail(
                DomainValidationFailureReason.CompatibilityProjectionDenied,
                "The runtime legality descriptor denies compatibility projection.");
        }

        return DomainValidationResult.Passed;
    }
}
