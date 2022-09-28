﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Devlooped.Extensions.DependencyInjection.Attributed;

/// <summary>
/// Discovers annotated services during compilation and generates the partial method 
/// implementations for <c>AddServices</c> to invoke.
/// </summary>
[Generator(LanguageNames.CSharp)]
public class IncrementalGenerator : IIncrementalGenerator
{
    static readonly SymbolDisplayFormat fullNameFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var types = context.CompilationProvider.SelectMany((x, c) =>
        {
            var visitor = new TypesVisitor(s => x.IsSymbolAccessibleWithin(s, x.Assembly), c);
            x.GlobalNamespace.Accept(visitor);
            return visitor.TypeSymbols;
        });

        bool IsService(AttributeData attr) =>
            (attr.AttributeClass?.Name == "ServiceAttribute" || attr.AttributeClass?.Name == "Service") &&
            attr.ConstructorArguments.Length == 1 &&
            attr.ConstructorArguments[0].Kind == TypedConstantKind.Enum &&
            attr.ConstructorArguments[0].Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Microsoft.Extensions.DependencyInjection.ServiceLifetime";

        bool IsExport(AttributeData attr)
        {
            var attrName = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return attrName == "global::System.Composition.ExportAttribute" ||
                attrName == "global::System.ComponentModel.Composition.ExportAttribute";
        };

        // NOTE: we recognize the attribute by name, not precise type. This makes the generator 
        // more flexible and avoids requiring any sort of run-time dependency.
        var services = types
            .Select((x, _) =>
            {
                var attrs = x.GetAttributes();
                var serviceAttr = attrs.FirstOrDefault(IsService);
                var service = serviceAttr != null || attrs.Any(IsExport);

                if (!service)
                    return null;

                // Default lifetime is singleton for [Service], Transient for MEF
                var lifetime = serviceAttr != null ? 0 : 2;
                if (serviceAttr != null)
                {
                    lifetime = (int)serviceAttr.ConstructorArguments[0].Value!;
                }
                else
                {
                    // In NuGet MEF, [Shared] makes exports singleton
                    if (attrs.Any(a => a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Composition.SharedAttribute"))
                    {
                        lifetime = 0;
                    }
                    // In .NET MEF, [PartCreationPolicy(CreationPolicy.Shared)] does it.
                    else if (attrs.Any(a =>
                        a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.ComponentModel.Composition.PartCreationPolicyAttribute" &&
                        a.ConstructorArguments.Length == 1 &&
                        a.ConstructorArguments[0].Kind == TypedConstantKind.Enum &&
                        a.ConstructorArguments[0].Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.ComponentModel.Composition.CreationPolicy" &&
                        (int)a.ConstructorArguments[0].Value! == 1))
                    {
                        lifetime = 0;
                    }
                }

                return new
                {
                    Type = x,
                    Lifetime = lifetime
                };
            })
            .Where(x => x != null);

        var options = context.AnalyzerConfigOptionsProvider.Combine(
            context.CompilationProvider.Select((c, _) => (Func<ISymbol, bool>)(s => c.IsSymbolAccessibleWithin(s, c.Assembly))));

        // Only requisite is that we define Scoped = 0, Singleton = 1 and Transient = 2.
        // This matches https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.servicelifetime?view=dotnet-plat-ext-6.0#fields
        var singleton = services.Where(x => x!.Lifetime == 0).Select((x, _) => x!.Type).Collect().Combine(options);
        var scoped = services.Where(x => x!.Lifetime == 1).Select((x, _) => x!.Type).Collect().Combine(options);
        var transient = services.Where(x => x!.Lifetime == 2).Select((x, _) => x!.Type).Collect().Combine(options);

        context.RegisterSourceOutput(scoped, (ctx, data) => AddPartial("AddScoped", ctx, data));
        context.RegisterSourceOutput(singleton, (ctx, data) => AddPartial("AddSingleton", ctx, data));
        context.RegisterSourceOutput(transient, (ctx, data) => AddPartial("AddTransient", ctx, data));

        //Debugger.Launch();
    }

    void AddPartial(string methodName, SourceProductionContext ctx, (ImmutableArray<INamedTypeSymbol> Types, (AnalyzerConfigOptionsProvider Config, Func<ISymbol, bool> IsAccessible) Options) data)
    {
        var builder = new StringBuilder();

        var rootNs = data.Options.Config.GlobalOptions.TryGetValue("build_property.AddServicesNamespace", out var value) && !string.IsNullOrEmpty(value)
            ? value
            : "Microsoft.Extensions.DependencyInjection";

        var className = data.Options.Config.GlobalOptions.TryGetValue("build_property.AddServicesClassName", out value) && !string.IsNullOrEmpty(value) ?
            value : "AddServicesExtension";

        builder.AppendLine(
          $$"""
            // <auto-generated />
            using Microsoft.Extensions.DependencyInjection;
            
            namespace {{rootNs}}
            {
                static partial class {{className}}
                {
                    static partial void {{methodName}}Services(IServiceCollection services)
                    {
            """);

        AddServices(data.Types, data.Options.IsAccessible, methodName, builder);
        builder.AppendLine(
        """
                    }
                }
            }
            """);

        ctx.AddSource(methodName + ".g", builder.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine));
    }

