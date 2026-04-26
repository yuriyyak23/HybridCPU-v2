namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Result of a metadata schema compatibility validation.
    /// </summary>
    public readonly struct ValidationResult
    {
        /// <summary>Whether the validation passed (possibly with a warning).</summary>
        public bool IsValid { get; }

        /// <summary>Severity of the result.</summary>
        public ValidationSeverity Severity { get; }

        /// <summary>Human-readable message, or null if no message.</summary>
        public string? Message { get; }

        private ValidationResult(bool isValid, ValidationSeverity severity, string? message)
        {
            IsValid = isValid;
            Severity = severity;
            Message = message;
        }

        /// <summary>Creates an OK result with no message.</summary>
        public static ValidationResult Ok() =>
            new(isValid: true, ValidationSeverity.None, message: null);

        /// <summary>Creates a warning result — validation passed, but with a note.</summary>
        public static ValidationResult Warning(string message) =>
            new(isValid: true, ValidationSeverity.Warning, message);

        /// <summary>Creates an error result — validation failed.</summary>
        public static ValidationResult Error(string message) =>
            new(isValid: false, ValidationSeverity.Error, message);

        /// <inheritdoc/>
        public override string ToString() =>
            Severity == ValidationSeverity.None ? "Ok" : $"{Severity}: {Message}";
    }

    /// <summary>Severity level for a <see cref="ValidationResult"/>.</summary>
    public enum ValidationSeverity : byte
    {
        /// <summary>No issue — validation passed cleanly.</summary>
        None = 0,

        /// <summary>Informational warning — validation passed but has a note.</summary>
        Warning = 1,

        /// <summary>Error — validation failed.</summary>
        Error = 2,
    }

    /// <summary>
    /// Validates metadata schema compatibility between compiler-emitted
    /// and ISE-consumed metadata.
    /// <para>
    /// The ISE operates at schema version <see cref="MetadataSchemaVersion.Current"/>.
    /// Older metadata uses defaults for missing fields; newer metadata has unknown
    /// fields silently ignored.
    /// </para>
    /// </summary>
    public static class MetadataCompatibilityValidator
    {
        /// <summary>
        /// Validate schema compatibility of the given <see cref="BundleMetadata"/>.
        /// Returns <see cref="ValidationResult.Ok"/> for exact version match,
        /// a <see cref="ValidationResult.Warning"/> for minor version skew,
        /// or an <see cref="ValidationResult.Error"/> for unsupported schema.
        /// </summary>
        public static ValidationResult Validate(BundleMetadata metadata)
        {
            byte version = metadata.SchemaVersion;

            if (version == MetadataSchemaVersion.Current)
                return ValidationResult.Ok();

            if (version > MetadataSchemaVersion.Current)
                return ValidationResult.Warning(
                    $"BundleMetadata schema v{version} is newer than ISE v{MetadataSchemaVersion.Current} " +
                    $"— unknown fields will be ignored");

            // version < Current
            return ValidationResult.Warning(
                $"BundleMetadata schema v{version} is older than ISE v{MetadataSchemaVersion.Current} " +
                $"— missing fields will use defaults");
        }

        /// <summary>
        /// Validate schema compatibility of the given <see cref="SlotMetadata"/>.
        /// </summary>
        public static ValidationResult Validate(SlotMetadata metadata)
        {
            byte version = metadata.SchemaVersion;

            if (version == MetadataSchemaVersion.Current)
                return ValidationResult.Ok();

            if (version > MetadataSchemaVersion.Current)
                return ValidationResult.Warning(
                    $"SlotMetadata schema v{version} is newer than ISE v{MetadataSchemaVersion.Current} " +
                    $"— unknown fields will be ignored");

            return ValidationResult.Warning(
                $"SlotMetadata schema v{version} is older than ISE v{MetadataSchemaVersion.Current} " +
                $"— missing fields will use defaults");
        }
    }
}
