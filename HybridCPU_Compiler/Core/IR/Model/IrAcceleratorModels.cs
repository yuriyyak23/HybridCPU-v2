using System;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU.Compiler.Core.IR;

/// <summary>
/// Compiler-side lowering outcome for high-level accelerator intent.
/// The choice is made before native ACCEL_SUBMIT emission; runtime rejection never
/// authorizes a later compiler fallback.
/// </summary>
public enum AcceleratorLoweringMode : byte
{
    CpuOrNonAccelerator = 0,
    EmitAcceleratorSubmit = 1,
    DmaStreamCompute = 2,
    Reject = 3
}

/// <summary>
/// Typed descriptor payload that must travel outside raw VLIW reserved fields.
/// </summary>
public sealed record IrAcceleratorDescriptorSideband
{
    public IrAcceleratorDescriptorSideband(AcceleratorCommandDescriptor commandDescriptor)
    {
        ArgumentNullException.ThrowIfNull(commandDescriptor);
        CommandDescriptor = commandDescriptor;
        DescriptorReference = commandDescriptor.DescriptorReference;
    }

    public AcceleratorCommandDescriptor CommandDescriptor { get; }

    public AcceleratorDescriptorReference DescriptorReference { get; }
}

/// <summary>
/// High-level accelerator intent. This is a compiler decision surface only, not
/// runtime authority and not a fallback promise after ACCEL_SUBMIT exists.
/// </summary>
public sealed record IrAcceleratorIntent
{
    public required AcceleratorOperationKind Operation { get; init; }

    public required IrAcceleratorDescriptorSideband DescriptorSideband { get; init; }

    public AcceleratorLoweringMode RequestedMode { get; init; } =
        AcceleratorLoweringMode.EmitAcceleratorSubmit;

    public byte TokenDestinationRegister { get; init; } = 1;

    public bool IsCoarseGrained { get; init; } = true;

    public bool AllowRuntimeFallbackAfterSubmit { get; init; }

    public static IrAcceleratorIntent ForMatMul(
        AcceleratorCommandDescriptor descriptor,
        byte tokenDestinationRegister = 1,
        AcceleratorLoweringMode requestedMode = AcceleratorLoweringMode.EmitAcceleratorSubmit,
        bool isCoarseGrained = true)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new IrAcceleratorIntent
        {
            Operation = AcceleratorOperationKind.MatMul,
            DescriptorSideband = new IrAcceleratorDescriptorSideband(descriptor),
            RequestedMode = requestedMode,
            TokenDestinationRegister = tokenDestinationRegister,
            IsCoarseGrained = isCoarseGrained,
            AllowRuntimeFallbackAfterSubmit = false
        };
    }
}

/// <summary>
/// Concrete native command selected by compiler lowering.
/// </summary>
public sealed record IrAcceleratorCommand
{
    public required AcceleratorOperationKind Operation { get; init; }

    public required IrAcceleratorDescriptorSideband DescriptorSideband { get; init; }

    public required byte TokenDestinationRegister { get; init; }

    public bool AllowRuntimeFallbackAfterSubmit { get; init; }
}

/// <summary>
/// Compiler-side result for an accelerator lowering choice.
/// </summary>
public sealed record CompilerAcceleratorLoweringDecision
{
    private CompilerAcceleratorLoweringDecision(
        AcceleratorLoweringMode mode,
        IrAcceleratorCommand? command,
        string reason)
    {
        Mode = mode;
        Command = command;
        Reason = string.IsNullOrWhiteSpace(reason)
            ? "No accelerator lowering reason was supplied."
            : reason;
    }

    public AcceleratorLoweringMode Mode { get; }

    public IrAcceleratorCommand? Command { get; }

    public string Reason { get; }

    public bool EmitsAcceleratorSubmit => Mode == AcceleratorLoweringMode.EmitAcceleratorSubmit;

    public bool UsesNonAcceleratorLowering => Mode == AcceleratorLoweringMode.CpuOrNonAccelerator;

