namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;

/// <summary>
/// Rollback switch surface for L7-SDC validation. Disabling execution must not
/// disable parser, carrier, guard, telemetry, token observation, or fail-closed
/// rejection surfaces.
/// </summary>
public sealed record ExternalAcceleratorFeatureSwitch
{
    public static ExternalAcceleratorFeatureSwitch Enabled { get; } = new();

    public static ExternalAcceleratorFeatureSwitch SubmitAndBackendExecutionDisabled { get; } = new()
    {
        SubmitAdmissionEnabled = false,
        BackendExecutionEnabled = false
    };

    public static ExternalAcceleratorFeatureSwitch BackendExecutionDisabled { get; } = new()
    {
        BackendExecutionEnabled = false
    };

    public bool SubmitAdmissionEnabled { get; init; } = true;

    public bool BackendExecutionEnabled { get; init; } = true;

    public bool ParserAndCarrierValidationEnabled => true;

    public bool GuardValidationEnabled => true;

    public bool TokenObservationEnabled => true;

    public bool TelemetryEvidenceEnabled => true;
}
