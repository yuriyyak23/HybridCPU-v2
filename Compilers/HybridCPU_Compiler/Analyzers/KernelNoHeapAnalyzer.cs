using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace HybridCPU.Compiler.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class KernelNoHeapAnalyzer : DiagnosticAnalyzer
{
    public const string ProfileAttributeMetadataName = "HybridCPU.Compiler.Profiles.HybridCpuKernelNoHeapAttribute";
    private static readonly DiagnosticDescriptor Allocation = Rule("HC0004",
        "Managed heap allocation is not permitted", "'{0}' allocates in HybridCPU.Kernel.NoHeap.");
    private static readonly DiagnosticDescriptor RuntimeFeature = Rule("HC0005",
        "Runtime feature is not permitted", "'{0}' is not permitted in HybridCPU.Kernel.NoHeap.");
    private static readonly DiagnosticDescriptor LanguageFeature = Rule("HC0006",
        "Language feature is not permitted", "'{0}' is not permitted in HybridCPU.Kernel.NoHeap.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [Allocation, RuntimeFeature, LanguageFeature];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeAllocation,
            SyntaxKind.ObjectCreationExpression, SyntaxKind.ArrayCreationExpression,
            SyntaxKind.ImplicitArrayCreationExpression, SyntaxKind.AnonymousObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeForbiddenLanguage,
            SyntaxKind.SimpleLambdaExpression, SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.AnonymousMethodExpression, SyntaxKind.AwaitExpression,
            SyntaxKind.InterpolatedStringExpression);
        context.RegisterSyntaxNodeAction(AnalyzeStringConcatenation, SyntaxKind.AddExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterOperationAction(AnalyzeConversion, OperationKind.Conversion);
        context.RegisterOperationAction(AnalyzeDynamic, OperationKind.DynamicInvocation,
            OperationKind.DynamicMemberReference, OperationKind.DynamicIndexerAccess,
            OperationKind.DynamicObjectCreation);
    }

    private static void AnalyzeAllocation(SyntaxNodeAnalysisContext context)
    {
        if (!IsProfiled(context)) return;
        context.ReportDiagnostic(Diagnostic.Create(Allocation, context.Node.GetLocation(), context.Node.Kind().ToString()));
    }

    private static void AnalyzeForbiddenLanguage(SyntaxNodeAnalysisContext context)
    {
        if (!IsProfiled(context)) return;
        string feature = context.Node.Kind() switch
        {
            SyntaxKind.AwaitExpression => "async/await",
            SyntaxKind.InterpolatedStringExpression => "interpolated string",
            _ => "delegate or closure creation"
        };
        context.ReportDiagnostic(Diagnostic.Create(LanguageFeature, context.Node.GetLocation(), feature));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (!IsProfiled(context) || context.SemanticModel.GetSymbolInfo(context.Node).Symbol is not IMethodSymbol method)
            return;
        string owner = method.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        string identity = owner + "." + method.Name;
        if (owner is "System.GC" or "System.Threading.Thread" or "System.Threading.ThreadPool" ||
            owner.StartsWith("System.Reflection.", StringComparison.Ordinal) ||
            owner.StartsWith("System.Linq.", StringComparison.Ordinal) ||
            identity is "System.Activator.CreateInstance" or "System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject" or
                "System.Delegate.CreateDelegate" ||
            identity.StartsWith("System.Threading.Tasks.Task.", StringComparison.Ordinal) ||
            identity.StartsWith("System.Reflection.Assembly.Load", StringComparison.Ordinal))
            context.ReportDiagnostic(Diagnostic.Create(RuntimeFeature, context.Node.GetLocation(), identity));
    }

    private static void AnalyzeStringConcatenation(SyntaxNodeAnalysisContext context)
    {
        if (!IsProfiled(context) || context.Node is not BinaryExpressionSyntax expression ||
            context.SemanticModel.GetTypeInfo(expression).Type?.SpecialType != SpecialType.System_String) return;
        context.ReportDiagnostic(Diagnostic.Create(Allocation, expression.GetLocation(), "string concatenation"));
    }

    private static void AnalyzeConversion(OperationAnalysisContext context)
    {
        if (!IsProfiled(context.ContainingSymbol) || context.Operation is not IConversionOperation conversion ||
            conversion.Operand.Type?.IsValueType != true || conversion.Type?.IsReferenceType != true) return;
        context.ReportDiagnostic(Diagnostic.Create(Allocation, context.Operation.Syntax.GetLocation(), "boxing conversion"));
    }

    private static void AnalyzeDynamic(OperationAnalysisContext context)
    {
        if (!IsProfiled(context.ContainingSymbol)) return;
        context.ReportDiagnostic(Diagnostic.Create(LanguageFeature, context.Operation.Syntax.GetLocation(), "dynamic"));
    }

    private static bool IsProfiled(SyntaxNodeAnalysisContext context)
        => IsProfiled(context.ContainingSymbol);

    private static bool IsProfiled(ISymbol? owner)
    {
        while (owner is not null)
        {
            if (owner.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == ProfileAttributeMetadataName))
                return true;
            owner = owner.ContainingSymbol;
        }
        return false;
    }

    private static DiagnosticDescriptor Rule(string id, string title, string message) => new(
        id, title, message, "HybridCPU.Profile", DiagnosticSeverity.Error, isEnabledByDefault: true,
        description: "Source-profile diagnostics are conservative. CIL importer and whole-program admission remain mandatory.");
}