    public bool UsesDmaStreamCompute => Mode == AcceleratorLoweringMode.DmaStreamCompute;

    public static CompilerAcceleratorLoweringDecision EmitAcceleratorSubmit(
        IrAcceleratorCommand command,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new CompilerAcceleratorLoweringDecision(
            AcceleratorLoweringMode.EmitAcceleratorSubmit,
            command,
            reason);
    }

    public static CompilerAcceleratorLoweringDecision UseCpuOrNonAccelerator(
        string reason) =>
        new(
            AcceleratorLoweringMode.CpuOrNonAccelerator,
            command: null,
            reason);

    public static CompilerAcceleratorLoweringDecision UseDmaStreamCompute(
        string reason) =>
        new(
            AcceleratorLoweringMode.DmaStreamCompute,
            command: null,
            reason);

    public static CompilerAcceleratorLoweringDecision Reject(string reason) =>
        new(
            AcceleratorLoweringMode.Reject,
            command: null,
            reason);
}

/// <summary>
/// Compile-time accelerator capability strategy. Registry/provider facts here
/// only decide whether the compiler may emit ACCEL_SUBMIT.
/// </summary>
public sealed class CompilerAcceleratorCapabilityModel
{
    private CompilerAcceleratorCapabilityModel(
        bool supportsReferenceMatMul,
        ulong minimumCoarseElementCount)
    {
        SupportsReferenceMatMul = supportsReferenceMatMul;
        MinimumCoarseElementCount = minimumCoarseElementCount;
    }

    public static CompilerAcceleratorCapabilityModel Disabled { get; } = new(
        supportsReferenceMatMul: false,
        minimumCoarseElementCount: 0);

    public static CompilerAcceleratorCapabilityModel ReferenceMatMul { get; } = new(
        supportsReferenceMatMul: true,
        minimumCoarseElementCount: 1);

    public bool SupportsReferenceMatMul { get; }

    public ulong MinimumCoarseElementCount { get; }

    public bool Supports(IrAcceleratorIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ValidateDescriptorSideband(intent);

        AcceleratorCommandDescriptor descriptor = intent.DescriptorSideband.CommandDescriptor;
        return SupportsReferenceMatMul &&
               intent.Operation == AcceleratorOperationKind.MatMul &&
               IsSupportedReferenceMatMulDescriptor(descriptor);
    }

    public CompilerAcceleratorLoweringDecision Decide(IrAcceleratorIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ValidateMode(intent.RequestedMode);

        if (intent.AllowRuntimeFallbackAfterSubmit)
        {
            return CompilerAcceleratorLoweringDecision.Reject(
                "L7-SDC compiler intent cannot promise runtime fallback after ACCEL_SUBMIT emission.");
        }

        return intent.RequestedMode switch
        {
            AcceleratorLoweringMode.CpuOrNonAccelerator =>
                CompilerAcceleratorLoweringDecision.UseCpuOrNonAccelerator(
                    "Accelerator intent selected non-accelerator lowering before native ACCEL_SUBMIT emission."),

            AcceleratorLoweringMode.DmaStreamCompute =>
                CompilerAcceleratorLoweringDecision.UseDmaStreamCompute(
                    "Regular stream intent remains the lane6 DmaStreamCompute contour, not L7-SDC."),

            AcceleratorLoweringMode.Reject =>
                CompilerAcceleratorLoweringDecision.Reject(
                    "Accelerator intent requested compile-time rejection before native opcode emission."),

            AcceleratorLoweringMode.EmitAcceleratorSubmit when !Supports(intent) =>
                CompilerAcceleratorLoweringDecision.UseCpuOrNonAccelerator(
                    "Compiler accelerator capability model does not support this command; choose CPU/non-accelerator lowering before ACCEL_SUBMIT emission."),

            AcceleratorLoweringMode.EmitAcceleratorSubmit when
                !intent.IsCoarseGrained ||
                intent.DescriptorSideband.CommandDescriptor.ElementCount < MinimumCoarseElementCount =>
                CompilerAcceleratorLoweringDecision.UseCpuOrNonAccelerator(
                    "Accelerator workload is not coarse enough for L7-SDC emission; choose CPU/non-accelerator lowering before ACCEL_SUBMIT emission."),

            AcceleratorLoweringMode.EmitAcceleratorSubmit =>
                ValidateSubmitIntent(intent) is { } command
                    ? CompilerAcceleratorLoweringDecision.EmitAcceleratorSubmit(
                        command,
                        "Compiler capability model selected native lane7 ACCEL_SUBMIT emission.")
                    : throw new InvalidOperationException("Unreachable accelerator submit lowering state."),

            _ => throw CreateUnknownModeException(intent.RequestedMode)
        };
    }

