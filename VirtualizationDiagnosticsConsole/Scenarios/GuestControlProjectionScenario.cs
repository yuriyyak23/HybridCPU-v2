using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class GuestControlProjectionScenario : IVirtualizationScenario
{
    public string Id => "guest-control-projection";
    public string Description => "Guarded read-only GuestCr0/GuestCr4 projection and denial matrix.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.RuntimeContract;

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var service = new VmxCompatibilityAdmissionService();
        (VmcsField Field, long Expected)[] fields =
        [
            (VmcsField.GuestCr0, unchecked((long)0x80000011UL)),
            (VmcsField.GuestCr4, 0x00000620L),
        ];

        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach ((VmcsField field, long expected) in fields)
            {
                VmxCompatibilityVmReadAdmissionResult allowed = service.AdmitVmReadProjection(
                    VirtualizationFixtures.VmReadRequest(field, VirtualizationFixtures.PrivilegedDescriptor()));
                context.Check(allowed.Decision == VmxCompatibilityVmReadAdmissionDecision.ReadOnlyValueProjected,
                    $"{field} must project only after all guards");
                context.Check(allowed.IsReadOnlyValueProjected && allowed.Value == expected,
                    $"{field} projected value must match neutral owner state");
                context.Check(!allowed.ValueProjection.PrivilegedProjection.BackendSuccessAuthorized,
                    $"{field} projection must not authorize backend success");
                context.Check(!allowed.ValueProjection.PrivilegedProjection.MutationAuthorized,
                    $"{field} projection must not authorize mutation");
                context.Check(!allowed.ValueProjection.PrivilegedProjection.CompletionPublicationAuthorized,
                    $"{field} projection must not authorize completion publication");
                context.Check(!allowed.ValueProjection.PrivilegedProjection.RetirePublicationAuthorized,
                    $"{field} projection must not authorize retire publication");

                VmxCompatibilityVmReadAdmissionResult noSource = service.AdmitVmReadProjection(
                    VirtualizationFixtures.VmReadRequest(field, descriptor: null));
                context.Check(noSource.Decision == VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied,
                    $"{field} must fail closed without a read-only source");
                context.Check(noSource.Value == 0 && !noSource.IsReadOnlyValueProjected,
                    $"{field} denial must not leak a value");

                VmxCompatibilityVmReadAdmissionResult noProof = service.AdmitVmReadProjection(
                    VirtualizationFixtures.VmReadRequest(field, VirtualizationFixtures.PrivilegedDescriptor(), conformanceProven: false));
                context.Check(noProof.Decision == VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied,
                    $"{field} must fail closed without conformance proof");
                context.Check(noProof.ValueProjection.PrivilegedProjection.Decision ==
                    PrivilegedExecutionStateProjectionDecision.DeniedNoConformanceProof,
                    $"{field} denial must identify missing conformance proof");

                context.Count("allowed_read_only_projections");
                context.Count("missing_source_rejections");
                context.Count("missing_proof_rejections");
                context.Trace("guest-control-projection", ("field", field), ("value", allowed.Value));
            }

            context.CompleteIteration("GuestCr0/GuestCr4 guarded projection matrix completed.");
        }

        return Task.CompletedTask;
    }
}
