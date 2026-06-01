using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;
using YAKSys_Hybrid_CPU.Core.Nested;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class VmxProjectionSchemaAndQuarantineTests
{
    [Fact]
    public void VmcsFieldProjectionSchema_DeclaresOwnerAccessEvidenceAndMigrationPolicy()
    {
        var contract = new VmcsFieldProjectionSchemaConformanceContract();

        Assert.True(contract.IsCurrentSchemaSatisfied());

        foreach (VmcsFieldProjectionSchemaEntry entry in VmcsFieldProjectionSchema.Entries)
        {
            Assert.NotEqual(VmcsFieldProjectionMigrationPolicy.None, entry.MigrationPolicy);
            Assert.NotEqual(EvidenceVisibilityClass.HostOwnedRuntimeEvidence, entry.EvidenceClass);
        }

        Assert.True(VmcsFieldProjectionSchema.TryGet(VmcsField.ExitReason, out var exitReason));
        Assert.False(VmcsFieldProjectionSchema.CanWrite(exitReason));
        Assert.Equal(VmcsFieldProjectionMigrationPolicy.RecomputedCompletion, exitReason.MigrationPolicy);

        Assert.True(VmcsFieldProjectionSchema.TryGet(VmcsField.HostPc, out var hostPc));
        Assert.False(VmcsFieldProjectionSchema.CanWrite(hostPc));
        Assert.Equal(VmcsFieldProjectionMigrationPolicy.ProjectionOnly, hostPc.MigrationPolicy);
    }

    [Fact]
    public void VmxCapsBitSchema_RequiresTypedGrantSourceAndGuestVisibleProjectionPolicy()
    {
        var contract = new VmxCapsBitSchemaConformanceContract();

        Assert.True(contract.IsCurrentSchemaSatisfied());

        foreach (CapabilityBitSchemaEntry entry in CapabilityDescriptorSetSchema.VmxCompatibilityBits)
        {
            Assert.Contains("TypedGrant", entry.TypedGrantSource, StringComparison.Ordinal);
            Assert.Equal(
                CapabilityFrontendProjectionPolicy.ProjectIfCompatible,
                entry.FrontendProjectionPolicy);
            Assert.Equal(
                CapabilityEvidenceVisibility.GuestVisibleProjection,
                entry.EvidenceVisibility);
        }
    }

    [Fact]
    public void CapabilityDescriptorSet_ProjectsVmxCapsFromGrantFirstReadOnlyEvidence()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        const ulong compatibleCapability = VmxV2InstructionCaps.VmFunc;
        const ulong hardwareOnlyCapability = VmxV2InstructionCaps.VmCall;
        var descriptorSet = new CapabilityDescriptorSet(
            globalHardwareCaps: compatibleCapability | hardwareOnlyCapability,
            runtimeEnabledCaps: compatibleCapability,
            domainGrantedCaps: compatibleCapability | hardwareOnlyCapability);
        var contract = new CapabilityDescriptorSetGrantFirstAuthorityContract();

        Assert.True(contract.IsSatisfied(descriptorSet, compatibleCapability));
        Assert.Equal(
            compatibleCapability | hardwareOnlyCapability,
            descriptorSet.GlobalHardwareCaps);
        Assert.Equal(compatibleCapability, descriptorSet.RuntimeEnabledCaps);
        Assert.Equal(
            compatibleCapability | hardwareOnlyCapability,
            descriptorSet.DomainGrantedCaps);
        Assert.Equal(compatibleCapability, descriptorSet.CompatibilityCapsProjection);
        Assert.Equal(
            descriptorSet.TypedGrants.EffectiveCompatibilityMask,
            new VmxCapsProjection().Read(descriptorSet));
        Assert.True(descriptorSet.HasEffectiveCapability(compatibleCapability));
        Assert.False(descriptorSet.HasEffectiveCapability(hardwareOnlyCapability));
        Assert.True(descriptorSet.TypedGrants.TryGetGrant(
            compatibleCapability,
            CapabilityGrantScope.CompatibilityProjection,
            out CapabilityGrant projectionGrant));
        Assert.Equal(
            CapabilityEvidenceVisibility.GuestVisibleProjection,
            projectionGrant.EvidenceVisibility);
        Assert.Equal(
            CapabilityFrontendProjectionPolicy.ProjectIfCompatible,
            projectionGrant.FrontendProjectionPolicy);
        Assert.True(descriptorSet.TypedGrants.TryGetGrant(
            hardwareOnlyCapability,
            CapabilityGrantScope.HardwareAvailable,
            out CapabilityGrant hardwareGrant));
        Assert.Equal(CapabilityEvidenceVisibility.HostOnly, hardwareGrant.EvidenceVisibility);
        Assert.Equal(
            CapabilityFrontendProjectionPolicy.NeverProject,
            hardwareGrant.FrontendProjectionPolicy);

        foreach (CapabilityBitSchemaEntry entry in CapabilityDescriptorSetSchema.VmxCompatibilityBits)
        {
            Assert.DoesNotContain("CapabilityDescriptorSet.", entry.TypedGrantSource);
            Assert.Contains("TypedGrant", entry.TypedGrantSource, StringComparison.Ordinal);
        }

        string descriptorSource = File.ReadAllText(Path.Combine(
            projectRoot,
            CapabilityDescriptorSetGrantFirstAuthorityContract.DescriptorSetPath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in CapabilityDescriptorSetGrantFirstAuthorityContract.RequiredGrantFirstMarkers)
        {
            Assert.Contains(marker, descriptorSource);
        }

        foreach (string marker in CapabilityDescriptorSetGrantFirstAuthorityContract.ForbiddenDescriptorBackingMarkers)
        {
            Assert.DoesNotContain(marker, descriptorSource);
        }

        string grantSource = File.ReadAllText(Path.Combine(
            projectRoot,
            CapabilityDescriptorSetGrantFirstAuthorityContract.GrantCollectionPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("CreateInternalGrant", grantSource);
        foreach (string marker in CapabilityDescriptorSetGrantFirstAuthorityContract.ForbiddenGrantCollectionMarkers)
        {
            Assert.DoesNotContain(marker, grantSource);
        }

        string compatibilityProjectionSource = File.ReadAllText(Path.Combine(
            projectRoot,
            CapabilityDescriptorSetGrantFirstAuthorityContract.CompatibilityProjectionPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("FromCompatibilityMasks", compatibilityProjectionSource);
        Assert.Contains("CapabilityDescriptorSetSchema.VmxCompatibility", compatibilityProjectionSource);
        Assert.Contains("CapabilityDescriptorSetSchema.VmxCompatibilityBits", compatibilityProjectionSource);
    }

    [Fact]
    public void CapabilityRuntimeSubstrate_MovesTypedGrantDescriptorServicesOutOfVmxSubstrate()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        var contract = new CapabilityRuntimeSubstrateExtractionContract();

        Assert.True(contract.IsSatisfied());

        foreach (string relativePath in CapabilityRuntimeSubstrateExtractionContract.RemovedCapabilitySubstratePaths)
        {
            Assert.False(File.Exists(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        string combinedNeutralSource = string.Empty;
        foreach (string relativePath in CapabilityRuntimeSubstrateExtractionContract.NeutralCapabilityRuntimePaths)
        {
            string sourcePath = Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(sourcePath));
            string source = File.ReadAllText(sourcePath);
            combinedNeutralSource += source;
            foreach (string marker in CapabilityRuntimeSubstrateExtractionContract.ForbiddenNeutralCapabilityRuntimeMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }

        foreach (string marker in CapabilityRuntimeSubstrateExtractionContract.RequiredNeutralCapabilityRuntimeMarkers)
        {
            Assert.Contains(marker, combinedNeutralSource);
        }

        string compatibilityProjectionSource = File.ReadAllText(Path.Combine(
            projectRoot,
            CapabilityRuntimeSubstrateExtractionContract.CompatibilityCapabilityProjectionPath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in CapabilityRuntimeSubstrateExtractionContract.RequiredCompatibilityProjectionMarkers)
        {
            Assert.Contains(marker, compatibilityProjectionSource);
        }

        string projectFile = File.ReadAllText(Path.Combine(projectRoot, "HybridCPU_ISE.csproj"));
        foreach (string marker in CapabilityRuntimeSubstrateExtractionContract.RemovedProjectFolderMarkers)
        {
            Assert.DoesNotContain(marker, projectFile);
        }

        AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(projectRoot);
    }

    [Fact]
    public void CapabilityCallerMaskIngress_DoesNotBypassTypedGrantAuthority()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        const ulong capability = VmxV2InstructionCaps.NestedVmx;
        var descriptorSet = new CapabilityDescriptorSet(
            globalHardwareCaps: capability,
            runtimeEnabledCaps: capability,
            domainGrantedCaps: capability);
        var contract = new CapabilityCallerMaskIngressAuthorityRemovalContract();

        Assert.True(contract.IsSatisfied(
            descriptorSet,
            capability,
            CapabilityGrant.DefaultRuntimeOwnerDomainId));

        var nonTypedRequirement = new CapabilityBoundaryRequirement(
            capability,
            CapabilityGrantScope.CompatibilityProjection,
            RequiresTypedGrant: false);
        Assert.False(nonTypedRequirement.IsSatisfiedBy(descriptorSet));
        Assert.True(CapabilityBoundaryRequirement.TypedGrant(
            capability,
            CapabilityGrantScope.CompatibilityProjection).IsSatisfiedBy(descriptorSet));

        foreach (string relativePath in CapabilityCallerMaskIngressAuthorityRemovalContract.AdmissionAuthorityPaths)
        {
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));

            foreach (string marker in CapabilityCallerMaskIngressAuthorityRemovalContract.ForbiddenAdmissionFallbackMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }

        string publicationPolicySource = File.ReadAllText(Path.Combine(
            projectRoot,
            CapabilityCallerMaskIngressAuthorityRemovalContract.CapabilityPublicationPolicyPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.DoesNotContain("HasEffectiveCapability(", publicationPolicySource);
        Assert.Contains("TypedGrants.TryGetGrant", publicationPolicySource);
        Assert.Contains("IsPublishableCompatibilityGrant", publicationPolicySource);

        string nestedSource = File.ReadAllText(Path.Combine(
            projectRoot,
            CapabilityCallerMaskIngressAuthorityRemovalContract.NestedDomainControllerPath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in CapabilityCallerMaskIngressAuthorityRemovalContract.ForbiddenNestedCallerPressureMarkers)
        {
            Assert.DoesNotContain(marker, nestedSource);
        }

        foreach (string marker in CapabilityCallerMaskIngressAuthorityRemovalContract.RequiredNestedTypedProjectionMarkers)
        {
            Assert.Contains(marker, nestedSource);
        }
    }

    [Fact]
    public void ProjectionSchemaPipeline_HasCanonicalArtifactsForGeneratedTables()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        var projectionPipeline = new ProjectionSchemaPipelineContract();
        var compatAliasPipeline = new CompatAliasGeneratorPipelineContract();

        Assert.True(compatAliasPipeline.IsCurrentPipelineSatisfied());
        Assert.True(projectionPipeline.IsCurrentVmcsFieldProjectionSatisfied());
        Assert.True(projectionPipeline.IsCurrentVmxCapsBitProjectionSatisfied());

        Assert.True(File.Exists(Path.Combine(
            projectRoot,
            CompatAliasGeneratorPipelineContract.Current.SchemaPath.Replace('/', Path.DirectorySeparatorChar))));

        Assert.True(File.Exists(Path.Combine(
            projectRoot,
            ProjectionSchemaPipelineContract.CurrentVmcsFieldProjection.SchemaPath.Replace('/', Path.DirectorySeparatorChar))));

        Assert.True(File.Exists(Path.Combine(
            projectRoot,
            ProjectionSchemaPipelineContract.CurrentVmxCapsBitProjection.SchemaPath.Replace('/', Path.DirectorySeparatorChar))));

        Assert.True(File.Exists(Path.Combine(
            projectRoot,
            GeneratedProjectionLineageBuildContract.CompatSpecArtifactSchemaPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void GeneratedProjectionLineage_BuildTargetRegeneratesVmcsFieldSchemaCompatAliasesVmxCapsAndArtifactManifest()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        var contract = new GeneratedProjectionLineageBuildContract();

        Assert.True(contract.IsSatisfied());

        string projectFile = File.ReadAllText(Path.Combine(projectRoot, "HybridCPU_ISE.csproj"));
        Assert.Contains(GeneratedProjectionLineageBuildContract.BuildPropertyName, projectFile);
        Assert.Contains($"Name=\"{GeneratedProjectionLineageBuildContract.BuildTargetName}\"", projectFile);
        Assert.Contains($"BeforeTargets=\"{GeneratedProjectionLineageBuildContract.BuildTargetPhase}\"", projectFile);
        Assert.Contains(GeneratedProjectionLineageBuildContract.VerifierScriptPath.Replace('/', '\\'), projectFile);

        string verifierScript = File.ReadAllText(Path.Combine(
            projectRoot,
            GeneratedProjectionLineageBuildContract.VerifierScriptPath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in GeneratedProjectionLineageBuildContract.RequiredVerifierFunctions)
        {
            Assert.Contains(marker, verifierScript);
        }

        foreach (string marker in GeneratedProjectionLineageBuildContract.RequiredGeneratedInputs)
        {
            Assert.Contains(marker, verifierScript);
        }

        foreach (string marker in GeneratedProjectionLineageBuildContract.RequiredGeneratedOutputs)
        {
            Assert.Contains(marker, verifierScript);
        }

        foreach (string marker in GeneratedProjectionLineageBuildContract.RequiredDriftFailureMarkers)
        {
            Assert.Contains(marker, verifierScript);
        }

        string vmcsFieldSchema = File.ReadAllText(Path.Combine(
            projectRoot,
            GeneratedProjectionLineageBuildContract.VmcsFieldProjectionSchemaPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("\"generator\": \"VmcsFieldProjectionGenerator\"", vmcsFieldSchema);
        Assert.Contains("\"entries\"", vmcsFieldSchema);
        Assert.Contains("\"field\": \"GuestPc\"", vmcsFieldSchema);
        Assert.Contains("\"field\": \"ExitReason\"", vmcsFieldSchema);
        Assert.Contains("\"field\": \"EptViolationQualification\"", vmcsFieldSchema);

        string compatAliasSchema = File.ReadAllText(Path.Combine(
            projectRoot,
            GeneratedProjectionLineageBuildContract.CompatAliasSchemaPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("\"generator\": \"CompatAliasMapGenerator\"", compatAliasSchema);
        Assert.Contains("\"sourceName\": \"VMREAD\"", compatAliasSchema);
        Assert.Contains("\"sourceName\": \"VMWRITE\"", compatAliasSchema);
        Assert.Contains("\"sourceName\": \"VmxCompletion\"", compatAliasSchema);

        string vmxCapsSchema = File.ReadAllText(Path.Combine(
            projectRoot,
            GeneratedProjectionLineageBuildContract.VmxCapsCapabilityBitSchemaPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("\"generator\": \"VmxCapsCapabilityBitGenerator\"", vmxCapsSchema);
        Assert.Contains("\"entries\"", vmxCapsSchema);
        Assert.Contains("\"name\": \"VmPtrSt\"", vmxCapsSchema);
        Assert.Contains("\"name\": \"NestedVmx\"", vmxCapsSchema);
        Assert.Contains("\"typedGrantSource\": \"CapabilityGrantCollection.TypedGrant\"", vmxCapsSchema);
        Assert.Contains("\"typedGrantSource\": \"NestedDomainCapability.TypedGrant\"", vmxCapsSchema);

        string compatSpecSchema = File.ReadAllText(Path.Combine(
            projectRoot,
            GeneratedProjectionLineageBuildContract.CompatSpecArtifactSchemaPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("\"generator\": \"CompatSpecArtifactSetGenerator\"", compatSpecSchema);
        Assert.Contains("\"lineageKind\": \"ExecutableGeneratedSource\"", compatSpecSchema);
        Assert.Contains("\"lineageKind\": \"ProjectionContractOnly\"", compatSpecSchema);
        Assert.Contains("\"lineageSource\": \"CapabilityDescriptorSetSchema\"", compatSpecSchema);

        string vmcsProjectionSource = File.ReadAllText(Path.Combine(
            projectRoot,
            GeneratedProjectionLineageBuildContract.VmcsFieldProjectionSourcePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.DoesNotContain("File.ReadAllText", vmcsProjectionSource);
        Assert.DoesNotContain("Process.Start", vmcsProjectionSource);
        Assert.Contains("private static readonly VmcsFieldProjectionSchemaEntry[] EntryTable", vmcsProjectionSource);

        string compatAliasSource = File.ReadAllText(Path.Combine(
            projectRoot,
            GeneratedProjectionLineageBuildContract.CompatAliasSourcePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.DoesNotContain("File.ReadAllText", compatAliasSource);
        Assert.DoesNotContain("Process.Start", compatAliasSource);
        Assert.Contains("private static readonly CompatAliasMapEntry[] EntryTable", compatAliasSource);

        string vmxCapsSource = File.ReadAllText(Path.Combine(
            projectRoot,
            GeneratedProjectionLineageBuildContract.VmxCapsCapabilityBitSourcePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.DoesNotContain("File.ReadAllText", vmxCapsSource);
        Assert.DoesNotContain("Process.Start", vmxCapsSource);
        Assert.Contains("private static readonly CapabilityBitSchemaEntry[] VmxCompatibilityBitTable", vmxCapsSource);
        Assert.Contains("CapabilityGrantCollection.TypedGrant", vmxCapsSource);
        Assert.Contains("NestedDomainCapability.TypedGrant", vmxCapsSource);

        string compatSpecSource = File.ReadAllText(Path.Combine(
            projectRoot,
            GeneratedProjectionLineageBuildContract.CompatSpecArtifactSourcePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.DoesNotContain("File.ReadAllText", compatSpecSource);
        Assert.DoesNotContain("Process.Start", compatSpecSource);
        Assert.Contains("CompatSpecArtifactLineageKind.ExecutableGeneratedSource", compatSpecSource);
        Assert.Contains("CompatSpecArtifactLineageKind.ProjectionContractOnly", compatSpecSource);

        var specArtifacts = new CompatSpecArtifactSet();
        Assert.True(specArtifacts.HasExecutableLineage(CompatSpecArtifactKind.VmcsFieldAliases));
        Assert.True(specArtifacts.HasExecutableLineage(CompatSpecArtifactKind.CapabilityProjection));
        Assert.True(specArtifacts.HasExecutableLineage(CompatSpecArtifactKind.CompatAliasMap));
        Assert.False(specArtifacts.RequiresGeneratedParity(CompatSpecArtifactKind.CompletionProjection));
        Assert.True(specArtifacts.RequiresAbiFreeze(CompatSpecArtifactKind.CompletionProjection));

        foreach (string sourcePath in EnumerateProductionSources(projectRoot))
        {
            string source = File.ReadAllText(sourcePath);
            foreach (string marker in GeneratedProjectionLineageBuildContract.ForbiddenRuntimeOwnerMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }
    }

    [Fact]
    public void CompatibilityWriteNoEmissionSnapshot_DeniesAnySubstrateMutation()
    {
        var projection = new VmxCapsProjection(
            new CapabilityPublicationPolicy(
                allowCompatibilityAliasPublication: true,
                writeDisposition: CapabilityWriteDisposition.CompatibilityNoEffect),
            CapabilityDescriptorSetSchema.VmxCompatibility,
            compatibilityWriteFenceEnabled: true);

        var contract = new CompatibilityWriteNoEmissionContract();
        var before = new CompatibilityWriteNoEmissionSnapshot(1, 2, 3, 4, 5, 6, 7);
        var sameAfter = new CompatibilityWriteNoEmissionSnapshot(1, 2, 3, 4, 5, 6, 7);
        var mutatedGrant = new CompatibilityWriteNoEmissionSnapshot(1, 200, 3, 4, 5, 6, 7);

        Assert.True(contract.IsNoMutationSatisfied(
            projection,
            VmxCapsWriteResult.CompatibilityNoEffect,
            before,
            sameAfter));

        CompatibilityWriteNoEmissionResult result = contract.ValidateNoMutation(
            projection,
            VmxCapsWriteResult.CompatibilityNoEffect,
            before,
            mutatedGrant);

        Assert.Equal(CompatibilityWriteNoEmissionDecision.GrantsMutated, result.Decision);
    }

    [Fact]
    public void VmxQuarantineEvidenceManifest_MatchesFilesAndRejectsReverseImportWithoutProof()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        var manifest = new VmxQuarantineEvidenceManifest();

        foreach (VmxQuarantineEvidenceEntry entry in VmxQuarantineEvidenceManifest.Entries)
        {
            Assert.True(entry.IsRecognizedLegacyRisk);

            if (entry.MustRemainQuarantined)
            {
                Assert.True(
                    File.Exists(Path.Combine(projectRoot, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar))),
                    $"Missing quarantined file: {entry.RelativePath}");
            }
            else if (entry.IsReturnedToCore)
            {
                Assert.True(
                    File.Exists(Path.Combine(projectRoot, entry.ReturnedCorePath.Replace('/', Path.DirectorySeparatorChar))),
                    $"Missing returned file: {entry.ReturnedCorePath}");
                Assert.False(
                    File.Exists(Path.Combine(projectRoot, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar))),
                    $"Returned file still exists in Legacy/VMX: {entry.RelativePath}");
            }
            else
            {
                Assert.True(entry.IsRemovedWithoutReplacement);
                Assert.False(
                    File.Exists(Path.Combine(projectRoot, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar))),
                    $"Removed legacy file still exists in Legacy/VMX: {entry.RelativePath}");
            }

            var missingProof = new VmxQuarantineReturnProof(
                OriginatesFromLegacyVmx: true,
                DescriptorOwnerIdentified: false,
                CapabilityPolicyAdded: false,
                EvidencePolicyAdded: false,
                RetireBoundaryDefined: false,
                ProjectionTestsAdded: false,
                ContainsAuthoritativeVmxState: true);

            Assert.False(manifest.CanReturnToCore(entry.RelativePath, missingProof));

            var completeProjectionOnlyProof = new VmxQuarantineReturnProof(
                OriginatesFromLegacyVmx: true,
                DescriptorOwnerIdentified: true,
                CapabilityPolicyAdded: true,
                EvidencePolicyAdded: true,
                RetireBoundaryDefined: true,
                ProjectionTestsAdded: true,
                ContainsAuthoritativeVmxState: false);

            if (entry.IsRemovedWithoutReplacement)
            {
                Assert.False(manifest.CanReturnToCore(entry.RelativePath, completeProjectionOnlyProof));
            }
            else
            {
                Assert.True(manifest.CanReturnToCore(entry.RelativePath, completeProjectionOnlyProof));
            }
        }
    }

    [Fact]
    public void LegacyMarkedCoreVmxSources_ArePhysicallyIsolatedInLegacyQuarantine()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string coreVmxRoot = Path.Combine(projectRoot, "Core", "VMX");
        string legacyVmxRoot = Path.Combine(projectRoot, "Legacy", "VMX");
        string legacyCompatibilityRoot = Path.Combine(legacyVmxRoot, "Compatibility");

        string[] coreSources = Directory.GetFiles(coreVmxRoot, "*.cs", SearchOption.AllDirectories);
        Assert.DoesNotContain(
            coreSources,
            path => File.ReadAllText(path).Contains("legacy", StringComparison.OrdinalIgnoreCase));

        string[] quarantinedSources = GetCSharpSourcesIfDirectoryExists(legacyVmxRoot);
        Assert.Empty(quarantinedSources);
        Assert.Empty(GetCSharpSourcesIfDirectoryExists(legacyCompatibilityRoot));
        Assert.DoesNotContain(
            Path.Combine(
                legacyVmxRoot,
                "Compatibility",
                "Adapters",
                "MemoryInvalidation",
                "LegacyVmxTranslationInvalidationBackend.cs"),
            quarantinedSources);
        Assert.DoesNotContain(
            Path.Combine(
                legacyVmxRoot,
                "Compatibility",
                "Generated",
                "CsrProjection",
                "LegacyCsrBackedVmxCapabilityDescriptorSource.cs"),
            quarantinedSources);
        Assert.DoesNotContain(
            Path.Combine(
                legacyVmxRoot,
                "Compatibility",
                "Adapters",
                "LegacyVmxV1",
                "LegacyVmxV1AdapterBoundary.cs"),
            quarantinedSources);
        Assert.DoesNotContain(
            Path.Combine(
                legacyVmxRoot,
                "Compatibility",
                "Adapters",
                "LegacyVmxV2",
                "LegacyVmxV2AdapterBoundary.cs"),
            quarantinedSources);
        Assert.DoesNotContain(
            Path.Combine(
                legacyVmxRoot,
                "Compatibility",
                "Generated",
                "VmcsProjection",
                "ShadowVmcsNestedProjectionService.cs"),
            quarantinedSources);
    }

    [Fact]
    public void LegacyVmxV1AdapterBoundaryRemoval_DeadPolicyShellIsAbsentWithoutReplacement()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        string removedPath = Path.Combine(
            projectRoot,
            LegacyVmxV1AdapterBoundaryRemovalContract.RemovedCompatibilityBoundaryPath.Replace('/', Path.DirectorySeparatorChar));
        var contract = new LegacyVmxV1AdapterBoundaryRemovalContract();
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(contract.IsSatisfied());
        Assert.False(File.Exists(removedPath));
        Assert.True(manifest.TryGetEntry(
            LegacyVmxV1AdapterBoundaryRemovalContract.RemovedCompatibilityBoundaryPath,
            out VmxQuarantineEvidenceEntry entry));
        Assert.True(entry.IsRemovedWithoutReplacement);
        Assert.False(manifest.RequiresQuarantine(
            LegacyVmxV1AdapterBoundaryRemovalContract.RemovedCompatibilityBoundaryPath));

        for (int index = 0; index < LegacyVmxV1AdapterBoundaryRemovalContract.CurrentFailClosedRoutingPaths.Length; index++)
        {
            string currentSource = File.ReadAllText(Path.Combine(
                projectRoot,
                LegacyVmxV1AdapterBoundaryRemovalContract.CurrentFailClosedRoutingPaths[index]
                    .Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains(
                LegacyVmxV1AdapterBoundaryRemovalContract.RequiredTypedFailClosedMarkers[index],
                currentSource);
        }
    }

    [Fact]
    public void LegacyVmxV2AdapterBoundaryRemoval_DeadPolicyShellIsAbsentWithoutReplacement()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        string removedPath = Path.Combine(
            projectRoot,
            LegacyVmxV2AdapterBoundaryRemovalContract.RemovedCompatibilityBoundaryPath.Replace('/', Path.DirectorySeparatorChar));
        var contract = new LegacyVmxV2AdapterBoundaryRemovalContract();
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(contract.IsSatisfied());
        Assert.False(File.Exists(removedPath));
        Assert.True(manifest.TryGetEntry(
            LegacyVmxV2AdapterBoundaryRemovalContract.RemovedCompatibilityBoundaryPath,
            out VmxQuarantineEvidenceEntry entry));
        Assert.True(entry.IsRemovedWithoutReplacement);
        Assert.False(manifest.RequiresQuarantine(
            LegacyVmxV2AdapterBoundaryRemovalContract.RemovedCompatibilityBoundaryPath));

        for (int index = 0; index < LegacyVmxV2AdapterBoundaryRemovalContract.RetainedGeneratedProjectionPaths.Length; index++)
        {
            string projectionSource = File.ReadAllText(Path.Combine(
                projectRoot,
                LegacyVmxV2AdapterBoundaryRemovalContract.RetainedGeneratedProjectionPaths[index]
                    .Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains(
                LegacyVmxV2AdapterBoundaryRemovalContract.RequiredGeneratedProjectionMarkers[index],
                projectionSource);
        }
    }

    [Fact]
    public void LegacyCsrBackedVmxCapabilityDescriptorSourceRemoval_DeadStubIsAbsentWithoutReplacement()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        string removedPath = Path.Combine(
            projectRoot,
            LegacyCsrBackedVmxCapabilityDescriptorSourceRemovalContract.RemovedCompatibilitySourcePath.Replace('/', Path.DirectorySeparatorChar));
        var contract = new LegacyCsrBackedVmxCapabilityDescriptorSourceRemovalContract();
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(contract.IsSatisfied());
        Assert.False(File.Exists(removedPath));
        Assert.True(manifest.TryGetEntry(
            LegacyCsrBackedVmxCapabilityDescriptorSourceRemovalContract.RemovedCompatibilitySourcePath,
            out VmxQuarantineEvidenceEntry entry));
        Assert.True(entry.IsRemovedWithoutReplacement);
        Assert.False(manifest.RequiresQuarantine(
            LegacyCsrBackedVmxCapabilityDescriptorSourceRemovalContract.RemovedCompatibilitySourcePath));

        for (int index = 0; index < LegacyCsrBackedVmxCapabilityDescriptorSourceRemovalContract.NeutralCapabilityAuthorityPaths.Length; index++)
        {
            string neutralSource = File.ReadAllText(Path.Combine(
                projectRoot,
                LegacyCsrBackedVmxCapabilityDescriptorSourceRemovalContract.NeutralCapabilityAuthorityPaths[index]
                    .Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains(
                LegacyCsrBackedVmxCapabilityDescriptorSourceRemovalContract.RequiredNeutralCapabilityAuthorityMarkers[index],
                neutralSource);
        }
    }

    [Fact]
    public void LegacyVmxTranslationInvalidationBackendRemoval_DeadShellIsAbsentWithoutReplacement()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        string removedPath = Path.Combine(
            projectRoot,
            LegacyVmxTranslationInvalidationBackendRemovalContract.RemovedCompatibilityBackendPath.Replace('/', Path.DirectorySeparatorChar));
        var contract = new LegacyVmxTranslationInvalidationBackendRemovalContract();
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(contract.IsSatisfied());
        Assert.False(File.Exists(removedPath));
        Assert.True(manifest.TryGetEntry(
            LegacyVmxTranslationInvalidationBackendRemovalContract.RemovedCompatibilityBackendPath,
            out VmxQuarantineEvidenceEntry entry));
        Assert.True(entry.IsRemovedWithoutReplacement);
        Assert.False(manifest.RequiresQuarantine(
            LegacyVmxTranslationInvalidationBackendRemovalContract.RemovedCompatibilityBackendPath));

        for (int index = 0; index < LegacyVmxTranslationInvalidationBackendRemovalContract.NeutralMemoryAuthorityPaths.Length; index++)
        {
            string neutralSource = File.ReadAllText(Path.Combine(
                projectRoot,
                LegacyVmxTranslationInvalidationBackendRemovalContract.NeutralMemoryAuthorityPaths[index]
                    .Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains(
                LegacyVmxTranslationInvalidationBackendRemovalContract.RequiredNeutralMemoryAuthorityMarkers[index],
                neutralSource);
        }
    }

    [Fact]
    public void LegacyVmxRetainedCompatibilitySurfaceInventory_ProvesNecessityAndNoAuthorityMutation()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        string legacyCompatibilityRoot = Path.Combine(projectRoot, "Legacy", "VMX", "Compatibility");
        var contract = new LegacyVmxRetainedCompatibilitySurfaceInventoryContract();
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(contract.IsSatisfied());

        Assert.Empty(GetCSharpSourcesIfDirectoryExists(legacyCompatibilityRoot));

        string[] rehomedProductionCompatibilitySources = Directory.GetFiles(
                Path.Combine(projectRoot, "Core", "VMX", "Compatibility"),
                "*.cs",
                SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(projectRoot, path).Replace('\\', '/'))
            .ToArray();

        foreach (LegacyVmxRetainedCompatibilityInventoryEntry entry in
                 LegacyVmxRetainedCompatibilitySurfaceInventoryContract.RetainedProductionCompatibilitySources)
        {
            Assert.Contains(entry.RelativePath, rehomedProductionCompatibilitySources);
            string retainedSource = File.ReadAllText(Path.Combine(
                projectRoot,
                entry.RelativePath.Replace('/', Path.DirectorySeparatorChar)));

            Assert.DoesNotContain("legacy", retainedSource, StringComparison.OrdinalIgnoreCase);
            Assert.True(manifest.TryGetEntry(
                entry.RelativePath.Replace("Core/VMX", "Legacy/VMX", StringComparison.Ordinal),
                out VmxQuarantineEvidenceEntry manifestEntry));
            Assert.Equal(entry.RelativePath, manifestEntry.ReturnedCorePath);
            Assert.False(manifest.RequiresQuarantine(manifestEntry.RelativePath));
            foreach (string marker in entry.RequiredMarkers)
            {
                Assert.Contains(marker, retainedSource);
            }

            foreach (string marker in LegacyVmxRetainedCompatibilitySurfaceInventoryContract
                         .ForbiddenCompatibilitySourceAuthorityMutationMarkers)
            {
                Assert.DoesNotContain(marker, retainedSource);
            }
        }

        foreach (LegacyVmxRetainedCompatibilityInventoryEntry caller in
                 LegacyVmxRetainedCompatibilitySurfaceInventoryContract.RequiredProductionCallers)
        {
            string callerSource = File.ReadAllText(Path.Combine(
                projectRoot,
                caller.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            foreach (string marker in caller.RequiredMarkers)
            {
                Assert.Contains(marker, callerSource);
            }
        }

        foreach (string sourcePath in Directory.GetFiles(
                     Path.Combine(projectRoot, "Core"),
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            string normalizedPath = sourcePath.Replace('\\', '/');
            if (normalizedPath.Contains("/Core/VMX/Conformance/", StringComparison.Ordinal))
            {
                continue;
            }

            string coreSource = File.ReadAllText(sourcePath);
            foreach (string marker in LegacyVmxRetainedCompatibilitySurfaceInventoryContract
                         .ForbiddenCoreProductionRetireSuccessFactories)
            {
                Assert.DoesNotContain(marker, coreSource);
            }
        }

        foreach (LegacyVmxRetainedCompatibilityInventoryEntry evidence in
                 LegacyVmxRetainedCompatibilitySurfaceInventoryContract.GeneratedDebugLifecycleConformanceInventory)
        {
            Assert.Contains("/Conformance/", evidence.RelativePath);
            string evidenceSource = File.ReadAllText(Path.Combine(
                projectRoot,
                evidence.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            foreach (string marker in evidence.RequiredMarkers)
            {
                Assert.Contains(marker, evidenceSource);
            }
        }
    }

    [Fact]
    public void LegacyVmxFreezeReadinessCertification_DeclaresFreezeAfterBroadDebtClosure()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        string legacyCompatibilityRoot = Path.Combine(projectRoot, "Legacy", "VMX", "Compatibility");
        var contract = new LegacyVmxFreezeReadinessCertificationContract();

        Assert.True(contract.IsSatisfied());
        Assert.True(LegacyVmxFreezeReadinessCertificationContract.CanDeclareFreeze);
        Assert.True(LegacyVmxFreezeReadinessCertificationContract.MoveAwayProbeProductionBuildPassedAfterCarrierExit);
        Assert.True(LegacyVmxFreezeReadinessCertificationContract.ConformanceFolderMoveAwayProbeWasExecuted);
        Assert.True(LegacyVmxFreezeReadinessCertificationContract.ConformanceFolderProductionBuildIndependent);
        Assert.True(LegacyVmxFreezeReadinessCertificationContract.ConformanceFolderTestBuildIndependent);
        Assert.True(LegacyVmxFreezeReadinessCertificationContract
            .ConformanceFolderDeletionSafeWithoutTestEvidenceDecoupling);
        Assert.Equal(0, LegacyVmxFreezeReadinessCertificationContract.ConformanceFolderMoveAwayProbeTestCompileErrors);
        Assert.Empty(LegacyVmxFreezeReadinessCertificationContract
            .ConformanceFolderMoveAwayProbeMissingEvidenceSymbols);
        Assert.Contains(
            "HybridCPU_ISE.Tests/VmxRefactoring/VmxProjectionSchemaAndQuarantineTests.cs",
            LegacyVmxFreezeReadinessCertificationContract.ConformanceFolderMoveAwayProbeTestDependents);
        Assert.True(LegacyVmxFreezeReadinessCertificationContract.BroadVmxFilterPassedAfterRepositoryPathRepair);
        Assert.Empty(LegacyVmxFreezeReadinessCertificationContract.MoveAwayProbeMissingSymbols);
        Assert.Empty(GetCSharpSourcesIfDirectoryExists(legacyCompatibilityRoot));

        string[] retainedCompatibilitySources = Directory.GetFiles(
                Path.Combine(projectRoot, "Core", "VMX", "Compatibility"),
                "*.cs",
                SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(projectRoot, path).Replace('\\', '/'))
            .ToArray();

        foreach (LegacyVmxFreezeReadinessInventoryEntry entry in
                 LegacyVmxFreezeReadinessCertificationContract.RetainedProductionCompatibilitySources)
        {
            Assert.Contains(entry.RelativePath, retainedCompatibilitySources);
            string retainedSource = File.ReadAllText(Path.Combine(
                projectRoot,
                entry.RelativePath.Replace('/', Path.DirectorySeparatorChar)));

            Assert.DoesNotContain("legacy", retainedSource, StringComparison.OrdinalIgnoreCase);
            foreach (string marker in entry.RequiredMarkers)
            {
                Assert.Contains(marker, retainedSource);
            }

            foreach (string marker in LegacyVmxFreezeReadinessCertificationContract
                         .ForbiddenRetainedProductionEvidenceMarkers)
            {
                Assert.DoesNotContain(marker, retainedSource);
            }
        }

        string combinedCallerSource = string.Empty;
        foreach (LegacyVmxFreezeReadinessInventoryEntry caller in
                 LegacyVmxFreezeReadinessCertificationContract.ProductionCallersProvingDeletionIsNotMechanical)
        {
            string callerSource = File.ReadAllText(Path.Combine(
                projectRoot,
                caller.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            combinedCallerSource += callerSource;
            foreach (string marker in caller.RequiredMarkers)
            {
                Assert.Contains(marker, callerSource);
            }
        }

        foreach (string missingSymbol in LegacyVmxFreezeReadinessCertificationContract.MoveAwayProbeMissingSymbols)
        {
            Assert.Contains(missingSymbol, combinedCallerSource);
        }

        Assert.All(
            LegacyVmxFreezeReadinessCertificationContract.BroadConformanceMatrix,
            entry => Assert.False(string.IsNullOrWhiteSpace(entry.RequiredOutcome)));
        Assert.Empty(LegacyVmxFreezeReadinessCertificationContract.KnownUnrelatedBroadFilterDebt);
        Assert.NotEmpty(LegacyVmxFreezeReadinessCertificationContract.ResolvedBroadFilterDebt);
        Assert.NotEmpty(LegacyVmxFreezeReadinessCertificationContract.OutOfScopeNonVmxBroadFilterDebt);
    }

    [Fact]
    public void AddressSpaceCanonicalIdentity_UsesNeutralSecondStageAndAddressSpaceTagsOnly()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        var contract = new AddressSpaceCanonicalIdentityAuthorityRemovalContract();
        var control = new MemoryDomainTranslationControl(
            TranslationEnabled: true,
            AddressSpaceTaggingEnabled: true,
            AddressSpaceRoot: 0x2000UL,
            SecondStageRoot: 0x4000UL,
            DomainTag: 7,
            AddressSpaceTag: 11,
            AddressSpaceGeneration: 13,
            DefaultMemoryType: MemoryDomainTranslationControl.WriteBackMemoryType);
        AddressSpaceId identity = control.ToAddressSpaceId(
            secondStageEpoch: 17,
            addressSpaceTagEpoch: 19);

        Assert.True(contract.IsNeutralRuntimeIdentity(control, 17, 19, identity));
        Assert.True(identity.MatchesSecondStageRoot(0x4000UL));
        Assert.True(identity.MatchesAddressSpaceTag(11));

        string addressSpaceSource = File.ReadAllText(Path.Combine(
            projectRoot,
            AddressSpaceCanonicalIdentityAuthorityRemovalContract.AddressSpaceIdentityPath.Replace('/', Path.DirectorySeparatorChar)));
        string tagSource = File.ReadAllText(Path.Combine(
            projectRoot,
            AddressSpaceCanonicalIdentityAuthorityRemovalContract.NestedTlbTagPath.Replace('/', Path.DirectorySeparatorChar)));
        string tlbSource = File.ReadAllText(Path.Combine(
            projectRoot,
            AddressSpaceCanonicalIdentityAuthorityRemovalContract.TlbPath.Replace('/', Path.DirectorySeparatorChar)));
        string genericIommuSource = File.ReadAllText(Path.Combine(
            projectRoot,
            AddressSpaceCanonicalIdentityAuthorityRemovalContract.GenericIommuPath.Replace('/', Path.DirectorySeparatorChar)));
        string domainBoundIommuSource = File.ReadAllText(Path.Combine(
            projectRoot,
            AddressSpaceCanonicalIdentityAuthorityRemovalContract.DomainBoundIommuPath.Replace('/', Path.DirectorySeparatorChar)));
        string pageWalkerSource = File.ReadAllText(Path.Combine(
            projectRoot,
            AddressSpaceCanonicalIdentityAuthorityRemovalContract.PageWalkerPath.Replace('/', Path.DirectorySeparatorChar)));
        string pageWalkerTranslateSource = File.ReadAllText(Path.Combine(
            projectRoot,
            AddressSpaceCanonicalIdentityAuthorityRemovalContract.PageWalkerTranslatePath.Replace('/', Path.DirectorySeparatorChar)));
        string compositionSource = File.ReadAllText(Path.Combine(
            projectRoot,
            AddressSpaceCanonicalIdentityAuthorityRemovalContract.NestedCompositionPath.Replace('/', Path.DirectorySeparatorChar)));
        string descriptorSource = File.ReadAllText(Path.Combine(
            projectRoot,
            AddressSpaceCanonicalIdentityAuthorityRemovalContract.MemoryDomainDescriptorPath.Replace('/', Path.DirectorySeparatorChar)));
        string compatibilitySource = File.ReadAllText(Path.Combine(
            projectRoot,
            AddressSpaceCanonicalIdentityAuthorityRemovalContract.CompatibilityTranslationPath.Replace('/', Path.DirectorySeparatorChar)));

        foreach (string marker in AddressSpaceCanonicalIdentityAuthorityRemovalContract.ForbiddenCanonicalIdentityMarkers)
        {
            Assert.DoesNotContain(marker, addressSpaceSource);
            Assert.DoesNotContain(marker, tagSource);
            Assert.DoesNotContain(marker, tlbSource);
        }

        foreach (string marker in AddressSpaceCanonicalIdentityAuthorityRemovalContract.ForbiddenNestedCompositionMarkers)
        {
            Assert.DoesNotContain(marker, compositionSource);
        }

        Assert.Contains("MemoryDomainTranslationControl TranslationControl", descriptorSource);
        Assert.DoesNotContain("MemoryTranslationControl TranslationControl", descriptorSource);
        Assert.Contains("IsReadOnlyCompatibilityProjection", compatibilitySource);
        Assert.DoesNotContain("DomainControl", compatibilitySource);
        Assert.DoesNotContain("ToAddressSpaceId(", compatibilitySource);
        Assert.Contains("MemoryDomainTranslationControl ChildTranslationControl", compositionSource);
        Assert.Contains("FlushNestedBySecondStageRoot", tlbSource);
        Assert.Contains("FlushNestedByAddressSpaceTag", tlbSource);
        Assert.DoesNotContain("vmxActive", genericIommuSource);
        Assert.DoesNotContain("guestCR3", genericIommuSource);
        Assert.DoesNotContain("eptPointer", genericIommuSource);
        Assert.Contains("MemoryDomainTranslationControl domainControl", domainBoundIommuSource);
        Assert.DoesNotContain("MemoryTranslationControl compatibilityControl", domainBoundIommuSource);
        Assert.DoesNotContain("nptControl", domainBoundIommuSource);
        Assert.DoesNotContain("eptEpoch", domainBoundIommuSource);
        Assert.DoesNotContain("vpidEpoch", domainBoundIommuSource);
        Assert.Contains("MemoryDomainTranslationControl control", pageWalkerSource);
        Assert.Contains("MemoryDomainTranslationControl control", pageWalkerTranslateSource);
        Assert.DoesNotContain("MemoryTranslationControl control", pageWalkerSource);
        Assert.DoesNotContain("MemoryTranslationControl control", pageWalkerTranslateSource);
        Assert.DoesNotContain("WalkNpt", pageWalkerSource);
        Assert.DoesNotContain("NptViolation", pageWalkerSource);
    }

    [Fact]
    public void TranslationIoLaneIdentity_RemovesVmidNptAuthorityFromExecutableKeys()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        var contract = new TranslationIoLaneIdentityAuthorityRemovalContract();
        IommuDomainBinding binding = IommuDomainBinding.Create(
            ioDomainTag: 3,
            domainId: 4,
            domainTag: 0x100UL,
            deviceId: 6,
            permissions: IOMMUAccessPermissions.ReadWrite,
            domainEpoch: 7);
        IotlbTag tag = IotlbTag.Create(
            binding,
            ioVirtualAddress: 0x2000UL,
            permissions: IOMMUAccessPermissions.Read,
            mappingEpoch: 9);

        Assert.True(contract.IsNeutralIotlbIdentity(binding, tag));

        foreach (string relativePath in TranslationIoLaneIdentityAuthorityRemovalContract.IoIdentityPaths)
        {
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            foreach (string marker in TranslationIoLaneIdentityAuthorityRemovalContract.ForbiddenExecutableIdentityMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }

        foreach (string relativePath in TranslationIoLaneIdentityAuthorityRemovalContract.LaneIdentityPaths)
        {
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            foreach (string marker in TranslationIoLaneIdentityAuthorityRemovalContract.ForbiddenExecutableIdentityMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }

        string iommuSource = File.ReadAllText(Path.Combine(
            projectRoot,
            TranslationIoLaneIdentityAuthorityRemovalContract.DomainBoundIommuPath.Replace('/', Path.DirectorySeparatorChar)));
        string resultSource = File.ReadAllText(Path.Combine(
            projectRoot,
            TranslationIoLaneIdentityAuthorityRemovalContract.NestedTranslationResultPath.Replace('/', Path.DirectorySeparatorChar)));
        string compatibilityControlSource = File.ReadAllText(Path.Combine(
            projectRoot,
            TranslationIoLaneIdentityAuthorityRemovalContract.CompatibilityTranslationControlPath.Replace('/', Path.DirectorySeparatorChar)));
        string compatAliasSource = File.ReadAllText(Path.Combine(
            projectRoot,
            TranslationIoLaneIdentityAuthorityRemovalContract.CompatibilityIoAliasPath.Replace('/', Path.DirectorySeparatorChar)));
        string removedNestedComposer = Path.Combine(
            projectRoot,
            TranslationIoLaneIdentityAuthorityRemovalContract.RemovedNestedProjectionComposerPath.Replace('/', Path.DirectorySeparatorChar));

        Assert.Contains("MemoryDomainTranslationControl domainControl", iommuSource);
        Assert.DoesNotContain("MemoryTranslationControl compatibilityControl", iommuSource);
        foreach (string marker in TranslationIoLaneIdentityAuthorityRemovalContract.ForbiddenNestedResultMarkers)
        {
            Assert.DoesNotContain(marker, resultSource);
        }

        Assert.Contains("SecondStageViolation", resultSource);
        Assert.Contains("SecondStageMisconfiguration", resultSource);
        foreach (string marker in TranslationIoLaneIdentityAuthorityRemovalContract.ForbiddenCompatibilityFactoryMarkers)
        {
            Assert.DoesNotContain(marker, compatibilityControlSource);
        }

        Assert.False(File.Exists(removedNestedComposer));
        Assert.Contains("InvalidateVmxIotlbByVmid", compatAliasSource);
        Assert.Contains("VmxCompatibilityIoAliasesAreReadOnlyDenied", compatAliasSource);
        Assert.DoesNotContain("InvalidateIotlbByIoDomainTag(", compatAliasSource);
    }

    [Fact]
    public void Lane6HostOwnedEvidence_MovesToNeutralRebuildOnlyStoreAndRejectsEpochWraparound()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");

        Assert.True(Lane6HostOwnedEvidenceBoundaryRemovalContract.RemovedVmxShapedHostTokenCarrier);
        Assert.True(Lane6HostOwnedEvidenceBoundaryRemovalContract.RequiresHostEvidenceRebuildAfterRestore);
        Assert.True(Lane6HostOwnedEvidenceBoundaryRemovalContract.RejectsEpochWraparound);
        Assert.False(File.Exists(Path.Combine(
            projectRoot,
            Lane6HostOwnedEvidenceBoundaryRemovalContract.RemovedVmxQueueVirtualizerPath.Replace('/', Path.DirectorySeparatorChar))));

        string stateSource = File.ReadAllText(Path.Combine(
            projectRoot,
            Lane6HostOwnedEvidenceBoundaryRemovalContract.Lane6StatePath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in Lane6HostOwnedEvidenceBoundaryRemovalContract.ForbiddenLane6StateOwnerMarkers)
        {
            Assert.DoesNotContain(marker, stateSource);
        }
        Assert.Contains("Lane6QueueRuntime", stateSource);
        Assert.Contains("Lane6HostOwnedEvidenceStore", stateSource);

        string evidenceSource = File.ReadAllText(Path.Combine(
            projectRoot,
            Lane6HostOwnedEvidenceBoundaryRemovalContract.NeutralHostEvidenceStorePath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in Lane6HostOwnedEvidenceBoundaryRemovalContract.ForbiddenNeutralEvidenceStoreMarkers)
        {
            Assert.DoesNotContain(marker, evidenceSource);
        }
        foreach (string marker in Lane6HostOwnedEvidenceBoundaryRemovalContract.RequiredRebuildMarkers)
        {
            Assert.Contains(marker, evidenceSource);
        }

        string queueRuntimeSource = File.ReadAllText(Path.Combine(
            projectRoot,
            Lane6HostOwnedEvidenceBoundaryRemovalContract.NeutralQueueRuntimePath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in Lane6HostOwnedEvidenceBoundaryRemovalContract.RequiredFailClosedEpochMarkers)
        {
            Assert.Contains(marker, queueRuntimeSource);
        }
        Assert.DoesNotContain("Vmx", queueRuntimeSource);
        Assert.DoesNotContain("Vmcs", queueRuntimeSource);
        string migrationSource = File.ReadAllText(Path.Combine(
            projectRoot,
            Lane6HostOwnedEvidenceBoundaryRemovalContract.MigrationDescriptorPath.Replace('/', Path.DirectorySeparatorChar)));
        string checkpointSource = File.ReadAllText(Path.Combine(
            projectRoot,
            Lane6HostOwnedEvidenceBoundaryRemovalContract.DomainCheckpointImagePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("MigrationPayloadClass.NativeTokenEvidence", migrationSource);
        Assert.Contains("MustRecomputeAfterRestore", migrationSource);
        Assert.Contains("ContainsHostOwnedEvidence", checkpointSource);
        Assert.Contains("EvidenceVisibilityClass.NativeTokenEvidence", checkpointSource);

        var token = new Lane6VirtualToken(
            IoDomainTag: 3,
            OwnerVirtualThreadId: 1,
            DomainId: 4,
            DomainTag: 5,
            DeviceId: 6,
            GuestTokenId: 7,
            VirtualTokenId: 8,
            GuestFenceId: 9,
            QueueEpoch: 10,
            FenceEpoch: 11);
        var originalHandle = new DmaStreamComputeTokenHandle(20, 1, 2, 3, 4, 5, 6, 7);
        var rebuiltHandle = new DmaStreamComputeTokenHandle(21, 1, 2, 3, 4, 5, 6, 8);
        var evidenceStore = new Lane6HostOwnedEvidenceStore();
        Assert.True(evidenceStore.TryBind(token, originalHandle));
        Assert.True(evidenceStore.TryResolve(token, out DmaStreamComputeTokenHandle observedOriginal));
        Assert.Equal(originalHandle, observedOriginal);

        Lane6HostEvidenceRestoreResult prepare = evidenceStore.PrepareForRestore(EvidencePolicyDescriptor.FailClosed);
        Assert.True(prepare.RequiresRebuild);
        Assert.False(evidenceStore.TryResolve(token, out _));
        Assert.Equal(
            Lane6HostEvidenceRestoreDecision.Rebuilt,
            evidenceStore.RebuildAfterRestore(token, rebuiltHandle).Decision);
        Assert.True(evidenceStore.TryResolve(token, out DmaStreamComputeTokenHandle observedRebuilt));
        Assert.Equal(rebuiltHandle, observedRebuilt);

        var binding = IommuDomainBinding.Create(
            ioDomainTag: 3,
            domainId: 4,
            domainTag: 5,
            deviceId: 6,
            permissions: IOMMUAccessPermissions.ReadWrite);
        var exhaustedQueueRuntime = new Lane6QueueRuntime(1, ulong.MaxValue, ulong.MaxValue);
        Assert.False(exhaustedQueueRuntime.TryEnsureQueue(binding, 1, 0, out _, out DmaFault queueFault));
        Assert.Equal(DmaFaultKind.EpochExhausted, queueFault.Kind);
        Assert.False(exhaustedQueueRuntime.TryObserveFence(binding, 1, out DmaFault fenceFault));
        Assert.Equal(DmaFaultKind.EpochExhausted, fenceFault.Kind);
    }

    [Fact]
    public void Lane7HostOwnedEvidence_MovesToNeutralRebuildOnlyStoreAndKeepsFrozenVocabReadOnly()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        var contract = new Lane7HostOwnedEvidenceSubstrateExtractionContract();

        Assert.True(Lane7HostOwnedEvidenceSubstrateExtractionContract.ExtractsLane7HostEvidenceToNeutralRuntimeNamespace);
        Assert.True(Lane7HostOwnedEvidenceSubstrateExtractionContract.RequiresTokenBackendAndSchedulerEvidenceRebuildAfterRestore);
        Assert.True(Lane7HostOwnedEvidenceSubstrateExtractionContract.KeepsVmxTranslationVmcsAndIotlbNamesFrozenCompatibilityOnly);

        string stateSource = File.ReadAllText(Path.Combine(
            projectRoot,
            Lane7HostOwnedEvidenceSubstrateExtractionContract.Lane7StatePath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in Lane7HostOwnedEvidenceSubstrateExtractionContract.ForbiddenLane7StateCacheMarkers)
        {
            Assert.DoesNotContain(marker, stateSource);
        }
        foreach (string marker in Lane7HostOwnedEvidenceSubstrateExtractionContract.RequiredLane7StateDelegationMarkers)
        {
            Assert.Contains(marker, stateSource);
        }

        string evidenceSource = File.ReadAllText(Path.Combine(
            projectRoot,
            Lane7HostOwnedEvidenceSubstrateExtractionContract.NeutralHostEvidenceStorePath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in Lane7HostOwnedEvidenceSubstrateExtractionContract.ForbiddenNeutralEvidenceStoreMarkers)
        {
            Assert.DoesNotContain(marker, evidenceSource);
        }
        foreach (string marker in Lane7HostOwnedEvidenceSubstrateExtractionContract.RequiredNeutralEvidenceStoreMarkers)
        {
            Assert.Contains(marker, evidenceSource);
        }

        string checkpointSource = File.ReadAllText(Path.Combine(
            projectRoot,
            Lane7HostOwnedEvidenceSubstrateExtractionContract.Lane7CheckpointPath.Replace('/', Path.DirectorySeparatorChar)));
        string checkpointEvidenceSource = File.ReadAllText(Path.Combine(
            projectRoot,
            Lane7HostOwnedEvidenceSubstrateExtractionContract.Lane7CheckpointEvidencePath.Replace('/', Path.DirectorySeparatorChar)));
        string tokenEvidenceSource = File.ReadAllText(Path.Combine(
            projectRoot,
            Lane7HostOwnedEvidenceSubstrateExtractionContract.Lane7VirtualTokenEvidencePath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in Lane7HostOwnedEvidenceSubstrateExtractionContract.ForbiddenCheckpointEvidenceLeakMarkers)
        {
            Assert.DoesNotContain(marker, checkpointSource);
            Assert.DoesNotContain(marker, checkpointEvidenceSource);
            Assert.DoesNotContain(marker, tokenEvidenceSource);
        }
        Assert.Contains("HostEvidence.PrepareForRestore(EvidencePolicyDescriptor.FailClosed)", checkpointSource);
        Assert.Contains("public bool ContainsNativeTokenHandle(AcceleratorTokenHandle hostHandle) => false", checkpointEvidenceSource);
        Assert.Contains("public bool ExposesHostTokenHandle(AcceleratorTokenHandle hostHandle) =>", tokenEvidenceSource);
        Assert.Contains("false;", tokenEvidenceSource);

        foreach (string relativePath in new[]
        {
            Lane7HostOwnedEvidenceSubstrateExtractionContract.FrozenMemoryTranslationControlPath,
            Lane7HostOwnedEvidenceSubstrateExtractionContract.FrozenVmcsFieldProjectionPath,
            Lane7HostOwnedEvidenceSubstrateExtractionContract.FrozenIotlbCompatibilityAliasPath,
        })
        {
            string compatibilitySource = File.ReadAllText(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains(
                Lane7HostOwnedEvidenceSubstrateExtractionContract.RequiredFrozenCompatibilityMarkers[
                    Array.IndexOf(new[]
                    {
                        Lane7HostOwnedEvidenceSubstrateExtractionContract.FrozenMemoryTranslationControlPath,
                        Lane7HostOwnedEvidenceSubstrateExtractionContract.FrozenVmcsFieldProjectionPath,
                        Lane7HostOwnedEvidenceSubstrateExtractionContract.FrozenIotlbCompatibilityAliasPath,
                    }, relativePath)],
                compatibilitySource);
        }

        var virtualToken = new Lane7VirtualToken(
            ExecutionDomainTag: 3,
            AddressSpaceTag: 4,
            OwnerVirtualThreadId: 1,
            VirtualHandle: 0x7000_0000_0000_0020UL,
            VirtualTokenId: 0x7100_0000_0000_0030UL,
            TokenEpoch: 5,
            Status: new AcceleratorTokenStatusWord(
                AcceleratorTokenState.Created,
                AcceleratorTokenFaultCode.None,
                AcceleratorTokenStatusFlags.ModelOnly,
                1),
            CompletionEpoch: 6);
        AcceleratorTokenHandle originalHostHandle = AcceleratorTokenHandle.FromOpaqueValue(0xA000_0000_0000_0001UL);
        AcceleratorTokenHandle rebuiltHostHandle = AcceleratorTokenHandle.FromOpaqueValue(0xA000_0000_0000_0002UL);
        var store = new Lane7HostOwnedEvidenceStore();

        Assert.True(store.TryBindToken(virtualToken, originalHostHandle));
        Assert.True(store.TryResolveHostToken(virtualToken.VirtualTokenId, out AcceleratorTokenHandle observedOriginal));
        Assert.Equal(originalHostHandle, observedOriginal);
        Assert.Equal(
            Lane7HostEvidenceRestoreDecision.Rebuilt,
            store.RebuildBackendAfterRestore(
                virtualToken.ExecutionDomainTag,
                virtualToken.OwnerVirtualThreadId,
                virtualToken.VirtualHandle,
                backendGeneration: 11,
                out Lane7BackendBinding rebuiltBackend).Decision);
        Assert.True(rebuiltBackend.IsUsable);
        Assert.Equal(1, store.ActiveBackendBindingCount);
        Lane7PressureSnapshot pressure = store.ObserveSubmitPollPressure(
            virtualToken.ExecutionDomainTag,
            virtualToken.AddressSpaceTag,
            virtualToken.OwnerVirtualThreadId,
            inflightTokens: 1,
            new Lane7QuotaPolicy(1, 1, 1));
        Assert.Equal((ulong)1, pressure.PressureEpoch);

        Lane7HostEvidenceRestoreResult prepare = store.PrepareForRestore(EvidencePolicyDescriptor.FailClosed);
        Assert.True(prepare.RequiresRebuild);
        Assert.Equal(0, store.ActiveTokenBindingCount);
        Assert.Equal(0, store.ActiveBackendBindingCount);
        Assert.False(store.TryResolveHostToken(virtualToken.VirtualTokenId, out _));
        Assert.Equal(default, store.LastPressure);

        Assert.Equal(
            Lane7HostEvidenceRestoreDecision.Rebuilt,
            store.RebuildTokenAfterRestore(virtualToken, rebuiltHostHandle).Decision);
        Assert.True(store.TryResolveHostToken(virtualToken.VirtualTokenId, out AcceleratorTokenHandle observedRebuilt));
        Assert.Equal(rebuiltHostHandle, observedRebuilt);

        var lane7 = new Lane7StateBlock();
        lane7.ConfigureOwnership(
            executionDomainTag: 3,
            addressSpaceTag: 4,
            quotaPolicy: new Lane7QuotaPolicy(4, 4, 2));
        Lane7VirtualHandle handle = lane7.AllocateVirtualHandle(
            ownerVirtualThreadId: 1,
            AcceleratorDeviceId.ReferenceMatMul,
            Lane7VirtualCapability.QueryCaps | Lane7VirtualCapability.Submit);
        Assert.True(lane7.TryBindBackend(handle.Value, backendGeneration: 21, out Lane7BackendBinding binding));
        Assert.True(binding.IsUsable);
        Assert.Equal(1, lane7.HostEvidence.ActiveBackendBindingCount);

        Lane7Checkpoint checkpoint = lane7.CreateCheckpoint();
        Assert.True(contract.RejectsCheckpointNativeHandleProjection(checkpoint, AcceleratorTokenHandle.FromOpaqueValue(handle.Value)));
        Assert.False(checkpoint.ContainsHostEvidence(default));
        lane7.RestoreCheckpoint(checkpoint);
        Assert.Equal(0, lane7.HostEvidence.ActiveBackendBindingCount);
        Assert.Equal(0, lane7.HostEvidence.ActiveTokenBindingCount);
        Assert.Equal(0UL, lane7.PressureEpoch);
        Assert.Equal(
            Lane7HostEvidenceRestoreDecision.Rebuilt,
            lane7.RebuildBackendBindingAfterRestore(handle.Value, backendGeneration: 22, out Lane7BackendBinding restoredBinding).Decision);
        Assert.True(restoredBinding.IsUsable);

        AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(projectRoot);
    }

    [Fact]
    public void DomainRuntimeSubstrate_MovesToNeutralRuntimeDomainsAndKeepsCompatAliasesFrozen()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");

        foreach (string relativePath in DomainRuntimeSubstrateExtractionContract.RemovedDomainRuntimeSubstratePaths)
        {
            Assert.False(File.Exists(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        string combinedNeutralRuntimeSource = string.Empty;
        foreach (string relativePath in DomainRuntimeSubstrateExtractionContract.NeutralDomainRuntimePaths)
        {
            string sourcePath = Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(sourcePath));
            string source = File.ReadAllText(sourcePath);
            combinedNeutralRuntimeSource += source;
            foreach (string marker in DomainRuntimeSubstrateExtractionContract.ForbiddenNeutralDomainRuntimeMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }

        foreach (string marker in DomainRuntimeSubstrateExtractionContract.RequiredNeutralDomainRuntimeMarkers)
        {
            Assert.Contains(marker, combinedNeutralRuntimeSource);
        }

        string admissionSource = File.ReadAllText(Path.Combine(
            projectRoot,
            DomainRuntimeSubstrateExtractionContract.RuntimeBoundaryAdmissionPath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in DomainRuntimeSubstrateExtractionContract.RequiredRuntimeAdmissionMarkers)
        {
            Assert.Contains(marker, admissionSource);
        }

        string[] frozenPaths =
        {
            DomainRuntimeSubstrateExtractionContract.FrozenMemoryTranslationControlPath,
            DomainRuntimeSubstrateExtractionContract.FrozenVmcsFieldProjectionPath,
            DomainRuntimeSubstrateExtractionContract.FrozenIotlbCompatibilityAliasPath,
            DomainRuntimeSubstrateExtractionContract.FrozenOpcodeVocabularyPath,
        };
        for (int index = 0; index < frozenPaths.Length; index++)
        {
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                frozenPaths[index].Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains(
                DomainRuntimeSubstrateExtractionContract.RequiredFrozenCompatibilityMarkers[index],
                source);
        }

        const ulong capability = VmxV2InstructionCaps.VmFunc;
        var context = new DomainRuntimeContext(
            execution: new ExecutionDomainDescriptor(
                domainTag: 3,
                bundleLegality: new YAKSys_Hybrid_CPU.Core.BundleLegalityDescriptor(),
                schedulingBudget: new SchedulingBudgetDescriptor(
                    SchedulingBudgetAuthority.Runtime,
                    maxOperationsPerEpoch: 0,
                    requiresSystemSingletonLane: false,
                    pinnedLaneId: 0),
                extension: null,
                compatibilityProjectionEnabled: true),
            memory: new MemoryDomainDescriptor(),
            io: new IoDomainDescriptor(),
            capabilities: new CapabilityDescriptorSet(
                globalHardwareCaps: capability,
                runtimeEnabledCaps: capability,
                domainGrantedCaps: capability));
        var root = new RootAuthorityDescriptor(
            RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 1,
            grantedCapabilityMask: capability,
            allowCompatibilityFrontendActivation: true,
            allowAuthoritativeStateMutation: true);
        var operation = new DomainRuntimeOperation(
            DomainRuntimeOperationKind.EnterDomain,
            DomainRuntimeOperationSource.RuntimeService,
            requiresCapabilityGrant: true,
            isProjectionOnly: false);
        var authority = new DomainRuntimeAuthority();
        DomainRuntimeAuthorityResult authorityResult = authority.Validate(
            root,
            context,
            operation,
            CapabilityBoundaryRequirement.TypedGrant(
                capability,
                CapabilityGrantScope.CompatibilityProjection));

        Assert.True(authorityResult.IsAllowed);
        Assert.True(new DomainLegalityService().Validate(
            context,
            new DomainRuntimeOperation(
                DomainRuntimeOperationKind.ReadCompatibilityProjection,
                DomainRuntimeOperationSource.RuntimeService,
                requiresCapabilityGrant: false,
                isProjectionOnly: true)).IsValid);
        Assert.True(new DomainSchedulingAdmission().Admit(context, laneId: 0).IsValid);
        Assert.True(new DomainBindingTable().CanBind(new DomainBindingRequest(
            new DomainBindingEntry(
                DomainBindingAuthority.Runtime,
                DomainId: 10,
                context,
                AllowsCompatibilityProjection: true),
            ExpectedDomainId: 10,
            RequiresCompatibilityProjection: true)));

        AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(projectRoot);
    }

    [Fact]
    public void GenericDomainSubstrate_MovesDescriptorsAndAdmissionToNeutralRuntime()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");

        foreach (string relativePath in GenericDomainSubstrateExtractionContract.RemovedGenericDomainSubstratePaths)
        {
            Assert.False(File.Exists(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        string combinedNeutralSource = string.Empty;
        foreach (string relativePath in GenericDomainSubstrateExtractionContract.NeutralRuntimePaths)
        {
            string sourcePath = Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(sourcePath));
            string source = File.ReadAllText(sourcePath);
            combinedNeutralSource += source;
            foreach (string marker in GenericDomainSubstrateExtractionContract.ForbiddenNeutralRuntimeMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }

        foreach (string marker in GenericDomainSubstrateExtractionContract.RequiredNeutralRuntimeMarkers)
        {
            Assert.Contains(marker, combinedNeutralSource);
        }

        Assert.True(File.Exists(Path.Combine(
            projectRoot,
            GenericDomainSubstrateExtractionContract.CapabilityDescriptorSetPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Contains(
            "VmxCompatibility",
            File.ReadAllText(Path.Combine(
                projectRoot,
                GenericDomainSubstrateExtractionContract.CapabilityGeneratedSchemaPath.Replace('/', Path.DirectorySeparatorChar))));

        string[] frozenPaths = GenericDomainSubstrateExtractionContract.FrozenCompatibilityProjectionPaths;
        for (int index = 0; index < frozenPaths.Length; index++)
        {
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                frozenPaths[index].Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains(
                GenericDomainSubstrateExtractionContract.RequiredFrozenCompatibilityMarkers[index],
                source);
        }

        var executionDescriptor = new ExecutionDomainDescriptor(
            domainTag: 3,
            bundleLegality: new YAKSys_Hybrid_CPU.Core.BundleLegalityDescriptor(),
            schedulingBudget: new SchedulingBudgetDescriptor(
                SchedulingBudgetAuthority.Runtime,
                maxOperationsPerEpoch: 0,
                requiresSystemSingletonLane: false,
                pinnedLaneId: 0),
            extension: null,
            compatibilityProjectionEnabled: true);
        Assert.True(new ExecutionDomainRuntime().Validate(
            new ExecutionDomainRuntimeRequest(
                executionDescriptor,
                RequiresBundleLegality: true,
                RequiresSchedulingBudget: true,
                RequiresCompatibilityProjection: true)).IsAllowed);

        Assert.True(new MemoryDomainRuntime().Validate(
            new MemoryDomainRuntimeRequest(
                new MemoryDomainDescriptor(),
                RequiresAddressSpace: false,
                RequiresTranslationPolicy: false,
                RequiresSecondStageTranslation: false,
                RequiresDirtyTracking: false)).IsAllowed);

        Assert.True(new IoDomainRuntime().Validate(
            new IoDomainRuntimeRequest(
                new IoDomainDescriptor(),
                RequiresDmaAuthority: true,
                RequiresIommuAuthority: true,
                RequiresVirtualizationBlock: false,
                RequiresDmaWindow: false,
                RequiresCompatibilityProjection: true)).IsAllowed);

        Assert.True(new Lane6DomainRuntime().Validate(
            new Lane6DomainRuntimeRequest(
                new Lane6DomainDescriptor(),
                RequiresTokenNamespace: false,
                RequiresQueueNamespace: false,
                RequiresFenceDomain: false,
                RequiresCompatibilityProjection: true)).IsAllowed);

        var lane7Descriptor = new Lane7AcceleratorDescriptor(
            Lane7AcceleratorAuthority.Runtime,
            VirtualizationLaneBindingPolicy.Lane7Id,
            backendBindingId: 0,
            handleNamespaceId: 0,
            tokenNamespaceId: 0,
            completionRouteId: 0,
            requiresRuntimeBackendBinding: false,
            allowsCompatibilityProjection: true);
        Assert.True(new Lane7DomainRuntime().Validate(
            new Lane7DomainRuntimeRequest(
                lane7Descriptor,
                RequiresBackendBinding: false,
                RequiresHandleNamespace: false,
                RequiresTokenNamespace: false,
                RequiresCompletionRoute: false,
                RequiresCompatibilityProjection: true)).IsAllowed);

        var nestedDescriptor = new NestedDomainDescriptor(
            NestedDomainAuthority.Runtime,
            parentDomainId: 1,
            childDomainId: 2,
            capabilities: NestedCapabilityGrantMask.None,
            domainCompositionEnabled: true,
            allowsCompatibilityProjection: true,
            hostEvidenceExcluded: true,
            lanePassthroughBlocked: true);
        NestedDomainRuntimeResult nestedAdmission = new NestedDomainRuntime().Validate(
            new NestedDomainRuntimeRequest(
                nestedDescriptor,
                NestedCapabilityFilterResult.Allowed,
                RequiresCompatibilityProjection: true));
        Assert.True(nestedAdmission.IsAllowed);
        Assert.True(new NestedProjectionService().Validate(
            new NestedProjectionRequest(
                nestedDescriptor,
                nestedAdmission,
                NestedCapabilityFilterResult.Allowed,
                NestedEvidencePolicyResult.Allowed,
                default,
                RequiresCompletionMapping: false,
                RequiresCompatibilityProjection: true)).IsAllowed);

        AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(projectRoot);
    }

    [Fact]
    public void CapabilityProjectionPlacementServiceSubstrate_MovesNeutralServicesAndQuarantinesCompatVocabulary()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        var contract = new CapabilityProjectionPlacementServiceSubstrateExtractionContract();

        Assert.True(contract.IsSatisfied());
        Assert.Equal(
            CapabilityProjectionPlacementServiceSubstrateExtractionContract.CapabilityGeneratedProjectionPath,
            GeneratedProjectionLineageBuildContract.VmxCapsCapabilityBitSourcePath);

        Assert.False(File.Exists(Path.Combine(
            projectRoot,
            CapabilityProjectionPlacementServiceSubstrateExtractionContract.RemovedCapabilityGeneratedProjectionPath.Replace('/', Path.DirectorySeparatorChar))));
        string capabilityProjectionPath = Path.Combine(
            projectRoot,
            CapabilityProjectionPlacementServiceSubstrateExtractionContract.CapabilityGeneratedProjectionPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(capabilityProjectionPath));
        string capabilityProjectionSource = File.ReadAllText(capabilityProjectionPath);
        Assert.Contains("VmxCompatibilityBitTable", capabilityProjectionSource);
        Assert.Contains("CapabilityGrantCollection.TypedGrant", capabilityProjectionSource);
        Assert.DoesNotContain("VmcsManager", capabilityProjectionSource);
        Assert.DoesNotContain("VmcsV2Runtime", capabilityProjectionSource);

        foreach (string relativePath in CapabilityProjectionPlacementServiceSubstrateExtractionContract.RemovedNeutralServiceSubstratePaths)
        {
            Assert.False(File.Exists(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        string combinedNeutralSource = string.Empty;
        foreach (string relativePath in CapabilityProjectionPlacementServiceSubstrateExtractionContract.NeutralRuntimeServicePaths)
        {
            string sourcePath = Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(sourcePath));
            string source = File.ReadAllText(sourcePath);
            combinedNeutralSource += source;
            foreach (string marker in CapabilityProjectionPlacementServiceSubstrateExtractionContract.ForbiddenNeutralRuntimeMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }

        Assert.Contains("MemoryTranslationPolicy", combinedNeutralSource);
        Assert.Contains("IotlbInvalidationService", combinedNeutralSource);
        Assert.Contains("DomainCheckpointImage", combinedNeutralSource);
        Assert.Contains("LaneCompletionRouter", combinedNeutralSource);
        Assert.Contains("EventDeliveryService", combinedNeutralSource);
        Assert.Contains("Lane6StateBlock", combinedNeutralSource);

        string verifierScript = File.ReadAllText(Path.Combine(
            projectRoot,
            GeneratedProjectionLineageBuildContract.VerifierScriptPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains(
            CapabilityProjectionPlacementServiceSubstrateExtractionContract.CapabilityGeneratedProjectionPath,
            verifierScript);
        Assert.DoesNotContain(
            CapabilityProjectionPlacementServiceSubstrateExtractionContract.RemovedCapabilityGeneratedProjectionPath,
            verifierScript);

        for (int index = 0; index < CapabilityProjectionPlacementServiceSubstrateExtractionContract.FrozenCompatibilityQuarantinePaths.Length; index++)
        {
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                CapabilityProjectionPlacementServiceSubstrateExtractionContract.FrozenCompatibilityQuarantinePaths[index].Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains(
                CapabilityProjectionPlacementServiceSubstrateExtractionContract.RequiredCompatibilityQuarantineMarkers[index],
                source);
        }

        string projectFile = File.ReadAllText(Path.Combine(projectRoot, "HybridCPU_ISE.csproj"));
        Assert.Contains("Core\\VMX\\Compatibility\\Generated\\CapabilityProjection\\", projectFile);
        foreach (string marker in CapabilityProjectionPlacementServiceSubstrateExtractionContract.RemovedProjectFolderMarkers)
        {
            Assert.DoesNotContain(marker, projectFile);
        }

        AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(projectRoot);
    }

    [Fact]
    public void CoreVmxSubstrateResiduals_MoveNeutralStateAndLeaveFrozenCompatibilityQuarantine()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        var contract = new CoreVmxSubstrateResidualExtractionContract();

        Assert.True(contract.IsSatisfied());

        foreach (string relativePath in CoreVmxSubstrateResidualExtractionContract.RemovedNeutralResidualSubstratePaths)
        {
            Assert.False(File.Exists(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        string combinedNeutralSource = string.Empty;
        foreach (string relativePath in CoreVmxSubstrateResidualExtractionContract.NeutralRuntimeResidualPaths)
        {
            string sourcePath = Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(sourcePath));
            string source = File.ReadAllText(sourcePath);
            combinedNeutralSource += source;
            foreach (string marker in CoreVmxSubstrateResidualExtractionContract.ForbiddenNeutralRuntimeMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }

        foreach (string marker in CoreVmxSubstrateResidualExtractionContract.RequiredNeutralRuntimeMarkers)
        {
            Assert.Contains(marker, combinedNeutralSource);
        }

        for (int index = 0; index < CoreVmxSubstrateResidualExtractionContract.FrozenCompatibilityQuarantinePaths.Length; index++)
        {
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                CoreVmxSubstrateResidualExtractionContract.FrozenCompatibilityQuarantinePaths[index].Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains(
                CoreVmxSubstrateResidualExtractionContract.RequiredFrozenCompatibilityMarkers[index],
                source);
        }

        string projectFile = File.ReadAllText(Path.Combine(projectRoot, "HybridCPU_ISE.csproj"));
        Assert.DoesNotContain("Core\\VMX\\Substrate\\", projectFile);

        AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(projectRoot);
    }

    [Fact]
    public void VmxCompletionTrapNestedRetireSurfaces_QuarantineCompatibilityAndUseNeutralAuthority()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        var contract = new VmxCompletionTrapNestedRetireQuarantineContract();

        Assert.True(contract.IsSatisfied());

        foreach (string relativePath in VmxCompletionTrapNestedRetireQuarantineContract.RemovedCompatibilitySubstratePaths)
        {
            Assert.False(File.Exists(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        string combinedNeutralSource = string.Empty;
        foreach (string relativePath in VmxCompletionTrapNestedRetireQuarantineContract.NeutralAuthorityPaths)
        {
            string sourcePath = Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(sourcePath));
            string source = File.ReadAllText(sourcePath);
            combinedNeutralSource += source;
            foreach (string marker in VmxCompletionTrapNestedRetireQuarantineContract.ForbiddenNeutralAuthorityMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }

        foreach (string marker in VmxCompletionTrapNestedRetireQuarantineContract.RequiredNeutralAuthorityMarkers)
        {
            Assert.Contains(marker, combinedNeutralSource);
        }

        foreach (string relativePath in VmxCompletionTrapNestedRetireQuarantineContract.CompatibilityQuarantinePaths)
        {
            Assert.True(File.Exists(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        string vmcsBlocksSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2Blocks.cs".Replace('/', Path.DirectorySeparatorChar)));
        string vmcsDescriptorSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs".Replace('/', Path.DirectorySeparatorChar)));
        string combinedVmcsProjection = vmcsBlocksSource + vmcsDescriptorSource;
        foreach (string marker in VmxCompletionTrapNestedRetireQuarantineContract.ForbiddenVmcsProjectionHelperMarkers)
        {
            Assert.DoesNotContain(marker, combinedVmcsProjection);
        }

        foreach (string marker in VmxCompletionTrapNestedRetireQuarantineContract.RequiredVmcsProjectionReadOnlyMarkers)
        {
            Assert.Contains(marker, combinedVmcsProjection);
        }

        AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(projectRoot);
    }

    [Fact]
    public void FinalFrozenAliasQuarantine_DeniesVmxNamedMutationAndKeepsNeutralAuthority()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        var contract = new FinalFrozenAliasQuarantineContract();

        Assert.True(contract.IsSatisfied());
        foreach (string relativePath in FinalFrozenAliasQuarantineContract.RemovedSubstrateAndHostAliasPaths)
        {
            Assert.False(File.Exists(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        foreach (string relativePath in FinalFrozenAliasQuarantineContract.NeutralAuthorityPaths)
        {
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            foreach (string marker in FinalFrozenAliasQuarantineContract.ForbiddenNeutralAuthorityMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }

        string translationProjection = File.ReadAllText(Path.Combine(
            projectRoot,
            FinalFrozenAliasQuarantineContract.CompatibilityProjectionPaths[0].Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in FinalFrozenAliasQuarantineContract.ForbiddenTranslationProjectionAuthorityMarkers)
        {
            Assert.DoesNotContain(marker, translationProjection);
        }
        Assert.Contains("IsReadOnlyCompatibilityProjection", translationProjection);

        string iommuAliasProjection = File.ReadAllText(Path.Combine(
            projectRoot,
            FinalFrozenAliasQuarantineContract.CompatibilityProjectionPaths[1].Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in FinalFrozenAliasQuarantineContract.ForbiddenIotlbAliasExecutionMarkers)
        {
            Assert.DoesNotContain(marker, iommuAliasProjection);
        }
        Assert.Contains("VmxCompatibilityIoAliasesAreReadOnlyDenied", iommuAliasProjection);

        foreach (string relativePath in FinalFrozenAliasQuarantineContract.DeniedCompatibilityAdapterPaths)
        {
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains("IsReadOnlyDeniedCompatibilityBackend", source);
            Assert.DoesNotContain("_hostBackend.", source);
        }

        string lane7Runtime = File.ReadAllText(Path.Combine(
            projectRoot,
            "Core/Runtime/Lanes/Lane7/Lane7StateBlock.cs".Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in FinalFrozenAliasQuarantineContract.ForbiddenLane7RuntimeCompatibilityMarkers)
        {
            Assert.DoesNotContain(marker, lane7Runtime);
        }

        foreach (VmcsFieldProjectionSchemaEntry entry in VmcsFieldProjectionSchema.Entries)
        {
            Assert.False(VmcsFieldProjectionSchema.CanWrite(entry));
        }

        var policy = new EvidencePolicyDescriptor(
            allowCompatibilityAliases: true,
            allowGuestArchitecturalState: true,
            allowMigrationSerializableState: true);
        VmcsFieldAliasResult writeResult = new VmcsFieldAliasProjection().ValidateAccess(
            new VmcsFieldAliasRequest(
                VmcsField.GuestPc,
                VmcsFieldAliasAccess.Write,
                EvidenceVisibilityClass.GuestArchitecturalState,
                GeneratedAliasDeclared: true,
                DescriptorValidated: true,
                AllowWrite: true),
            policy);
        Assert.Equal(VmcsFieldAliasDecision.WriteDenied, writeResult.Decision);

        AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(projectRoot);
    }

    [Fact]
    public void EventTrapDomainIdentity_RemovesVmidVpidFromExecutableRouting()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        var contract = new EventTrapDomainIdentityAuthorityRemovalContract();

        foreach (string relativePath in EventTrapDomainIdentityAuthorityRemovalContract.ExecutableEventTrapPaths)
        {
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            foreach (string marker in EventTrapDomainIdentityAuthorityRemovalContract.ForbiddenExecutableIdentityMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }

        EventInjectionDescriptor sourceEvent = EventInjectionDescriptor.Create(
            EventInjectionKind.VirtualTimer,
            vector: 32,
            targetVtId: 1,
            executionDomainTag: 3,
            addressSpaceTag: 4);
        TrapRequest timerTrap = TrapRequest.ForPreemptionTimer(
            vtId: 1,
            executionDomainTag: 3,
            addressSpaceTag: 4,
            deadline: 11);
        Assert.True(contract.IsNeutralIdentity(sourceEvent, timerTrap, 3, 4));

        InterruptRemapEntry route = InterruptRemapEntry.Route(
            EventInjectionKind.VirtualTimer,
            sourceVector: 32,
            targetVector: 33,
            targetVtId: 2,
            targetExecutionDomainTag: 5,
            targetAddressSpaceTag: 6,
            sourceExecutionDomainTag: 3);
        EventInjectionDescriptor routed = route.Apply(sourceEvent);
        Assert.Equal((ushort)5, routed.ExecutionDomainTag);
        Assert.Equal((ushort)6, routed.AddressSpaceTag);

        var timer = new VirtualTimerState();
        timer.Arm(
            nowCycle: 1,
            deltaCycles: 10,
            targetVtId: 1,
            executionDomainTag: 3,
            addressSpaceTag: 4,
            vector: 32);
        Assert.True(timer.TryConsumeExpired(11, out EventInjectionDescriptor timerEvent));
        Assert.Equal((ushort)3, timerEvent.ExecutionDomainTag);
        Assert.Equal((ushort)4, timerEvent.AddressSpaceTag);
        Assert.DoesNotContain(
            EventTrapDomainIdentityAuthorityRemovalContract.CompatibilityEventProjectionPath,
            EventTrapDomainIdentityAuthorityRemovalContract.ExecutableEventTrapPaths);

        string compatibilitySource = File.ReadAllText(Path.Combine(
            projectRoot,
            EventTrapDomainIdentityAuthorityRemovalContract.CompatibilityEventProjectionPath.Replace('/', Path.DirectorySeparatorChar)));
        int eventBlockStart = compatibilitySource.IndexOf("public sealed class EventInjectionBlock", StringComparison.Ordinal);
        int eventBlockEnd = compatibilitySource.IndexOf("public sealed class LaneCompletionRoutingBlock", StringComparison.Ordinal);
        Assert.True(eventBlockStart >= 0 && eventBlockEnd > eventBlockStart);
        string compatibilityEventBlock = compatibilitySource[eventBlockStart..eventBlockEnd];
        foreach (string marker in EventTrapDomainIdentityAuthorityRemovalContract.ForbiddenCompatibilityEventHelperMarkers)
        {
            Assert.DoesNotContain(marker, compatibilityEventBlock);
        }
        Assert.Contains("IsReadOnlyCompatibilityProjection", compatibilityEventBlock);
    }

    [Fact]
    public void NestedDomainProjectionCheckpoint_UsesNeutralOwnerAndRejectsHostEvidenceRestore()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        string ownerSource = File.ReadAllText(Path.Combine(
            projectRoot,
            NestedDomainProjectionCheckpointOwnerContract.NeutralOwnerPath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in NestedDomainProjectionCheckpointOwnerContract.ForbiddenNeutralOwnerMarkers)
        {
            Assert.DoesNotContain(marker, ownerSource);
        }
        foreach (string marker in NestedDomainProjectionCheckpointOwnerContract.RequiredNeutralOwnerMarkers)
        {
            Assert.Contains(marker, ownerSource);
        }

        string compatibilitySource = File.ReadAllText(Path.Combine(
            projectRoot,
            NestedDomainProjectionCheckpointOwnerContract.CompatibilityProjectionPath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in NestedDomainProjectionCheckpointOwnerContract.RequiredCompatibilityDenyMarkers)
        {
            Assert.Contains(marker, compatibilitySource);
        }

        var descriptor = new NestedDomainDescriptor(
            NestedDomainAuthority.Runtime,
            parentDomainId: 1,
            childDomainId: 2,
            capabilities: NestedCapabilityGrantMask.RequiredForPhase7,
            domainCompositionEnabled: true,
            allowsCompatibilityProjection: true,
            hostEvidenceExcluded: true,
            lanePassthroughBlocked: true);
        var projectionRequest = new NestedProjectionRequest(
            descriptor,
            NestedDomainRuntimeResult.Allowed,
            NestedCapabilityFilterResult.Allowed,
            NestedEvidencePolicyResult.Allowed,
            default,
            RequiresCompletionMapping: false,
            RequiresCompatibilityProjection: true);
        var policy = new MigrationValidationPolicy(
            new MigrationDescriptor(
                allowGuestArchitecturalState: true,
                allowDomainDescriptorState: true,
                allowCompatibilityProjectionMetadata: false),
            new EvidencePolicyDescriptor(
                allowCompatibilityAliases: false,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: true),
            rejectCompatibilityProjectionMetadata: true,
            requireGuestStatePreservePolicy: true);
        DomainCheckpointImage checkpoint = new DomainCheckpointImage(
            DomainCheckpointAuthority.DomainDescriptor,
            checkpointEpoch: 17,
            payloadMask: 0,
            evidenceMask: 0,
            containsCompatibilityProjectionMetadata: false)
            .WithPayload(MigrationPayloadClass.GuestArchitecturalState);
        var service = new NestedDomainProjectionCheckpointService();
        var request = new NestedDomainProjectionCheckpointRequest(
            projectionRequest,
            checkpoint,
            policy,
            EvidenceRestorePolicy.PreserveGuestArchitecturalState,
            ExpectedCheckpointEpoch: 17);

        Assert.True(service.Validate(request).IsAllowed);

        DomainCheckpointImage hostEvidenceCheckpoint =
            checkpoint.WithPayload(MigrationPayloadClass.NativeTokenEvidence);
        var hostEvidenceRequest = request with { Checkpoint = hostEvidenceCheckpoint };
        NestedDomainProjectionCheckpointResult denied = service.Validate(hostEvidenceRequest);
        Assert.False(denied.IsAllowed);
        Assert.Equal(NestedDomainProjectionCheckpointDecision.RestoreDenied, denied.Decision);
        Assert.True(new NestedDomainProjectionCheckpointOwnerContract()
            .RejectsHostOwnedCheckpoint(service, hostEvidenceRequest));
    }

    [Fact]
    public void VectorAcceleratorNestedIdentityPressure_UsesNeutralRuntimeBoundary()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        var contract = new VectorAcceleratorNestedIdentityPressureRemovalContract();

        foreach (string relativePath in VectorAcceleratorNestedIdentityPressureRemovalContract.RemovedVectorIdentityPaths)
        {
            Assert.False(File.Exists(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        foreach (string relativePath in VectorAcceleratorNestedIdentityPressureRemovalContract.NeutralVectorIdentityPaths)
        {
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            foreach (string marker in VectorAcceleratorNestedIdentityPressureRemovalContract.ForbiddenVectorIdentityMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }

        string blocksSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VectorAcceleratorNestedIdentityPressureRemovalContract.VmcsBlocksPath.Replace('/', Path.DirectorySeparatorChar)));
        int exitInfoStart = blocksSource.IndexOf("public sealed class ExitInfoBlock", StringComparison.Ordinal);
        int exitInfoEnd = blocksSource.IndexOf("public sealed class VirtualCpuBlock", StringComparison.Ordinal);
        Assert.True(exitInfoStart >= 0 && exitInfoEnd > exitInfoStart);
        string exitInfoBlock = blocksSource[exitInfoStart..exitInfoEnd];
        Assert.DoesNotContain("ushort vpid", exitInfoBlock);
        Assert.DoesNotContain("VmxVectorExceptionInfo", exitInfoBlock);
        Assert.DoesNotContain("VmxStreamDescriptorFaultInfo", exitInfoBlock);
        Assert.Contains("ushort addressSpaceTag", exitInfoBlock);

        var vectorInfo = new VectorStreamExceptionInfo(
            ExecutionDomainTag: 3,
            AddressSpaceTag: 4,
            OwnerVirtualThreadId: 1,
            ExceptionMask: 0x10,
            ExceptionPriority: 0x20,
            HighestExceptionIndex: 2,
            FaultingPc: 0x1000,
            FaultingLane: 7,
            FaultingOpcode: 0x44,
            VectorExceptionAction.ReflectAsCompatibilityExit,
            Sequence: 9);
        Assert.True(contract.IsNeutralVectorExceptionIdentity(vectorInfo, 3, 4));
        Assert.Equal(
            2UL |
            (((ulong)VectorExceptionAction.ReflectAsCompatibilityExit & 0x3UL) << 8) |
            (1UL << 16) |
            (4UL << 32),
            vectorInfo.EncodeCompatibilityQualification());

        var streamFault = new VectorStreamDescriptorFaultInfo(
            ExecutionDomainTag: 3,
            AddressSpaceTag: 4,
            OwnerVirtualThreadId: 1,
            StreamDescriptorFaultKind.DescriptorDecodeFault,
            GuestDescriptorAddress: 0x2000,
            DescriptorLength: 64,
            StreamReplayEpoch: 5,
            Sequence: 6,
            Message: "decode fault");
        Assert.True(contract.IsNeutralStreamFaultIdentity(streamFault, 3, 4));

        var descriptor = YAKSys_Hybrid_CPU.Core.Vmcs.V2.VmcsV2Descriptor.CreateDefault();
        descriptor.RecordVectorExceptionExit(vectorInfo);
        Assert.Equal(VmExitReason.VectorException, descriptor.ExitInfo.ExitReason);
        Assert.Equal(vectorInfo.EncodeCompatibilityQualification(), descriptor.ExitInfo.ExitQualification);
        descriptor.RecordStreamReplayRequiredExit(
            ownerVirtualThreadId: 1,
            addressSpaceTag: 4,
            streamReplayEpoch: 77);
        Assert.Equal(VmExitReason.StreamReplayRequired, descriptor.ExitInfo.ExitReason);
        Assert.Equal((1UL << 16) | (4UL << 32), descriptor.ExitInfo.ExitQualification);

        foreach (string relativePath in VectorAcceleratorNestedIdentityPressureRemovalContract.RemovedAcceleratorWrapperPaths)
        {
            Assert.False(File.Exists(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        foreach (string relativePath in VectorAcceleratorNestedIdentityPressureRemovalContract.NeutralAcceleratorNamespacePaths)
        {
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            foreach (string marker in VectorAcceleratorNestedIdentityPressureRemovalContract.ForbiddenAcceleratorNamespaceMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }

        var lane7 = new Lane7StateBlock();
        lane7.ConfigureOwnership(executionDomainTag: 11, addressSpaceTag: 12);
        Assert.True(lane7.HandleNamespace.TryAllocate(
            ownerVirtualThreadId: 2,
            AcceleratorDeviceId.ReferenceMatMul,
            Lane7VirtualCapability.Submit | Lane7VirtualCapability.QueryCaps,
            out Lane7VirtualHandle handle,
            out Lane7Fault lane7Fault));
        Assert.False(lane7Fault.IsFaulted);
        Assert.True(lane7.HandleNamespace.TryResolve(handle.Value, out Lane7VirtualHandle resolved));
        Assert.Equal((ushort)11, resolved.ExecutionDomainTag);

        string lane7Source = File.ReadAllText(Path.Combine(
            projectRoot,
            VectorAcceleratorNestedIdentityPressureRemovalContract.Lane7StatePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("HandleNamespace", lane7Source);
        Assert.Contains("TokenNamespace", lane7Source);
        Assert.Contains("AdmissionPolicy", lane7Source);
        Assert.DoesNotContain("HandleMap = new VmxAcceleratorHandleMap", lane7Source);
        Assert.DoesNotContain("TokenVirtualizer = new VmxLane7TokenVirtualizer", lane7Source);
        Assert.DoesNotContain("Policy = new VmxAcceleratorPolicy", lane7Source);

        string childIntentSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VectorAcceleratorNestedIdentityPressureRemovalContract.ChildDomainIntentPath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in VectorAcceleratorNestedIdentityPressureRemovalContract.ForbiddenNestedIntentMarkers)
        {
            Assert.DoesNotContain(marker, childIntentSource);
        }
        foreach (string marker in VectorAcceleratorNestedIdentityPressureRemovalContract.RequiredNeutralNestedIntentMarkers)
        {
            Assert.Contains(marker, childIntentSource);
        }

        var intentPolicy = ChildDomainIntentAccessPolicy.DefaultNestedL1Visible();
        Assert.Equal(
            ChildDomainIntentAccessDisposition.Allowed,
            intentPolicy.EvaluateRead(ChildDomainIntentFieldIds.AddressSpaceTag));
        Assert.Null(typeof(ChildDomainIntentDescriptor).GetMethod("TryVmRead"));
        Assert.Null(typeof(ChildDomainIntentDescriptor).GetMethod("TryVmWrite"));
        Assert.NotNull(typeof(ChildDomainIntentSnapshot).GetProperty("ChildIntentPointer"));
        Assert.Null(typeof(ChildDomainIntentSnapshot).GetProperty("Vmcs12Pointer"));

        string neutralNestedOwner = File.ReadAllText(Path.Combine(
            projectRoot,
            VectorAcceleratorNestedIdentityPressureRemovalContract.NestedProjectionCheckpointOwnerPath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in NestedDomainProjectionCheckpointOwnerContract.ForbiddenNeutralOwnerMarkers)
        {
            Assert.DoesNotContain(marker, neutralNestedOwner);
        }

        var nestedDescriptor = new NestedDomainDescriptor(
            NestedDomainAuthority.Runtime,
            parentDomainId: 1,
            childDomainId: 2,
            capabilities: NestedCapabilityGrantMask.RequiredForPhase7,
            domainCompositionEnabled: true,
            allowsCompatibilityProjection: true,
            hostEvidenceExcluded: true,
            lanePassthroughBlocked: true);
        var projectionRequest = new NestedProjectionRequest(
            nestedDescriptor,
            NestedDomainRuntimeResult.Allowed,
            NestedCapabilityFilterResult.Allowed,
            NestedEvidencePolicyResult.Allowed,
            default,
            RequiresCompletionMapping: false,
            RequiresCompatibilityProjection: true);
        var migrationPolicy = new MigrationValidationPolicy(
            new MigrationDescriptor(
                allowGuestArchitecturalState: true,
                allowDomainDescriptorState: true,
                allowCompatibilityProjectionMetadata: false),
            new EvidencePolicyDescriptor(
                allowCompatibilityAliases: false,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: true),
            rejectCompatibilityProjectionMetadata: true,
            requireGuestStatePreservePolicy: true);
        DomainCheckpointImage checkpoint = new DomainCheckpointImage(
            DomainCheckpointAuthority.DomainDescriptor,
            checkpointEpoch: 21,
            payloadMask: 0,
            evidenceMask: 0,
            containsCompatibilityProjectionMetadata: false)
            .WithPayload(MigrationPayloadClass.GuestArchitecturalState);
        var nestedService = new NestedDomainProjectionCheckpointService();
        var nestedRequest = new NestedDomainProjectionCheckpointRequest(
            projectionRequest,
            checkpoint.WithPayload(MigrationPayloadClass.NativeTokenEvidence),
            migrationPolicy,
            EvidenceRestorePolicy.PreserveGuestArchitecturalState,
            ExpectedCheckpointEpoch: 21);
        Assert.True(contract.RejectsHostOwnedNestedRestore(nestedService, nestedRequest));
    }

    [Fact]
    public void LegacyVmxIoVirtualizationBackendRemoval_DeadCompiledShellIsAbsentWithoutReplacement()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        string removedPath = Path.Combine(
            projectRoot,
            LegacyVmxIoVirtualizationBackendRemovalContract.RemovedCompatibilityBackendPath.Replace('/', Path.DirectorySeparatorChar));
        string compatibilityRoot = Path.Combine(projectRoot, "Legacy", "VMX", "Compatibility");
        var contract = new LegacyVmxIoVirtualizationBackendRemovalContract();
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(contract.IsSatisfied());
        Assert.False(File.Exists(removedPath));
        Assert.True(manifest.TryGetEntry(
            LegacyVmxIoVirtualizationBackendRemovalContract.RemovedCompatibilityBackendPath,
            out VmxQuarantineEvidenceEntry entry));
        Assert.True(entry.IsRemovedWithoutReplacement);
        Assert.False(manifest.RequiresQuarantine(
            LegacyVmxIoVirtualizationBackendRemovalContract.RemovedCompatibilityBackendPath));

        for (int index = 0; index < LegacyVmxIoVirtualizationBackendRemovalContract.NeutralIoAuthorityPaths.Length; index++)
        {
            string neutralSource = File.ReadAllText(Path.Combine(
                projectRoot,
                LegacyVmxIoVirtualizationBackendRemovalContract.NeutralIoAuthorityPaths[index]
                    .Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains(
                LegacyVmxIoVirtualizationBackendRemovalContract.RequiredNeutralIoAuthorityMarkers[index],
                neutralSource);
        }

        foreach (string compatibilityPath in GetCSharpSourcesIfDirectoryExists(compatibilityRoot))
        {
            string compatibilitySource = File.ReadAllText(compatibilityPath);
            foreach (string marker in LegacyVmxIoVirtualizationBackendRemovalContract.ForbiddenCompatibilityAuthorityMutationMarkers)
            {
                Assert.DoesNotContain(marker, compatibilitySource);
            }
        }
    }

    [Fact]
    public void ReturnedLegacyIommuDomainBinding_UsesGenericHostMechanicsOnly()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string legacyPath = Path.Combine(
            projectRoot,
            LegacyIommuDomainBindingReturnContract.LegacyOriginPath.Replace('/', Path.DirectorySeparatorChar));
        string genericHostPath = Path.Combine(
            projectRoot,
            LegacyIommuDomainBindingReturnContract.GenericHostPath.Replace('/', Path.DirectorySeparatorChar));
        string aliasPath = Path.Combine(
            projectRoot,
            LegacyIommuDomainBindingReturnContract.VmxCompatibilityAliasPath.Replace('/', Path.DirectorySeparatorChar));
        string ioHostBackendPath = Path.Combine(
            projectRoot,
            "Memory/MMU/IoVirtualizationHostBackend.cs".Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.False(File.Exists(legacyPath));
        Assert.True(File.Exists(genericHostPath));
        Assert.True(File.Exists(aliasPath));
        Assert.True(manifest.CanReturnToCore(
            LegacyIommuDomainBindingReturnContract.LegacyOriginPath,
            VmxQuarantineReturnProof.CompleteProjectionOnly));
        Assert.True(LegacyIommuDomainBindingReturnContract.RejectsVmxShapedHostMechanics);

        string genericSource = File.ReadAllText(genericHostPath);
        Assert.DoesNotContain("VMX", genericSource);
        Assert.DoesNotContain("Vmx", genericSource);
        Assert.DoesNotContain("BindVmx", genericSource);
        Assert.DoesNotContain("UnbindVmx", genericSource);
        Assert.DoesNotContain("InvalidateVmx", genericSource);
        Assert.DoesNotContain("ApplyVmx", genericSource);
        Assert.DoesNotContain("TryTranslateVmxDma", genericSource);
        Assert.DoesNotContain("InitializeVmxDmaState", genericSource);
        Assert.DoesNotContain("_vmxDomainBindings", genericSource);
        Assert.Contains("BindIoDomain", genericSource);
        Assert.Contains("TryTranslateDma", genericSource);
        Assert.Contains("ApplyIoDomainInvalidation", genericSource);

        string ioHostBackendSource = File.ReadAllText(ioHostBackendPath);
        Assert.DoesNotContain("BindVmx", ioHostBackendSource);
        Assert.DoesNotContain("UnbindVmx", ioHostBackendSource);
        Assert.DoesNotContain("InvalidateVmx", ioHostBackendSource);
        Assert.DoesNotContain("TryTranslateVmxDma", ioHostBackendSource);

        string aliasSource = File.ReadAllText(aliasPath);
        Assert.Contains("VmxCompatibilityIoAliasesAreReadOnlyDenied", aliasSource);
        Assert.DoesNotContain("BindIoDomain", aliasSource);
        Assert.DoesNotContain("ApplyTranslationInvalidation", aliasSource);
    }

    [Fact]
    public void RemovedLegacyVmcsMemoryTranslationProjection_IsNotReintroduced()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string legacyPath = Path.Combine(
            projectRoot,
            LegacyVmcsMemoryTranslationProjectionRemovalContract.LegacyOriginPath.Replace('/', Path.DirectorySeparatorChar));
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmcsMemoryTranslationProjectionRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        string genericControlPath = Path.Combine(
            projectRoot,
            LegacyVmcsMemoryTranslationProjectionRemovalContract.GenericControlPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmcsMemoryTranslationProjectionRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmcsMemoryTranslationProjectionRemovalContract.RejectsReturnedVmcsTranslationAuthority);
        Assert.True(manifest.TryGetEntry(
            LegacyVmcsMemoryTranslationProjectionRemovalContract.LegacyOriginPath,
            out VmxQuarantineEvidenceEntry entry));
        Assert.True(entry.IsRemovedWithoutReplacement);
        Assert.False(File.Exists(legacyPath));
        Assert.False(File.Exists(frontendPath));
        Assert.True(File.Exists(genericControlPath));

        string genericControlSource = File.ReadAllText(genericControlPath);
        Assert.DoesNotContain("CreateRuntimeProjection", genericControlSource);
        Assert.DoesNotContain("CreateSecondStageControl", genericControlSource);
        Assert.DoesNotContain("FromDomainControl", genericControlSource);
        Assert.DoesNotContain("IVmcsManager", genericControlSource);
        Assert.DoesNotContain("VmcsField", genericControlSource);
        Assert.DoesNotContain("ReadFieldValue", genericControlSource);
    }

    [Fact]
    public void RemovedLegacyShadowVmcsBlock_IsNotReintroduced()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string legacyPath = Path.Combine(
            projectRoot,
            LegacyShadowVmcsBlockRemovalContract.LegacyOriginPath.Replace('/', Path.DirectorySeparatorChar));
        string nestedProjectionServicePath = Path.Combine(
            projectRoot,
            LegacyShadowVmcsBlockRemovalContract.NestedProjectionServicePath.Replace('/', Path.DirectorySeparatorChar));
        string descriptorPath = Path.Combine(
            projectRoot,
            LegacyShadowVmcsBlockRemovalContract.VmcsDescriptorPath.Replace('/', Path.DirectorySeparatorChar));
        string managerPath = Path.Combine(
            projectRoot,
            LegacyShadowVmcsBlockRemovalContract.VmcsManagerPath.Replace('/', Path.DirectorySeparatorChar));
        string checkpointPath = Path.Combine(
            projectRoot,
            LegacyShadowVmcsBlockRemovalContract.CheckpointPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyShadowVmcsBlockRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyShadowVmcsBlockRemovalContract.RejectsShadowVmcsAuthorityReturn);
        Assert.True(manifest.TryGetEntry(
            LegacyShadowVmcsBlockRemovalContract.LegacyOriginPath,
            out VmxQuarantineEvidenceEntry entry));
        Assert.True(entry.IsRemovedWithoutReplacement);
        Assert.False(File.Exists(legacyPath));
        Assert.True(File.Exists(nestedProjectionServicePath));
        Assert.True(File.Exists(descriptorPath));
        Assert.False(File.Exists(managerPath));
        Assert.False(File.Exists(checkpointPath));

        string nestedProjectionServiceSource = File.ReadAllText(nestedProjectionServicePath);
        Assert.DoesNotContain(".ShadowVmcs.", nestedProjectionServiceSource);
        Assert.DoesNotContain("IShadowVmcsCompatibilityBridge", nestedProjectionServiceSource);
        Assert.DoesNotContain("ShadowVmcsCompatibilityBridge", nestedProjectionServiceSource);
        Assert.Contains("removed without replacement", nestedProjectionServiceSource);

        string descriptorSource = File.ReadAllText(descriptorPath);
        Assert.DoesNotContain("ShadowVmcsBlock", descriptorSource);
        Assert.DoesNotContain("new ShadowVmcs", descriptorSource);
        Assert.DoesNotContain(".ShadowVmcs.", descriptorSource);
    }

    [Fact]
    public void ReturnedLegacyVmxV1ExecutionAdapterSurfaces_AreCurrentCoreRoutingOnly()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string legacyDispatcherPath = Path.Combine(
            projectRoot,
            LegacyVmxV1ExecutionAdapterSurfaceReturnContract.LegacyDispatcherOriginPath.Replace('/', Path.DirectorySeparatorChar));
        string legacyPipelinePath = Path.Combine(
            projectRoot,
            LegacyVmxV1ExecutionAdapterSurfaceReturnContract.LegacyPipelineOriginPath.Replace('/', Path.DirectorySeparatorChar));
        string coreDispatcherPath = Path.Combine(
            projectRoot,
            LegacyVmxV1ExecutionAdapterSurfaceReturnContract.CoreDispatcherPath.Replace('/', Path.DirectorySeparatorChar));
        string corePipelinePath = Path.Combine(
            projectRoot,
            LegacyVmxV1ExecutionAdapterSurfaceReturnContract.CorePipelinePath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(manifest.CanReturnToCore(
            LegacyVmxV1ExecutionAdapterSurfaceReturnContract.LegacyDispatcherOriginPath,
            VmxQuarantineReturnProof.CompleteProjectionOnly));
        Assert.True(manifest.CanReturnToCore(
            LegacyVmxV1ExecutionAdapterSurfaceReturnContract.LegacyPipelineOriginPath,
            VmxQuarantineReturnProof.CompleteProjectionOnly));
        Assert.True(LegacyVmxV1ExecutionAdapterSurfaceReturnContract.RejectsVmcsManagerAuthority);
        Assert.True(LegacyVmxV1ExecutionAdapterSurfaceReturnContract.RejectsRawVmcsFieldAuthority);
        Assert.True(LegacyVmxV1ExecutionAdapterSurfaceReturnContract.RejectsIommuAuthority);

        Assert.False(File.Exists(legacyDispatcherPath));
        Assert.False(File.Exists(legacyPipelinePath));
        Assert.True(File.Exists(coreDispatcherPath));
        Assert.True(File.Exists(corePipelinePath));

        string dispatcherSource = File.ReadAllText(coreDispatcherPath);
        Assert.Contains("CaptureRetireWindowVmxEffect", dispatcherSource);
        Assert.Contains("CreateRemovedFrontendFaultEffect", dispatcherSource);
        Assert.DoesNotContain("_vmxUnit", dispatcherSource);
        Assert.DoesNotContain("VmxExecutionUnit", dispatcherSource);
        Assert.Contains("EnqueuePipelineEvent", dispatcherSource);
        Assert.DoesNotContain("VmcsManager", dispatcherSource);
        Assert.DoesNotContain("IVmcsManager", dispatcherSource);
        Assert.DoesNotContain("ReadFieldValue", dispatcherSource);
        Assert.DoesNotContain("WriteFieldValue", dispatcherSource);
        Assert.DoesNotContain("IOMMU.", dispatcherSource);
        Assert.DoesNotContain(".ShadowVmcs.", dispatcherSource);

        string pipelineSource = File.ReadAllText(corePipelinePath);
        Assert.Contains("MaterializeLaneVmxEffect", pipelineSource);
        Assert.Contains("ApplyRetiredVmxEffect", pipelineSource);
        Assert.Contains("RetireCoordinator", pipelineSource);
        Assert.Contains("ApplyRetiredVmxPipelineStateOwnership", pipelineSource);
        Assert.DoesNotContain("VmcsManager", pipelineSource);
        Assert.DoesNotContain("IVmcsManager", pipelineSource);
        Assert.DoesNotContain("ReadFieldValue", pipelineSource);
        Assert.DoesNotContain("WriteFieldValue", pipelineSource);
        Assert.DoesNotContain("IOMMU.", pipelineSource);
        Assert.DoesNotContain(".ShadowVmcs.", pipelineSource);
    }

#if false // Historical executable tests for the removed VmxExecutionUnit shell; retained only as retired specification text.
    [Fact]
    public void LegacyVmxExecutionUnit_DoesNotOwnInvalidationAuthority()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitInvalidationRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitInvalidationRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitInvalidationRemovalContract.RejectsFrontendOwnedInvalidationEpochs);
        Assert.True(LegacyVmxExecutionUnitInvalidationRemovalContract.RejectsDirectHostInvalidationAuthority);
        Assert.True(LegacyVmxExecutionUnitInvalidationRemovalContract.RequiresFailClosedCompatibilityInvalidation);
        Assert.True(manifest.RequiresQuarantine(
            LegacyVmxExecutionUnitInvalidationRemovalContract.QuarantinedFrontendPath));
        Assert.True(File.Exists(frontendPath));

        string frontendSource = File.ReadAllText(frontendPath);
        Assert.DoesNotContain("IOMMU.", frontendSource);
        Assert.DoesNotContain("ApplyVmxInvalidation", frontendSource);
        Assert.DoesNotContain("EptInvalidationEpoch", frontendSource);
        Assert.DoesNotContain("VpidInvalidationEpoch", frontendSource);
        Assert.DoesNotContain("AdvanceRuntimeEpoch(", frontendSource);
        Assert.DoesNotContain("ResolveInvalidation(", frontendSource);
        Assert.DoesNotContain("VmxRetireEffect.Invalidation", frontendSource);
        Assert.DoesNotContain("DecodeInvalidationScope", frontendSource);
        Assert.Contains("ResolveUnsupportedInvalidation", frontendSource);
        Assert.Contains("ApplyUnsupportedInvalidation", frontendSource);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", frontendSource);
    }

    [Theory]
    [InlineData(InstructionsEnum.INVEPT, VmxOperationKind.Invept)]
    [InlineData(InstructionsEnum.INVVPID, VmxOperationKind.Invvpid)]
    public void LegacyVmxExecutionUnit_InvalidationOpcodesFailClosed(
        InstructionsEnum opcode,
        VmxOperationKind operation)
    {
        var csr = new CsrFile();
        csr.HardwareWrite(CsrAddresses.VmxEnable, 1);
        var vmx = new VmxExecutionUnit(csr, new VmcsManager());
        var state = new Vmx09FakeCpuState();

        VmxRetireEffect effect = vmx.Resolve(
            VmxIrHelper.MakeVmx(opcode, rs1: 1, rs2: 2),
            state,
            PrivilegeLevel.Machine,
            virtualThreadId: 0);

        Assert.Equal(operation, effect.Operation);
        Assert.True(effect.IsFaulted);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, effect.FailureReason);

        VmxRetireOutcome outcome = vmx.RetireEffect(effect, state, virtualThreadId: 0);

        Assert.True(outcome.Faulted);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, outcome.FailureReason);
        Assert.Equal(0UL, csr.DirectRead(CsrAddresses.VmxExitReason));
    }

    [Fact]
    public void LegacyVmxExecutionUnit_DoesNotCompleteNestedTranslationFaults()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitNestedTranslationFaultRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitNestedTranslationFaultRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitNestedTranslationFaultRemovalContract.RejectsFrontendOwnedNestedTranslationExitCompletion);
        Assert.True(LegacyVmxExecutionUnitNestedTranslationFaultRemovalContract.RejectsDirectNestedTranslationVmcsPublication);
        Assert.True(LegacyVmxExecutionUnitNestedTranslationFaultRemovalContract.RequiresGenericNestedDomainFaultRouting);
        Assert.True(manifest.RequiresQuarantine(
            LegacyVmxExecutionUnitNestedTranslationFaultRemovalContract.QuarantinedFrontendPath));
        Assert.True(File.Exists(frontendPath));

        string frontendSource = File.ReadAllText(frontendPath);
        Assert.DoesNotContain("CompleteNestedTranslationFault", frontendSource);
        Assert.DoesNotContain("RecordNestedTranslationExit", frontendSource);
        Assert.DoesNotContain("NestedTranslationResult", frontendSource);
        Assert.DoesNotContain("NestedTranslationStatus", frontendSource);
        Assert.DoesNotContain("translation.CausesVmExit", frontendSource);
        Assert.DoesNotContain("EptMisconfiguration", frontendSource);
        Assert.DoesNotContain("EptViolation", frontendSource);
    }

    [Fact]
    public void LegacyVmxExecutionUnit_DoesNotDeriveAdmissionDomainsFromVmcsFields()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitAdmissionVmcsFieldAuthorityRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitAdmissionVmcsFieldAuthorityRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitAdmissionVmcsFieldAuthorityRemovalContract.RejectsFrontendDerivedAdmissionDomains);
        Assert.True(LegacyVmxExecutionUnitAdmissionVmcsFieldAuthorityRemovalContract.RejectsVmcsFieldBackedInterceptEventAdmission);
        Assert.True(LegacyVmxExecutionUnitAdmissionVmcsFieldAuthorityRemovalContract.RequiresGenericDomainAdmissionForTaggedRouting);
        Assert.True(manifest.RequiresQuarantine(
            LegacyVmxExecutionUnitAdmissionVmcsFieldAuthorityRemovalContract.QuarantinedFrontendPath));
        Assert.True(File.Exists(frontendPath));

        string frontendSource = File.ReadAllText(frontendPath);
        Assert.DoesNotContain("ResolveActiveMemoryTranslationControl", frontendSource);
        Assert.DoesNotContain("CreateRuntimeProjection", frontendSource);
        Assert.DoesNotContain("VmcsField.SecondaryProcControls", frontendSource);
        Assert.DoesNotContain("VmcsField.GuestCr3", frontendSource);
        Assert.DoesNotContain("VmcsField.EptPointer", frontendSource);
        Assert.DoesNotContain("VmcsField.Vpid", frontendSource);
        Assert.DoesNotContain("MemoryTranslationControl.Disabled", frontendSource);
    }

    [Fact]
    public void LegacyVmxExecutionUnit_DoesNotPublishStandaloneInterceptExits()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitInterceptPublicationRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitInterceptPublicationRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitInterceptPublicationRemovalContract.RejectsFrontendOwnedInterceptExitPublication);
        Assert.True(LegacyVmxExecutionUnitInterceptPublicationRemovalContract.RejectsStandaloneInterceptVmcsTracePublication);
        Assert.True(LegacyVmxExecutionUnitInterceptPublicationRemovalContract.RequiresGenericDomainTrapPublication);
        Assert.True(manifest.RequiresQuarantine(
            LegacyVmxExecutionUnitInterceptPublicationRemovalContract.QuarantinedFrontendPath));
        Assert.True(File.Exists(frontendPath));

        string frontendSource = File.ReadAllText(frontendPath);
        Assert.DoesNotContain("RecordInterceptExit", frontendSource);
        Assert.DoesNotContain("VmxEventKind.InterceptExit", frontendSource);
        Assert.Contains("ApplyInterceptExit", frontendSource);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", frontendSource);
    }

    [Fact]
    public void LegacyVmxExecutionUnit_DoesNotOwnLane7OrVectorStreamRuntimePaths()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitLaneVectorAuthorityRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitLaneVectorAuthorityRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitLaneVectorAuthorityRemovalContract.RejectsFrontendOwnedLane7VmFuncResolution);
        Assert.True(LegacyVmxExecutionUnitLaneVectorAuthorityRemovalContract.RejectsFrontendOwnedVectorStreamSaveRestore);
        Assert.True(LegacyVmxExecutionUnitLaneVectorAuthorityRemovalContract.RequiresGenericLaneVectorRuntimeAdmission);
        Assert.True(manifest.RequiresQuarantine(
            LegacyVmxExecutionUnitLaneVectorAuthorityRemovalContract.QuarantinedFrontendPath));
        Assert.True(File.Exists(frontendPath));

        string frontendSource = File.ReadAllText(frontendPath);
        Assert.DoesNotContain("TryResolveLane7VmFunc", frontendSource);
        Assert.DoesNotContain("Lane7VmFuncResult", frontendSource);
        Assert.DoesNotContain("TrySaveVectorStreamState", frontendSource);
        Assert.DoesNotContain("TryRestoreVectorStreamState", frontendSource);
        Assert.DoesNotContain("TryValidateVectorStreamExtendedStateMask", frontendSource);
        Assert.DoesNotContain("TryDecodeVectorStreamSaveMask", frontendSource);
        Assert.DoesNotContain("DescriptorMatchesActiveVmcs", frontendSource);
        Assert.DoesNotContain("VectorStreamSaveMask", frontendSource);
        Assert.DoesNotContain("VmxFunctionLeaf.CapabilityQuery", frontendSource);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", frontendSource);
    }

    [Fact]
    public void LegacyVmxExecutionUnit_DoesNotOwnVirtualEventDelivery()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitEventDeliveryRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitEventDeliveryRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitEventDeliveryRemovalContract.RejectsFrontendOwnedVirtualEventDelivery);
        Assert.True(LegacyVmxExecutionUnitEventDeliveryRemovalContract.RejectsVmcsBackedEventDeliveryCalls);
        Assert.True(LegacyVmxExecutionUnitEventDeliveryRemovalContract.RequiresGenericEventDeliveryRuntimeAdmission);
        Assert.True(manifest.RequiresQuarantine(
            LegacyVmxExecutionUnitEventDeliveryRemovalContract.QuarantinedFrontendPath));
        Assert.True(File.Exists(frontendPath));

        string frontendSource = File.ReadAllText(frontendPath);
        Assert.DoesNotContain("TryDeliverVirtualEvent", frontendSource);
        Assert.DoesNotContain("VmxEventKind.EventDelivered", frontendSource);
        Assert.DoesNotContain("TryDeliverPendingVirtualEvent(", frontendSource);
        Assert.Contains("TryDeliverPendingVirtualEventAtSafeBoundary", frontendSource);
        Assert.Contains("return false;", frontendSource);
    }

    [Fact]
    public void LegacyVmxExecutionUnit_DoesNotOwnVmcsAccessRouting()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitVmcsAccessRoutingRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitVmcsAccessRoutingRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitVmcsAccessRoutingRemovalContract.RejectsFrontendOwnedVmcsReadWriteRouting);
        Assert.True(LegacyVmxExecutionUnitVmcsAccessRoutingRemovalContract.RejectsNestedVmcsAccessRouting);
        Assert.True(LegacyVmxExecutionUnitVmcsAccessRoutingRemovalContract.RequiresGeneratedProjectionAccessPolicy);
        Assert.True(manifest.RequiresQuarantine(
            LegacyVmxExecutionUnitVmcsAccessRoutingRemovalContract.QuarantinedFrontendPath));
        Assert.True(File.Exists(frontendPath));

        string frontendSource = File.ReadAllText(frontendPath);
        Assert.DoesNotContain("ReadFieldValue", frontendSource);
        Assert.DoesNotContain("WriteFieldValue", frontendSource);
        Assert.DoesNotContain("TryNestedVmRead", frontendSource);
        Assert.DoesNotContain("TryNestedVmWrite", frontendSource);
        Assert.DoesNotContain("ApplyNestedVmcsAccessFailure", frontendSource);
        Assert.DoesNotContain("VmxRetireEffect.VmcsRead", frontendSource);
        Assert.DoesNotContain("VmxRetireEffect.VmcsWrite", frontendSource);
        Assert.Contains("VmxOperationKind.VmRead", frontendSource);
        Assert.Contains("VmxOperationKind.VmWrite", frontendSource);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", frontendSource);
    }

    [Fact]
    public void LegacyVmxExecutionUnit_DoesNotOwnCommonVmExitCompletion()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitExitCompletionRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitExitCompletionRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitExitCompletionRemovalContract.RejectsCommonFrontendVmExitCompletionHelper);
        Assert.True(LegacyVmxExecutionUnitExitCompletionRemovalContract.RejectsQualifiedVmExitPublication);
        Assert.True(LegacyVmxExecutionUnitExitCompletionRemovalContract.RequiresGenericDomainTrapCompletionRouting);
        Assert.True(manifest.RequiresQuarantine(
            LegacyVmxExecutionUnitExitCompletionRemovalContract.QuarantinedFrontendPath));
        Assert.True(File.Exists(frontendPath));

        string frontendSource = File.ReadAllText(frontendPath);
        Assert.DoesNotContain("CompleteQualifiedVmExit", frontendSource);
        Assert.DoesNotContain("RecordQualifiedVmExit", frontendSource);
        Assert.DoesNotContain("VmxExitCnt", frontendSource);
        Assert.DoesNotContain("VmxEventKind.VmExit", frontendSource);
        Assert.Contains("ApplyInterceptExit", frontendSource);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", frontendSource);
    }

    [Fact]
    public void LegacyVmxExecutionUnit_DoesNotOwnVmcsPointerLifecycle()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitVmcsPointerLifecycleRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitVmcsPointerLifecycleRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitVmcsPointerLifecycleRemovalContract.RejectsFrontendOwnedVmcsPointerLoadClearStore);
        Assert.True(LegacyVmxExecutionUnitVmcsPointerLifecycleRemovalContract.RejectsVmcsPointerTracePublication);
        Assert.True(LegacyVmxExecutionUnitVmcsPointerLifecycleRemovalContract.RequiresGenericExecutionDomainBindingAdmission);
        Assert.True(manifest.RequiresQuarantine(
            LegacyVmxExecutionUnitVmcsPointerLifecycleRemovalContract.QuarantinedFrontendPath));
        Assert.True(File.Exists(frontendPath));

        string frontendSource = File.ReadAllText(frontendPath);
        Assert.DoesNotContain("VmcsPointerEffect", frontendSource);
        Assert.DoesNotContain("VmxRetireEffect.VmPtrSt", frontendSource);
        Assert.DoesNotContain("ClearPointer", frontendSource);
        Assert.DoesNotContain("LoadPointer", frontendSource);
        Assert.DoesNotContain("StorePointer", frontendSource);
        Assert.DoesNotContain("VmcsPointerResult", frontendSource);
        Assert.DoesNotContain("VmxEventKind.VmClear", frontendSource);
        Assert.DoesNotContain("VmxEventKind.VmPtrLd", frontendSource);
        Assert.Contains("VmxOperationKind.VmClear", frontendSource);
        Assert.Contains("VmxOperationKind.VmPtrLd", frontendSource);
        Assert.Contains("VmxOperationKind.VmPtrSt", frontendSource);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", frontendSource);
    }

    [Fact]
    public void LegacyVmxExecutionUnit_DoesNotOwnVmEntryAuthority()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitVmEntryAuthorityRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitVmEntryAuthorityRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitVmEntryAuthorityRemovalContract.RejectsFrontendOwnedVmEntryTransition);
        Assert.True(LegacyVmxExecutionUnitVmEntryAuthorityRemovalContract.RejectsGuestPcSpRestoreFromVmcs);
        Assert.True(LegacyVmxExecutionUnitVmEntryAuthorityRemovalContract.RejectsVmEntryTracePublication);
        Assert.True(LegacyVmxExecutionUnitVmEntryAuthorityRemovalContract.RequiresGenericDomainEnterAdmission);
        Assert.True(manifest.RequiresQuarantine(
            LegacyVmxExecutionUnitVmEntryAuthorityRemovalContract.QuarantinedFrontendPath));
        Assert.True(File.Exists(frontendPath));

        string frontendSource = File.ReadAllText(frontendPath);
        Assert.DoesNotContain("BeginVmEntry", frontendSource);
        Assert.DoesNotContain("VmEntryTransitionResult", frontendSource);
        Assert.DoesNotContain("VmxEventKind.VmEntry", frontendSource);
        Assert.DoesNotContain("VmxEventKind.VmResume", frontendSource);
        Assert.DoesNotContain("GuestPc", frontendSource);
        Assert.DoesNotContain("GuestSp", frontendSource);
        Assert.DoesNotContain("PipelineTransitionTrigger.VmLaunch", frontendSource);
        Assert.DoesNotContain("PipelineTransitionTrigger.VmResume", frontendSource);
        Assert.DoesNotContain("PipelineTransitionTrigger.EntryOk", frontendSource);
        Assert.DoesNotContain("PipelineTransitionTrigger.EntryFail", frontendSource);
        Assert.DoesNotContain("ResolveVmLaunch", frontendSource);
        Assert.DoesNotContain("ResolveVmResume", frontendSource);
        Assert.Contains("VmxOperationKind.VmLaunch", frontendSource);
        Assert.Contains("VmxOperationKind.VmResume", frontendSource);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", frontendSource);
    }

    [Fact]
    public void LegacyVmxExecutionUnit_DoesNotOwnVmxRootSwitchAuthority()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitVmxRootSwitchRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitVmxRootSwitchRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitVmxRootSwitchRemovalContract.RejectsFrontendOwnedVmxEnableToggle);
        Assert.True(LegacyVmxExecutionUnitVmxRootSwitchRemovalContract.RejectsRootDescriptorActivation);
        Assert.True(LegacyVmxExecutionUnitVmxRootSwitchRemovalContract.RejectsVmxRootSwitchTracePublication);
        Assert.True(LegacyVmxExecutionUnitVmxRootSwitchRemovalContract.RequiresGenericRuntimeDomainAdmission);
        Assert.True(manifest.RequiresQuarantine(
            LegacyVmxExecutionUnitVmxRootSwitchRemovalContract.QuarantinedFrontendPath));
        Assert.True(File.Exists(frontendPath));

        string frontendSource = File.ReadAllText(frontendPath);
        Assert.DoesNotContain("ResolveVmxOn", frontendSource);
        Assert.DoesNotContain("ResolveVmxOff", frontendSource);
        Assert.DoesNotContain("VmxOnRootDescriptor", frontendSource);
        Assert.DoesNotContain("ActivateRootDescriptor", frontendSource);
        Assert.DoesNotContain("_csr.Write(CsrAddresses.VmxEnable", frontendSource);
        Assert.DoesNotContain("VmxEventKind.VmxOn", frontendSource);
        Assert.DoesNotContain("VmxEventKind.VmxOff", frontendSource);
        Assert.DoesNotContain("PipelineTransitionTrigger.VmxOff", frontendSource);
        Assert.Contains("VmxOperationKind.VmxOn", frontendSource);
        Assert.Contains("VmxOperationKind.VmxOff", frontendSource);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", frontendSource);
    }

    [Fact]
    public void LegacyVmxExecutionUnit_DoesNotOwnVmFailOrTracePublication()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitFailTracePublicationRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitFailTracePublicationRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitFailTracePublicationRemovalContract.RejectsFrontendOwnedVmFailPublication);
        Assert.True(LegacyVmxExecutionUnitFailTracePublicationRemovalContract.RejectsFrontendOwnedTracePublication);
        Assert.True(LegacyVmxExecutionUnitFailTracePublicationRemovalContract.RejectsFrontendOwnedVmxCsrPublication);
        Assert.True(LegacyVmxExecutionUnitFailTracePublicationRemovalContract.RequiresGenericDomainFaultPublication);
        Assert.True(manifest.RequiresQuarantine(
            LegacyVmxExecutionUnitFailTracePublicationRemovalContract.QuarantinedFrontendPath));
        Assert.True(File.Exists(frontendPath));

        string frontendSource = File.ReadAllText(frontendPath);
        Assert.DoesNotContain("RecordVmxFailForObservability", frontendSource);
        Assert.DoesNotContain("RecordVmxEvent", frontendSource);
        Assert.DoesNotContain("HardwareWrite", frontendSource);
        Assert.DoesNotContain("CsrAddresses.VmxExitReason", frontendSource);
        Assert.DoesNotContain("CsrAddresses.VmxExitQual", frontendSource);
        Assert.DoesNotContain("VmxEventKind.", frontendSource);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", frontendSource);
        Assert.Contains("VmxRetireOutcome.Fault", frontendSource);
    }

    [Fact]
    public void LegacyVmxExecutionUnit_DoesNotOwnVmCallVmFuncCapabilityGates()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitVmCallVmFuncGateRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitVmCallVmFuncGateRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitVmCallVmFuncGateRemovalContract.RejectsFrontendOwnedVmCallGate);
        Assert.True(LegacyVmxExecutionUnitVmCallVmFuncGateRemovalContract.RejectsFrontendOwnedVmFuncCapabilityQueryGate);
        Assert.True(LegacyVmxExecutionUnitVmCallVmFuncGateRemovalContract.RejectsFrontendOwnedCapabilityProjectionReadback);
        Assert.True(LegacyVmxExecutionUnitVmCallVmFuncGateRemovalContract.RequiresGenericRuntimeCapabilityAdmission);
        Assert.True(manifest.RequiresQuarantine(
            LegacyVmxExecutionUnitVmCallVmFuncGateRemovalContract.QuarantinedFrontendPath));
        Assert.True(File.Exists(frontendPath));

        string frontendSource = File.ReadAllText(frontendPath);
        Assert.DoesNotContain("ResolveVmCall", frontendSource);
        Assert.DoesNotContain("ResolveVmFunc", frontendSource);
        Assert.DoesNotContain("ApplyVmCall", frontendSource);
        Assert.DoesNotContain("ApplyVmFunc", frontendSource);
        Assert.DoesNotContain("ReadProjectedVmxCaps", frontendSource);
        Assert.DoesNotContain("IsVmxV2CapabilityEnabled", frontendSource);
        Assert.DoesNotContain("VmxFunctionLeaf.CapabilityQuery", frontendSource);
        Assert.DoesNotContain("CsrAddresses.VmxControl", frontendSource);
        Assert.DoesNotContain("VmxV2ControlBits.VmFuncCapabilityQuery", frontendSource);
        Assert.DoesNotContain("VmxV2InstructionCaps.VmCall", frontendSource);
        Assert.DoesNotContain("VmxV2InstructionCaps.VmFunc", frontendSource);
        Assert.DoesNotContain("VmxRetireEffect.VmCall", frontendSource);
        Assert.DoesNotContain("VmxRetireEffect.VmFunc", frontendSource);
        Assert.Contains("VmxOperationKind.VmCall", frontendSource);
        Assert.Contains("VmxOperationKind.VmFunc", frontendSource);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", frontendSource);
    }

    [Theory]
    [InlineData(InstructionsEnum.VMCALL, VmxOperationKind.VmCall)]
    [InlineData(InstructionsEnum.VMFUNC, VmxOperationKind.VmFunc)]
    public void LegacyVmxExecutionUnit_VmCallVmFuncFailClosedWithoutCapabilityPublication(
        InstructionsEnum opcode,
        VmxOperationKind operation)
    {
        var csr = new CsrFile();
        csr.HardwareWrite(CsrAddresses.VmxEnable, 1);
        csr.HardwareWrite(CsrAddresses.VmxControl, VmxV2ControlBits.VmFuncCapabilityQuery);
        csr.HardwareWrite(CsrAddresses.VmxCaps, CapabilityDescriptorSetSchema.KnownVmxV2CompatibilityMask);
        var vmx = new VmxExecutionUnit(csr, new VmcsManager());
        var state = new Vmx09FakeCpuState();

        VmxRetireEffect effect = vmx.Resolve(
            VmxIrHelper.MakeVmx(opcode, rs1: 1, rs2: 2, rd: 5),
            state,
            PrivilegeLevel.Machine,
            virtualThreadId: 0);

        Assert.Equal(operation, effect.Operation);
        Assert.True(effect.IsFaulted);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, effect.FailureReason);

        VmxRetireOutcome outcome = vmx.RetireEffect(effect, state, virtualThreadId: 0);

        Assert.True(outcome.Faulted);
        Assert.False(outcome.HasRegisterWriteback);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, outcome.FailureReason);
        Assert.Equal(CapabilityDescriptorSetSchema.KnownVmxV2CompatibilityMask, csr.DirectRead(CsrAddresses.VmxCaps));
    }

    [Fact]
    public void LegacyVmxExecutionUnit_DoesNotBuildGuestInterceptRequests()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectRoot = Path.Combine(repositoryRoot, "HybridCPU_ISE");
        string frontendPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitGuestInterceptRemovalContract.QuarantinedFrontendPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitGuestInterceptRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitGuestInterceptRemovalContract.RejectsFrontendOwnedGuestInterceptRequestConstruction);
        Assert.True(LegacyVmxExecutionUnitGuestInterceptRemovalContract.RejectsVmcsBackedInterceptResolution);
        Assert.True(LegacyVmxExecutionUnitGuestInterceptRemovalContract.RejectsVmxEnableAndActiveVmcsGuardAuthority);
        Assert.True(LegacyVmxExecutionUnitGuestInterceptRemovalContract.RequiresGenericTrapAdmissionRouting);
        Assert.True(manifest.RequiresQuarantine(
            LegacyVmxExecutionUnitGuestInterceptRemovalContract.QuarantinedFrontendPath));
        Assert.True(File.Exists(frontendPath));

        string frontendSource = File.ReadAllText(frontendPath);
        Assert.DoesNotContain("TryResolveGuestVmxOperationIntercept", frontendSource);
        Assert.DoesNotContain("TryResolveGuestIntercept", frontendSource);
        Assert.DoesNotContain("TryResolveInterceptRequest", frontendSource);
        Assert.DoesNotContain("TryBuildCsrInterceptRequest", frontendSource);
        Assert.DoesNotContain("TryBuildMemoryInterceptRequest", frontendSource);
        Assert.DoesNotContain("TryBuildLaneInterceptRequest", frontendSource);
        Assert.DoesNotContain("TrapRequest.For", frontendSource);
        Assert.DoesNotContain("_vmcs.", frontendSource);
        Assert.DoesNotContain("_csr.", frontendSource);
        Assert.DoesNotContain("HasActiveVmcs", frontendSource);
        Assert.DoesNotContain("CsrAddresses.VmxEnable", frontendSource);
        Assert.Contains("TryExecuteGuestIntercept", frontendSource);
        Assert.Contains("return false;", frontendSource);
    }

    [Fact]
    public void LegacyVmxExecutionUnit_GuestInterceptHookFailsClosedWithoutVmcsResolution()
    {
        var vmx = new VmxExecutionUnit(new CsrFile(), new VmcsManager());
        var state = new Vmx09FakeCpuState();

        bool intercepted = vmx.TryExecuteGuestIntercept(
            VmxIrHelper.MakeVmx(InstructionsEnum.VMCALL, rs1: 1, rs2: 2),
            state,
            virtualThreadId: 0,
            out ExecutionResult result);

        Assert.False(intercepted);
        Assert.Equal(default, result);
    }
#endif

    [Fact]
    public void LegacyVmxExecutionUnitShell_IsRemovedWithoutCoreReplacement()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        string legacyPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitRemovalContract.LegacyOriginPath.Replace('/', Path.DirectorySeparatorChar));
        string dispatcherPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitRemovalContract.CurrentDispatcherPath.Replace('/', Path.DirectorySeparatorChar));
        string retirePath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitRemovalContract.CurrentRetirePath.Replace('/', Path.DirectorySeparatorChar));
        string microOpPath = Path.Combine(
            projectRoot,
            LegacyVmxExecutionUnitRemovalContract.CurrentMicroOpPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmxExecutionUnitRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmxExecutionUnitRemovalContract.RejectsLegacyOpcodeShell);
        Assert.True(LegacyVmxExecutionUnitRemovalContract.RejectsLegacyConstructorAbi);
        Assert.True(LegacyVmxExecutionUnitRemovalContract.RequiresTypedFailClosedCompatibilityEffects);
        Assert.True(manifest.TryGetEntry(LegacyVmxExecutionUnitRemovalContract.LegacyOriginPath, out var entry));
        Assert.True(entry.IsRemovedWithoutReplacement);
        Assert.False(manifest.RequiresQuarantine(LegacyVmxExecutionUnitRemovalContract.LegacyOriginPath));

        Assert.False(File.Exists(legacyPath));
        Assert.True(File.Exists(dispatcherPath));
        Assert.True(File.Exists(retirePath));
        Assert.True(File.Exists(microOpPath));

        string dispatcherSource = File.ReadAllText(dispatcherPath);
        Assert.Contains("CreateRemovedFrontendFaultEffect", dispatcherSource);
        Assert.Contains("VmxRetireEffect.Fault", dispatcherSource);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", dispatcherSource);
        Assert.DoesNotContain("VmxExecutionUnit", dispatcherSource);
        Assert.DoesNotContain("VmcsManager", dispatcherSource);
        Assert.DoesNotContain("CsrFile", dispatcherSource);
        Assert.DoesNotContain("IOMMU.", dispatcherSource);

        string retireSource = File.ReadAllText(retirePath);
        Assert.Contains("ApplyRemovedFrontendFailClosedEffect", retireSource);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", retireSource);
        Assert.DoesNotContain("VmxExecutionUnit", retireSource);
        Assert.DoesNotContain("VmcsManager", retireSource);
        Assert.DoesNotContain("CsrFile", retireSource);

        string microOpSource = File.ReadAllText(microOpPath);
        Assert.Contains("VmxRetireEffect.Fault", microOpSource);
        Assert.DoesNotContain("VmxExecutionUnit", microOpSource);
        Assert.DoesNotContain("VmxUnit", microOpSource);
    }

    [Fact]
    public void LegacyVmcsManager_VmxPublicationAuthoritySlice_RemainsCoveredAfterFullRemoval()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        string managerPath = Path.Combine(
            projectRoot,
            LegacyVmcsManagerVmxPublicationAuthorityRemovalContract.RemovedManagerPath.Replace('/', Path.DirectorySeparatorChar));
        string interfacePath = Path.Combine(
            projectRoot,
            LegacyVmcsManagerVmxPublicationAuthorityRemovalContract.LegacyInterfacePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(LegacyVmcsManagerVmxPublicationAuthorityRemovalContract.SliceRemovedWithoutReplacement);
        Assert.True(LegacyVmcsManagerVmxPublicationAuthorityRemovalContract.SupersededByFullManagerRemoval);
        Assert.True(LegacyVmcsManagerVmxPublicationAuthorityRemovalContract.RejectsVmExitAndInterceptPublication);
        Assert.True(LegacyVmcsManagerVmxPublicationAuthorityRemovalContract.RejectsStandaloneVirtualEventFrontendSurface);
        Assert.True(LegacyVmcsManagerVmxPublicationAuthorityRemovalContract.RejectsVmxObservabilitySurface);
        Assert.False(File.Exists(managerPath));
        Assert.False(File.Exists(interfacePath));

        string[] currentFailClosedRoutingPaths =
        {
            LegacyVmxExecutionUnitRemovalContract.CurrentDispatcherPath,
            LegacyVmxExecutionUnitRemovalContract.CurrentRetirePath,
            LegacyVmxExecutionUnitRemovalContract.CurrentMicroOpPath,
        };
        foreach (string relativePath in currentFailClosedRoutingPaths)
        {
            string routingSource = File.ReadAllText(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            foreach (string marker in LegacyVmcsManagerVmxPublicationAuthorityRemovalContract.RemovedSurfaceMarkers)
            {
                Assert.DoesNotContain(marker, routingSource);
            }
        }

    }

    [Fact]
    public void LegacyVmcsManager_IsRemovedWithoutReplacement_AndLivePathsAreDenied()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        string managerPath = Path.Combine(
            projectRoot,
            LegacyVmcsManagerRemovalContract.LegacyOriginPath.Replace('/', Path.DirectorySeparatorChar));
        string interfacePath = Path.Combine(
            projectRoot,
            LegacyVmcsManagerRemovalContract.LegacyInterfacePath.Replace('/', Path.DirectorySeparatorChar));
        string dirtyLogTypesPath = Path.Combine(
            projectRoot,
            LegacyVmcsManagerRemovalContract.DirtyLogProjectionTypesPath.Replace('/', Path.DirectorySeparatorChar));
        var manifest = new VmxQuarantineEvidenceManifest();

        Assert.True(LegacyVmcsManagerRemovalContract.RemovedWithoutReplacement);
        Assert.True(LegacyVmcsManagerRemovalContract.LiveCompatibilityLanePathsFailClosed);
        Assert.True(LegacyVmcsManagerRemovalContract.NativeLaneRuntimesRemainIndependent);
        Assert.True(LegacyVmcsManagerRemovalContract.RemovesVmcsOwnedDirtyTracking);
        Assert.True(manifest.TryGetEntry(
            LegacyVmcsManagerRemovalContract.LegacyOriginPath,
            out VmxQuarantineEvidenceEntry entry));
        Assert.True(entry.IsRemovedWithoutReplacement);
        Assert.False(entry.MustRemainQuarantined);
        Assert.False(File.Exists(managerPath));
        Assert.False(File.Exists(interfacePath));
        AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(projectRoot);

        foreach (string relativePath in LegacyVmcsManagerRemovalContract.ProductionPaths)
        {
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            foreach (string marker in LegacyVmcsManagerRemovalContract.ForbiddenProductionOwnerMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }

        string lane6Source = File.ReadAllText(Path.Combine(
            projectRoot,
            LegacyVmcsManagerRemovalContract.Lane6Path.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("Guest Lane6 compatibility execution is fail-closed", lane6Source);
        Assert.Contains("DmaStreamComputeRuntime.ExecuteMaterializedMicroOpToCommitPending", lane6Source);

        string lane7Source = File.ReadAllText(Path.Combine(
            projectRoot,
            LegacyVmcsManagerRemovalContract.Lane7Path.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("Guest Lane7 compatibility execution is fail-closed", lane7Source);
        Assert.Contains("core.GetExternalAcceleratorRuntime()", lane7Source);

        string dirtyLogTypes = File.ReadAllText(dirtyLogTypesPath);
        Assert.DoesNotContain("VmxDirtyLogManager", dirtyLogTypes);
        Assert.DoesNotContain("IVmxDirtyWriteSink", dirtyLogTypes);
    }

    [Fact]
    public void VmcsV2VectorStreamProjectionAuthorityHelpers_AreRemovedWithoutReplacement()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");

        Assert.True(VmcsV2VectorStreamProjectionAuthorityRemovalContract.RemovedWithoutReplacement);
        Assert.True(VmcsV2VectorStreamProjectionAuthorityRemovalContract.RejectsVmcsDescriptorOwnedVectorSaveRestore);
        Assert.True(VmcsV2VectorStreamProjectionAuthorityRemovalContract.RejectsVmcsDescriptorOwnedStreamValidationEvidence);
        Assert.True(VmcsV2VectorStreamProjectionAuthorityRemovalContract.KeepsFrozenVectorStreamVocabularyProjectionOnly);
        Assert.True(VmcsV2VectorStreamProjectionAuthorityRemovalContract.GenericVectorStreamRuntimeRemainsNeutralOwner);

        foreach (string relativePath in VmcsV2VectorStreamProjectionAuthorityRemovalContract.RemovedPaths)
        {
            Assert.False(File.Exists(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        string neutralRuntimeSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmcsV2VectorStreamProjectionAuthorityRemovalContract.NeutralRuntimePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("ExecutionExtensionDescriptor", neutralRuntimeSource);
        Assert.Contains("VectorStreamExecutionExtensionDescriptor", neutralRuntimeSource);
        Assert.DoesNotContain("VmcsV2Descriptor", neutralRuntimeSource);

        string projectionGateSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmcsV2VectorStreamProjectionAuthorityRemovalContract.ProjectionGatePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("ReadOnlyProjection", projectionGateSource);
        Assert.Contains("AuthoritativeMutationDenied", projectionGateSource);

        AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(projectRoot);

        foreach (string sourcePath in EnumerateProductionSources(projectRoot))
        {
            string source = File.ReadAllText(sourcePath);
            foreach (string marker in VmcsV2VectorStreamProjectionAuthorityRemovalContract.ForbiddenProductionOwnerMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }

            foreach (string marker in VmcsV2VectorStreamProjectionAuthorityRemovalContract.ForbiddenNewRuntimeOwnerMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }
    }

    [Fact]
    public void VmxCheckpointVmcsScalarAuthority_IsRemovedWithoutReplacement()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");

        Assert.True(VmxCheckpointVmcsScalarAuthorityRemovalContract.RemovedWithoutReplacement);
        Assert.True(VmxCheckpointVmcsScalarAuthorityRemovalContract.RejectsVmcsScalarCheckpointImageAuthority);
        Assert.True(VmxCheckpointVmcsScalarAuthorityRemovalContract.RejectsVmcsDescriptorScalarRestoreAuthority);
        Assert.True(VmxCheckpointVmcsScalarAuthorityRemovalContract.KeepsVmcsVocabularyProjectionOnly);
        Assert.True(VmxCheckpointVmcsScalarAuthorityRemovalContract.GenericDomainCheckpointRemainsNeutralOwner);

        foreach (string relativePath in VmxCheckpointVmcsScalarAuthorityRemovalContract.RemovedPaths)
        {
            Assert.False(File.Exists(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        string descriptorSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmxCheckpointVmcsScalarAuthorityRemovalContract.VmcsDescriptorPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.DoesNotContain("TryCreateGuestCheckpoint", descriptorSource);
        Assert.DoesNotContain("SnapshotMigratableScalarFields", descriptorSource);
        Assert.DoesNotContain("RestoreGuestStateForMigration", descriptorSource);
        Assert.DoesNotContain("RestoreScalarFieldForMigration", descriptorSource);
        Assert.DoesNotContain("TryWriteScalarField", descriptorSource);
        Assert.Contains("TryReadScalarField", descriptorSource);

        string domainCheckpointSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmxCheckpointVmcsScalarAuthorityRemovalContract.DomainCheckpointImagePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("DomainCheckpointAuthority.DomainDescriptor", domainCheckpointSource);
        Assert.Contains("CompatibilityProjection", domainCheckpointSource);
        Assert.DoesNotContain("VmcsV2Descriptor", domainCheckpointSource);
        Assert.DoesNotContain("VmcsField", domainCheckpointSource);

        string restoreValidationSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmxCheckpointVmcsScalarAuthorityRemovalContract.RestoreValidationServicePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("CompatibilityProjectionDenied", restoreValidationSource);
        Assert.DoesNotContain("VmcsV2Descriptor", restoreValidationSource);
        Assert.DoesNotContain("VmcsField", restoreValidationSource);

        AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(projectRoot);

        foreach (string sourcePath in EnumerateProductionSources(projectRoot))
        {
            string source = File.ReadAllText(sourcePath);
            foreach (string marker in VmxCheckpointVmcsScalarAuthorityRemovalContract.ForbiddenProductionOwnerMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }

            foreach (string marker in VmxCheckpointVmcsScalarAuthorityRemovalContract.ForbiddenNewRuntimeOwnerMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }
    }

    [Fact]
    public void VmcsV2Descriptor_ScalarWriteAuthority_IsRemovedWithoutReplacement()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");

        Assert.True(VmcsV2ScalarWriteAuthorityRemovalContract.RemovedWithoutReplacement);
        Assert.True(VmcsV2ScalarWriteAuthorityRemovalContract.RejectsVmcsV2PublicScalarWriteAuthority);
        Assert.True(VmcsV2ScalarWriteAuthorityRemovalContract.KeepsVmcsV2DescriptorReadProjectionOnly);
        Assert.True(VmcsV2ScalarWriteAuthorityRemovalContract.RejectsVmcsFieldStoreReplacementOwner);
        Assert.True(VmcsV2ScalarWriteAuthorityRemovalContract.KeepsFrozenFieldSchemaVocabularyOnly);

        Assert.Null(typeof(YAKSys_Hybrid_CPU.Core.Vmcs.V2.VmcsV2Descriptor).GetMethod("TryWriteScalarField"));

        string descriptorSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmcsV2ScalarWriteAuthorityRemovalContract.VmcsDescriptorPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("TryReadScalarField", descriptorSource);
        Assert.DoesNotContain("WriteKnownScalar", descriptorSource);
        foreach (string marker in VmcsV2ScalarWriteAuthorityRemovalContract.ForbiddenDescriptorMutationMarkers)
        {
            Assert.DoesNotContain(marker, descriptorSource);
        }

        string projectionGateSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmcsV2ScalarWriteAuthorityRemovalContract.ProjectionGatePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("ReadOnlyProjection", projectionGateSource);
        Assert.Contains("AuthoritativeMutationDenied", projectionGateSource);
        Assert.Contains("WritableProjectionDenied", projectionGateSource);

        string fieldSchemaSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmcsV2ScalarWriteAuthorityRemovalContract.FieldProjectionSchemaPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("VmcsFieldProjectionOwner.ExecutionDomainDescriptor", fieldSchemaSource);
        Assert.Contains("VmcsFieldProjectionOwner.MemoryDomainDescriptor", fieldSchemaSource);
        Assert.Contains("VmcsFieldProjectionOwner.CompatibilityControlDescriptor", fieldSchemaSource);

        AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(projectRoot);

        foreach (string sourcePath in EnumerateProductionSources(projectRoot))
        {
            string source = File.ReadAllText(sourcePath);
            Assert.DoesNotContain("TryWriteScalarField", source);
            foreach (string marker in VmcsV2ScalarWriteAuthorityRemovalContract.ForbiddenNewRuntimeOwnerMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }
    }

    [Fact]
    public void VmcsV2Descriptor_ScalarProjectionCache_IsDeniedWithoutReplacement()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");

        Assert.True(VmcsV2ScalarProjectionCacheAuthorityRemovalContract.RemovedWithoutReplacement);
        Assert.True(VmcsV2ScalarProjectionCacheAuthorityRemovalContract.RejectsDescriptorOwnedScalarProjectionCache);
        Assert.True(VmcsV2ScalarProjectionCacheAuthorityRemovalContract.KeepsTryReadScalarFieldAsDeniedCompatibilityAbi);
        Assert.True(VmcsV2ScalarProjectionCacheAuthorityRemovalContract.RequiresGeneratedReadOnlyProjectionOverNeutralOwners);
        Assert.True(VmcsV2ScalarProjectionCacheAuthorityRemovalContract.RejectsScalarProjectionStoreReplacementOwner);

        Type descriptorType = typeof(YAKSys_Hybrid_CPU.Core.Vmcs.V2.VmcsV2Descriptor);
        const System.Reflection.BindingFlags instanceAnyVisibility =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic;
        Assert.Null(descriptorType.GetField("_scalarValues", instanceAnyVisibility));
        Assert.Null(descriptorType.GetField("_scalarWritten", instanceAnyVisibility));
        Assert.Null(descriptorType.GetMethod("WriteKnownScalar", instanceAnyVisibility));
        Assert.Null(descriptorType.GetMethod("HasScalarFieldValue", instanceAnyVisibility));
        Assert.Null(descriptorType.GetMethod("TryGetScalarFieldValue", instanceAnyVisibility));
        Assert.NotNull(descriptorType.GetMethod("TryReadScalarField", instanceAnyVisibility));

        string descriptorSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmcsV2ScalarProjectionCacheAuthorityRemovalContract.VmcsDescriptorPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("TryReadScalarField", descriptorSource);
        Assert.Contains("scalar projection cache was removed", descriptorSource);
        foreach (string marker in VmcsV2ScalarProjectionCacheAuthorityRemovalContract.ForbiddenDescriptorCacheMarkers)
        {
            Assert.DoesNotContain(marker, descriptorSource);
        }

        var descriptor = YAKSys_Hybrid_CPU.Core.Vmcs.V2.VmcsV2Descriptor.CreateDefault();
        Assert.False(descriptor.TryReadScalarField(
            (ushort)VmcsField.GuestPc,
            out long projectedValue,
            out YAKSys_Hybrid_CPU.Core.Vmcs.V2.VmcsV2ValidationResult validation));
        Assert.Equal(0, projectedValue);
        Assert.Equal(YAKSys_Hybrid_CPU.Core.Vmcs.V2.VmcsV2ValidationCode.AccessDenied, validation.Code);
        Assert.Contains("scalar projection cache was removed", validation.Message);

        string projectionGateSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmcsV2ScalarProjectionCacheAuthorityRemovalContract.ProjectionGatePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("ReadOnlyProjection", projectionGateSource);
        Assert.Contains("AuthoritativeMutationDenied", projectionGateSource);
        Assert.Contains("WritableProjectionDenied", projectionGateSource);

        string fieldSchemaSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmcsV2ScalarProjectionCacheAuthorityRemovalContract.FieldProjectionSchemaPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("VmcsFieldProjectionOwner.ExecutionDomainDescriptor", fieldSchemaSource);
        Assert.Contains("VmcsFieldProjectionOwner.MemoryDomainDescriptor", fieldSchemaSource);
        Assert.Contains("VmcsFieldProjectionOwner.CompletionRecord", fieldSchemaSource);

        AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(projectRoot);

        foreach (string sourcePath in EnumerateProductionSources(projectRoot))
        {
            string source = File.ReadAllText(sourcePath);
            foreach (string marker in VmcsV2ScalarProjectionCacheAuthorityRemovalContract.ForbiddenDescriptorCacheMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }

            foreach (string marker in VmcsV2ScalarProjectionCacheAuthorityRemovalContract.ForbiddenNewRuntimeOwnerMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }
    }

    [Fact]
    public void VmcsV2Blocks_DirtyVectorCheckpointHelpers_AreRemovedWithoutReplacement()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");

        Assert.True(VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.RemovedWithoutReplacement);
        Assert.True(VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.RejectsVmcsBlocksDirtyLogMutationAuthority);
        Assert.True(VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.RejectsVmcsBlocksVectorSaveRestoreAuthority);
        Assert.True(VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.RejectsVmcsBlocksCheckpointRestoreAuthority);
        Assert.True(VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.KeepsVmcsBlocksVocabularyProjectionOnly);
        Assert.True(VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.GenericVectorStreamRuntimeRemainsNeutralOwner);
        Assert.True(VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.GenericDomainCheckpointRemainsNeutralOwner);
        Assert.True(VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.CompatibilityDirtyAccountingRemainsFailClosed);

        string blocksSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.VmcsBlocksPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("public sealed class VectorStreamStateBlock", blocksSource);
        Assert.Contains("public sealed class DirtyLogBlock", blocksSource);
        Assert.Contains("public VmxDirtyLogStatus SnapshotStatus()", blocksSource);
        Assert.DoesNotMatch(@"LastSnapshot\s*\{[^}]*private\s+set", blocksSource);
        Assert.DoesNotMatch(@"LastSnapshot\s*=\s*(?!>)", blocksSource);
        foreach (string marker in VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.ForbiddenVmcsBlocksHelperMarkers)
        {
            Assert.DoesNotContain(marker, blocksSource);
        }

        string descriptorSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.VmcsDescriptorPath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (string marker in VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.ForbiddenDescriptorHelperMarkers)
        {
            Assert.DoesNotContain(marker, descriptorSource);
        }

        string vectorRuntimeSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.NeutralVectorStreamRuntimePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("VectorStreamExecutionExtensionDescriptor", vectorRuntimeSource);
        Assert.Contains("IsRuntimeAuthoritative", vectorRuntimeSource);
        Assert.DoesNotContain("VmcsV2Descriptor", vectorRuntimeSource);
        Assert.DoesNotContain("VmcsField", vectorRuntimeSource);

        string checkpointSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.NeutralDomainCheckpointImagePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("DomainCheckpointAuthority.DomainDescriptor", checkpointSource);
        Assert.Contains("Compatibility projection checkpoint cannot restore authoritative domain state.", checkpointSource);
        Assert.DoesNotContain("VmcsV2Descriptor", checkpointSource);
        Assert.DoesNotContain("VmcsField", checkpointSource);

        string compatibilityAliasSource = File.ReadAllText(Path.Combine(
            projectRoot,
            VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.VmxCompatibilityAliasesPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("TryAccountNptWriteProtectDirty", compatibilityAliasSource);
        Assert.Contains("return false;", compatibilityAliasSource);

        AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(projectRoot);

        foreach (string sourcePath in EnumerateProductionSources(projectRoot))
        {
            string source = File.ReadAllText(sourcePath);
            foreach (string marker in VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract.ForbiddenNewRuntimeOwnerMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }
        }
    }

    [Fact]
    public void LegacyVmcsManager_Lane7GuestCompatibilityExecutionFailsClosedBeforeRuntimeResult()
    {
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
        core.WriteVirtualThreadPipelineState(0, PipelineState.GuestExecution);
        var microOp = new AcceleratorQueryCapsMicroOp();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => microOp.Execute(ref core));

        Assert.Contains("Guest Lane7 compatibility execution is fail-closed", exception.Message);
        Assert.Null(microOp.LastCommandResult);
    }

    [Theory]
    [InlineData(InstructionsEnum.VMXON, VmxOperationKind.VmxOn)]
    [InlineData(InstructionsEnum.VMXOFF, VmxOperationKind.VmxOff)]
    [InlineData(InstructionsEnum.VMLAUNCH, VmxOperationKind.VmLaunch)]
    [InlineData(InstructionsEnum.VMRESUME, VmxOperationKind.VmResume)]
    [InlineData(InstructionsEnum.VMREAD, VmxOperationKind.VmRead)]
    [InlineData(InstructionsEnum.VMWRITE, VmxOperationKind.VmWrite)]
    [InlineData(InstructionsEnum.VMCLEAR, VmxOperationKind.VmClear)]
    [InlineData(InstructionsEnum.VMPTRLD, VmxOperationKind.VmPtrLd)]
    [InlineData(InstructionsEnum.VMPTRST, VmxOperationKind.VmPtrSt)]
    [InlineData(InstructionsEnum.VMCALL, VmxOperationKind.VmCall)]
    [InlineData(InstructionsEnum.INVEPT, VmxOperationKind.Invept)]
    [InlineData(InstructionsEnum.INVVPID, VmxOperationKind.Invvpid)]
    [InlineData(InstructionsEnum.VMFUNC, VmxOperationKind.VmFunc)]
    [InlineData(InstructionsEnum.VMSAVEX, VmxOperationKind.VmSaveX)]
    [InlineData(InstructionsEnum.VMRESTX, VmxOperationKind.VmRestX)]
    public void RemovedLegacyVmxExecutionUnit_FrozenOpcodesRemainTypedAndFailClosed(
        InstructionsEnum opcode,
        VmxOperationKind operation)
    {
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        var microOp = new VmxMicroOp
        {
            OpCode = (uint)opcode,
            Instruction = new InstructionIR
            {
                CanonicalOpcode = opcode,
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = 0,
                Rs1 = 0,
                Rs2 = 0,
                Imm = 0,
            },
        };

        Assert.True(microOp.Execute(ref core));
        VmxRetireEffect effect = microOp.CreateRetireEffect();

        Assert.Equal(operation, effect.Operation);
        Assert.True(effect.IsFaulted);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, effect.FailureReason);

        VmxRetireOutcome outcome = core.ApplyRetiredVmxEffectForTesting(effect, virtualThreadId: 0);

        Assert.True(outcome.Faulted);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, outcome.FailureReason);
        Assert.False(outcome.HasRegisterWriteback);
    }

    private static IEnumerable<string> EnumerateProductionSources(string projectRoot)
    {
        foreach (string sourcePath in Directory.GetFiles(projectRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsSkippedProductionSource(sourcePath))
            {
                continue;
            }

            yield return sourcePath;
        }
    }

    private static bool IsSkippedProductionSource(string sourcePath)
    {
        string normalized = sourcePath.Replace(Path.DirectorySeparatorChar, '/');
        return normalized.Contains("/bin/", StringComparison.Ordinal) ||
            normalized.Contains("/obj/", StringComparison.Ordinal) ||
            normalized.Contains("/Core/VMX/Conformance/", StringComparison.Ordinal) ||
            normalized.Contains("/Legacy/VMX/Conformance/", StringComparison.Ordinal);
    }

    private static void AssertPhysicalLegacyQuarantineDoesNotRestoreRemovedHeavyCarriers(string projectRoot)
    {
        string quarantineRoot = Path.Combine(projectRoot, "Legacy", "VMX");

        Assert.Empty(GetCSharpSourcesIfDirectoryExists(quarantineRoot));
        Assert.False(File.Exists(Path.Combine(
            quarantineRoot,
            "Compatibility",
            "Frontend",
            "Handlers",
            "VmxExecutionUnit.cs")));
        Assert.False(File.Exists(Path.Combine(
            quarantineRoot,
            "Substrate",
            "Runtime",
            "Binding",
            "VmcsManager.cs")));
    }

    private static string[] GetCSharpSourcesIfDirectoryExists(string root) =>
        Directory.Exists(root)
            ? Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            : Array.Empty<string>();

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE.sln")) ||
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HybridCPU ISE repository root.");
    }
}