    private static IrAcceleratorCommand ValidateSubmitIntent(IrAcceleratorIntent intent)
    {
        ValidateDescriptorSideband(intent);
        AcceleratorCommandDescriptor descriptor = intent.DescriptorSideband.CommandDescriptor;
        if (intent.Operation != descriptor.Operation)
        {
            throw new InvalidOperationException(
                "L7-SDC compiler intent operation must match the guard-accepted descriptor operation.");
        }

        if (intent.TokenDestinationRegister > ArchRegId.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intent.TokenDestinationRegister),
                intent.TokenDestinationRegister,
                $"ACCEL_SUBMIT token destination register must be in [0, {ArchRegId.MaxValue}].");
        }

        return new IrAcceleratorCommand
        {
            Operation = intent.Operation,
            DescriptorSideband = intent.DescriptorSideband,
            TokenDestinationRegister = intent.TokenDestinationRegister,
            AllowRuntimeFallbackAfterSubmit = false
        };
    }

    private static void ValidateDescriptorSideband(IrAcceleratorIntent intent)
    {
        if (intent.DescriptorSideband is null)
        {
            throw new InvalidOperationException(
                "L7-SDC compiler accelerator intent requires typed descriptor sideband before ACCEL_SUBMIT emission.");
        }
    }

    private static bool IsSupportedReferenceMatMulDescriptor(
        AcceleratorCommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return MatMulCapabilityProvider.Matches(descriptor) &&
               descriptor.Shape == AcceleratorShapeKind.Matrix2D &&
               descriptor.ShapeRank == 2 &&
               descriptor.ElementCount is > 0 and <= MatMulDescriptorValidator.MaxOutputElements &&
               descriptor.PartialCompletionPolicy == AcceleratorPartialCompletionPolicy.AllOrNone &&
               descriptor.NormalizedFootprint.Hash != 0 &&
               HasNonEmptyRanges(descriptor.SourceRanges) &&
               HasNonEmptyRanges(descriptor.DestinationRanges) &&
               HasNonEmptyRanges(descriptor.NormalizedFootprint.SourceRanges) &&
               HasNonEmptyRanges(descriptor.NormalizedFootprint.DestinationRanges);
    }

    private static bool HasNonEmptyRanges(
        System.Collections.Generic.IReadOnlyList<AcceleratorMemoryRange>? ranges)
    {
        if (ranges is null || ranges.Count == 0)
        {
            return false;
        }

        for (int index = 0; index < ranges.Count; index++)
        {
            AcceleratorMemoryRange range = ranges[index];
            if (range.Length == 0 || range.Address > ulong.MaxValue - range.Length)
            {
                return false;
            }
        }

        return true;
    }

    public static void ValidateMode(AcceleratorLoweringMode mode)
    {
        if (mode is not
            (AcceleratorLoweringMode.CpuOrNonAccelerator or
             AcceleratorLoweringMode.EmitAcceleratorSubmit or
             AcceleratorLoweringMode.DmaStreamCompute or
             AcceleratorLoweringMode.Reject))
        {
            throw CreateUnknownModeException(mode);
        }
    }

    private static ArgumentOutOfRangeException CreateUnknownModeException(
        AcceleratorLoweringMode mode) =>
        new(
            nameof(mode),
            mode,
            "Unknown accelerator compiler lowering mode.");
}
