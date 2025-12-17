using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Devlooped.Extensions.DependencyInjection;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public class LegacyServiceAttributeAnalyzer : DiagnosticAnalyzer
{
    public static DiagnosticDescriptor ServiceTypeNotKeyType { get; } = new DiagnosticDescriptor(
        "DDI005",
        "Generic parameter for ServiceAttribute must be the service type to register, not the service key type.",
        "Generic parameter must not be the type of service key in use but rather the service type to register, if any.",
        "Build",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(ServiceTypeNotKeyType);

    public override void Initialize(AnalysisContext context)
    {
        if (!Debugger.IsAttached)
            context.EnableConcurrentExecution();

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { IsGenericType: true } attrClass)
                continue;

            if (attrClass.Name != "ServiceAttribute" && attrClass.Name != "Service")
                continue;

            if (attrClass.TypeArguments.Length != 1)
                continue;

            var typeArgument = attrClass.TypeArguments[0];
            // Registering as generic object should be ok.
            if (typeArgument.SpecialType == SpecialType.System_Object)
                continue;

            if (attribute.ConstructorArguments.Length == 0)
                continue;

            var keyArg = attribute.ConstructorArguments[0];

            // If they match, this would be the legacy usage scenario we want to fix
            if (keyArg.Type is not null &&
                SymbolEqualityComparer.Default.Equals(keyArg.Type, typeArgument))
            {
                var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                    ?? typeSymbol.Locations.FirstOrDefault();

                if (location is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(ServiceTypeNotKeyType, location));
                }
            }
        }
    }
}