    void AddServices(ImmutableArray<INamedTypeSymbol> types, Func<ISymbol, bool> isAccessible, string methodName, StringBuilder output)
    {
        foreach (var type in types)
        {
            var impl = type.ToDisplayString(fullNameFormat);
            var registered = new HashSet<string>();

            var importing = type.InstanceConstructors.FirstOrDefault(m =>
                m.GetAttributes().Any(a =>
                    a.AttributeClass?.ToDisplayString(fullNameFormat) == "global::System.Composition.ImportingConstructorAttribute" ||
                    a.AttributeClass?.ToDisplayString(fullNameFormat) == "global::System.ComponentModel.Composition.ImportingConstructorAttribute"));

            var ctor = importing ?? type.InstanceConstructors
                .Where(m => isAccessible(m))
                .OrderByDescending(m => m.Parameters.Length)
                .FirstOrDefault();

            if (ctor != null && ctor.Parameters.Length > 0)
            {
                var args = string.Join(", ", ctor.Parameters.Select(p => $"s.GetRequiredService<{p.Type.ToDisplayString(fullNameFormat)}>()"));
                output.AppendLine($"            services.{methodName}(s => new {impl}({args}));");
            }
            else
            {
                output.AppendLine($"            services.{methodName}(s => new {impl}());");
            }

            foreach (var iface in type.AllInterfaces)
            {
                var ifaceName = iface.ToDisplayString(fullNameFormat);
                if (!registered.Contains(ifaceName))
                {
                    output.AppendLine($"            services.{methodName}<{iface.ToDisplayString(fullNameFormat)}>(s => s.GetRequiredService<{impl}>());");
                    registered.Add(ifaceName);
                }

                // Register covariant interfaces too, for at most one type parameter.
                // TODO: perhaps explore registering for the full permutation of all out params?
                if (iface.IsGenericType &&
                    iface.TypeParameters.Length == 1 &&
                    iface.TypeParameters[0].Variance == VarianceKind.Out)
                {
                    var typeParam = iface.TypeArguments[0];
                    var candidates = typeParam.AllInterfaces.ToList();
                    var baseType = typeParam.BaseType;
                    while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
                    {
                        candidates.Add(baseType);
                        baseType = baseType.BaseType;
                    }

                    foreach (var candidate in candidates.Select(x => iface.ConstructedFrom.Construct(x))
                        .ToImmutableHashSet(SymbolEqualityComparer.Default)
                        .Where(x => x != null)
                        .Select(x => x!.ToDisplayString(fullNameFormat)))
                    {
                        if (!registered.Contains(candidate))
                        {
                            output.AppendLine($"            services.{methodName}<{candidate}>(s => s.GetRequiredService<{impl}>());");
                            registered.Add(candidate);
                        }
                    }
                }
            }
        }
    }

    class TypesVisitor : SymbolVisitor
    {
        Func<ISymbol, bool> isAccessible;
        CancellationToken cancellation;
        HashSet<INamedTypeSymbol> types = new(SymbolEqualityComparer.Default);

        public TypesVisitor(Func<ISymbol, bool> isAccessible, CancellationToken cancellation)
        {
            this.isAccessible = isAccessible;
            this.cancellation = cancellation;
        }

        public HashSet<INamedTypeSymbol> TypeSymbols => types;

        public override void VisitAssembly(IAssemblySymbol symbol)
        {
            cancellation.ThrowIfCancellationRequested();
            symbol.GlobalNamespace.Accept(this);
        }

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            foreach (var namespaceOrType in symbol.GetMembers())
            {
                cancellation.ThrowIfCancellationRequested();
                namespaceOrType.Accept(this);
            }
        }

        public override void VisitNamedType(INamedTypeSymbol type)
        {
            cancellation.ThrowIfCancellationRequested();

            if (!isAccessible(type) || !types.Add(type))
                return;

            var nestedTypes = type.GetTypeMembers();
            if (nestedTypes.IsDefaultOrEmpty)
                return;

            foreach (var nestedType in nestedTypes)
            {
                cancellation.ThrowIfCancellationRequested();
                nestedType.Accept(this);
            }
        }
    }
}
