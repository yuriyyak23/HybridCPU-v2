using System;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Stable fail-closed taxonomy for execution/retire contract violations that must
    /// remain observable across leaf throws and boundary wrappers.
    /// </summary>
    public enum ExecutionFaultCategory : byte
    {
        UnsupportedExecutionSurface = 1,
        InvalidInternalOp = 2,
        UnsupportedVectorElementType = 3,
        ReferenceModelValidation = 4,
        InvalidPipelineLane = 5,
        InvalidVirtualThread = 6
    }

    /// <summary>
    /// Canonical message/category/inner-exception contract for fail-closed execution faults.
    /// Leaf faults publish a prefixed message with no inner exception; wrappers preserve the
    /// original fault as <see cref="Exception.InnerException"/> and restamp the same category.
    /// </summary>
    public static class ExecutionFaultContract
    {
        private const string CategoryDataKey = "HybridCPU.ExecutionFaultCategory";
        private const string PrefixLead = "[FailClosed/";

        public static string GetMessagePrefix(ExecutionFaultCategory category)
            => $"{PrefixLead}{category}]";

        public static string FormatMessage(ExecutionFaultCategory category, string detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
                throw new ArgumentException("Execution fault detail must not be null or empty.", nameof(detail));

            return $"{GetMessagePrefix(category)} {detail}";
        }

        public static void Stamp(Exception exception, ExecutionFaultCategory category)
        {
            ArgumentNullException.ThrowIfNull(exception);
            exception.Data[CategoryDataKey] = category;
        }

        public static ExecutionFaultCategory GetCategory(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            if (TryGetCategory(exception, out ExecutionFaultCategory category))
            {
                return category;
            }

            throw new ArgumentException(
                "Exception does not carry a stable execution fault category.",
                nameof(exception));
        }

        public static bool TryGetCategory(Exception? exception, out ExecutionFaultCategory category)
        {
            if (TryGetDirectCategory(exception, out category))
            {
                return true;
            }

            if (exception?.InnerException != null &&
                TryGetCategory(exception.InnerException, out category))
            {
                return true;
            }

            category = default;
            return false;
        }

        public static InvalidOperationException CreateUnsupportedVectorElementTypeException(string detail)
        {
            InvalidOperationException exception =
                new(FormatMessage(ExecutionFaultCategory.UnsupportedVectorElementType, detail));
            Stamp(exception, ExecutionFaultCategory.UnsupportedVectorElementType);
            return exception;
        }

        public static ArgumentOutOfRangeException CreateReferenceModelValidationException(
            string paramName,
            object? actualValue,
            string detail)
        {
            ArgumentOutOfRangeException exception =
                new(paramName, actualValue, FormatMessage(ExecutionFaultCategory.ReferenceModelValidation, detail));
            Stamp(exception, ExecutionFaultCategory.ReferenceModelValidation);
            return exception;
        }

        public static InvalidOperationException CreateWrappedException(
            ExecutionFaultCategory category,
            string detail,
            Exception innerException)
        {
            ArgumentNullException.ThrowIfNull(innerException);

            InvalidOperationException exception =
                new(FormatMessage(category, detail), innerException);
            Stamp(exception, category);
            return exception;
        }

        private static bool TryGetDirectCategory(Exception? exception, out ExecutionFaultCategory category)
        {
            switch (exception)
            {
                case UnsupportedExecutionSurfaceException unsupportedExecutionSurfaceException:
                    category = unsupportedExecutionSurfaceException.Category;
                    return true;
                case Execution.InvalidInternalOpException invalidInternalOpException:
                    category = invalidInternalOpException.Category;
                    return true;
            }

            if (exception?.Data[CategoryDataKey] is ExecutionFaultCategory typedCategory)
            {
                category = typedCategory;
                return true;
            }

            if (exception?.Data[CategoryDataKey] is string serializedCategory &&
                Enum.TryParse(serializedCategory, ignoreCase: false, out category))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(exception?.Message) &&
                TryParseCategoryFromMessagePrefix(exception.Message, out category))
            {
                return true;
            }

            category = default;
            return false;
        }

        private static bool TryParseCategoryFromMessagePrefix(
            string message,
            out ExecutionFaultCategory category)
        {
            category = default;

            if (!message.StartsWith(PrefixLead, StringComparison.Ordinal))
            {
                return false;
            }

            int prefixEnd = message.IndexOf(']');
            if (prefixEnd <= PrefixLead.Length)
            {
                return false;
            }

            string categoryText = message.Substring(PrefixLead.Length, prefixEnd - PrefixLead.Length);
            return Enum.TryParse(categoryText, ignoreCase: false, out category);
        }
    }
}
