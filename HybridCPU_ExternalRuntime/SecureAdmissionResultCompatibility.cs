using HybridCPU.ExternalRuntime.Contracts;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;

namespace HybridCPU.ExternalRuntime;

public sealed partial class ExternalSecureDomainAdapterSession
{
    private static readonly Func<ExternalSecureRegisterLocalRequest,CanonicalBundle?,SecureComputeCanonicalAdmissionResult>? CleanupDenialFactory=
        typeof(SecureComputeCanonicalAdmissionResult).GetMethod("DeniedBackendCleanupRequired",[typeof(ExternalSecureRegisterLocalRequest),typeof(CanonicalBundle)])?
            .CreateDelegate<Func<ExternalSecureRegisterLocalRequest,CanonicalBundle?,SecureComputeCanonicalAdmissionResult>>();
    private static readonly Func<ExternalSecureRegisterLocalRequest,SecureComputeCanonicalAdmissionResult>? CompletedDenialFactory=
        typeof(SecureComputeCanonicalAdmissionResult).GetMethod("DeniedCompletedOperation",[typeof(ExternalSecureRegisterLocalRequest)])?
            .CreateDelegate<Func<ExternalSecureRegisterLocalRequest,SecureComputeCanonicalAdmissionResult>>();

    private static SecureComputeCanonicalAdmissionResult BackendCleanupDenied(ExternalSecureRegisterLocalRequest request,CanonicalBundle? canonical=null)=>
        CleanupDenialFactory is { } factory?factory(request,canonical):
            throw new InvalidOperationException("Original backend obligation remains pinned; canonical cleanup-required contract unavailable.");
    private static SecureComputeCanonicalAdmissionResult CompletedRegisterDenied(ExternalSecureRegisterLocalRequest request)=>
        CompletedDenialFactory is { } factory?factory(request):SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request);
}
