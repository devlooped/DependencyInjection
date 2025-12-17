using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Devlooped.Extensions.DependencyInjection;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class ConventionsAnalyzer : DiagnosticAnalyzer
{
    public static DiagnosticDescriptor AssignableTypeOfRequired { get; } = new DiagnosticDescriptor(
        "DDI002",
        "The convention-based registration requires a typeof() expression.",
        "When registering services by type, typeof() must be used exclusively to avoid run-time reflection.",
        "Build",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor OpenGenericType { get; } = new DiagnosticDescriptor(
        "DDI003",
        "Open generic service implementations are not supported for convention-based registration.",
        "Only the concrete (closed) implementations of the open generic interface will be registered. Register open generic services explicitly using the built-in service collection methods.",
        "Build",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(AssignableTypeOfRequired, OpenGenericType);

    public override void Initialize(AnalysisContext context)
    {
        if (!Debugger.IsAttached)
            context.EnableConcurrentExecution();

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(startContext =>
        {
            var servicesCollection = startContext.Compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection");
            if (servicesCollection == null)
                return;

            startContext.RegisterSemanticModelAction(semanticContext =>
            {
                var semantic = semanticContext.SemanticModel;
                var invocations = semantic.SyntaxTree
                    .GetRoot(semanticContext.CancellationToken)
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Select(invocation => new { Invocation = invocation, semantic.GetSymbolInfo(invocation, semanticContext.CancellationToken).Symbol })
                    .Where(x => x.Symbol is IMethodSymbol method &&
                        method.GetAttributes().Any(attr => attr.AttributeClass?.Name == "DDIAddServicesAttribute") &&
                        // This signals the convention overloads that take a type, regex and lifetime.
                        method.Parameters.Length > 1)
                    .Select(x => new { x.Invocation, Method = (IMethodSymbol)x.Symbol! });

                foreach (var invocation in invocations)
                {
                    for (var i = 0; i < invocation.Invocation.ArgumentList.Arguments.Count; i++)
                    {
                        var arg = invocation.Invocation.ArgumentList.Arguments[i];
                        var prm = invocation.Method.Parameters[i];
                        if (prm.Type.Name == "Type" && prm.Type.ContainingNamespace.Name == "System")
                        {
                            if (arg.Expression is not TypeOfExpressionSyntax typeExpr)
                                semanticContext.ReportDiagnostic(Diagnostic.Create(AssignableTypeOfRequired, arg.GetLocation()));
                            else if (semantic.GetSymbolInfo(typeExpr.Type).Symbol is INamedTypeSymbol argType && argType.IsGenericType && argType.IsUnboundGenericType)
                                semanticContext.ReportDiagnostic(Diagnostic.Create(OpenGenericType, arg.GetLocation()));
                        }
                    }
                }
            });
        });
    }
}