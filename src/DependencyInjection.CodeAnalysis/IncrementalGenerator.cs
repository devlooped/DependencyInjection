using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using DecoratedService = (Microsoft.CodeAnalysis.INamedTypeSymbol TDecorated, Microsoft.CodeAnalysis.INamedTypeSymbol TDecorator, bool IsKeyed, bool HasKeyValue, object? KeyValue, Microsoft.CodeAnalysis.Location? Location);
using KeyedService = (Microsoft.CodeAnalysis.INamedTypeSymbol TImplementation, Microsoft.CodeAnalysis.INamedTypeSymbol? TService, Microsoft.CodeAnalysis.TypedConstant? Key);

namespace Devlooped.Extensions.DependencyInjection;

/// <summary>
/// Discovers annotated services during compilation and generates the partial method 
/// implementations for <c>AddServices</c> to invoke.
/// </summary>
[Generator(LanguageNames.CSharp)]
public class IncrementalGenerator : IIncrementalGenerator
{
    public static DiagnosticDescriptor AmbiguousLifetime { get; } =
        new DiagnosticDescriptor(
        "DDI004",
        "Ambiguous lifetime registration.",
        "More than one registration matches {0} with lifetimes {1}.",
        "Build",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor DecoratorMustBeService { get; } =
        new DiagnosticDescriptor(
        "DDI006",
        "Decorator must be annotated with ServiceAttribute.",
        "Decorator type {0} must be annotated with [Service] so its registration is generated before decoration.",
        "Build",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor DecoratorLifetimeIncompatible { get; } =
        new DiagnosticDescriptor(
        "DDI007",
        "Decorator lifetime is incompatible with decorated services.",
        "Decorator type {0} has lifetime {1}, which is incompatible with decorated service {2} lifetimes {3}.",
        "Build",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor DecoratorConstructorMissing { get; } =
        new DiagnosticDescriptor(
        "DDI008",
        "Decorator constructor must accept the decorated service.",
        "Decorator type {0} must have an accessible constructor with exactly one parameter of type {1}; additional dependencies are allowed.",
        "Build",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    class ServiceSymbol(INamedTypeSymbol implementation, int lifetime, TypedConstant? key, Location? location, INamedTypeSymbol? service)
    {
        public INamedTypeSymbol TImplementation => implementation;
        public INamedTypeSymbol? TService => service;
        public int Lifetime => lifetime;
        public TypedConstant? Key => key;
        public Location? Location => location;

        public override bool Equals(object? obj)
        {
            if (obj is not ServiceSymbol other)
                return false;

            return SymbolEqualityComparer.Default.Equals(implementation, other.TImplementation) &&
                SymbolEqualityComparer.Default.Equals(service, other.TService) &&
                lifetime == other.Lifetime &&
                Equals(key, other.Key);
        }

        public override int GetHashCode()
        {
            var hashcode = HashCode.Combine(SymbolEqualityComparer.Default.GetHashCode(implementation), lifetime, key);
            if (service != null)
                hashcode = HashCode.Combine(hashcode, SymbolEqualityComparer.Default.GetHashCode(service));

            return hashcode;
        }
    }

    record ServiceRegistration(int Lifetime, TypeSyntax? AssignableTo, string? FullNameExpression, Location? Location)
    {
        Regex? regex;

        public Regex Regex => (regex ??= FullNameExpression is not null ? new(FullNameExpression) : new(".*"));
    }

    record ServiceAttributeInfo(int Lifetime, TypedConstant? Key, INamedTypeSymbol? ServiceType, Location? Location);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var types = context.CompilationProvider.Combine(context.AnalyzerConfigOptionsProvider).SelectMany((x, c) =>
        {
            (var compilation, var options) = x;

            // We won't add any registrations in these cases.            
            if (options.IsDesignTimeBuild() ||
                !options.GlobalOptions.TryGetValue("build_property.AddServicesExtension", out var value) ||
                !bool.TryParse(value, out var addServices) || !addServices)
                return [];

            var visitor = new TypesVisitor(s => compilation.IsSymbolAccessible(s), c);
            compilation.GlobalNamespace.Accept(visitor);

            // Also visit aliased references, which will not become part of the global:: namespace
            foreach (var symbol in compilation.References
                .Where(r => !r.Properties.Aliases.IsDefaultOrEmpty)
                .Select(r => compilation.GetAssemblyOrModuleSymbol(r)))
            {
                symbol?.Accept(visitor);
            }

            return visitor.TypeSymbols.Where(t => !t.IsAbstract && t.TypeKind == TypeKind.Class);
        });

        // NOTE: we recognize the attribute by name, not precise type. This makes the generator 
        // more flexible and avoids requiring any sort of run-time dependency.

        var attributedServices = types
            .Where(x => x.GetAttributes().Any())
            .Combine(context.CompilationProvider)
            .SelectMany((x, cancellation) =>
            {
                var (type, compilation) = x;
                var name = type.Name;
                var attrs = type.GetAttributes();
                var services = new List<ServiceSymbol>();

                foreach (var attr in attrs)
                {
                    var serviceAttr = IsServiceAttribute(attr) || IsKeyedServiceAttribute(attr) ? attr : null;
                    if (serviceAttr == null && !IsExportAttribute(attr))
                        continue;

                    TypedConstant? key = default;

                    // Default lifetime is singleton for [Service], Transient for MEF
                    var lifetime = serviceAttr != null ? 0 : 2;
                    if (serviceAttr != null)
                    {
                        if (IsKeyedServiceAttribute(serviceAttr))
                        {
                            key = serviceAttr.ConstructorArguments[0];
                            lifetime = (int)serviceAttr.ConstructorArguments[1].Value!;
                        }
                        else
                        {
                            lifetime = (int)serviceAttr.ConstructorArguments[0].Value!;
                        }
                    }
                    else if (IsExportAttribute(attr))
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

                        // Consider the [Export(contractName)] as a keyed service with the contract name as the key.
                        if (attr.ConstructorArguments.Length > 0 &&
                            attr.ConstructorArguments[0].Kind == TypedConstantKind.Primitive)
                        {
                            key = attr.ConstructorArguments[0];
                        }
                    }

                    INamedTypeSymbol? serviceType = null;

                    if (serviceAttr?.AttributeClass?.Arity == 1 && serviceAttr.ApplicationSyntaxReference != null &&
                        serviceAttr.ApplicationSyntaxReference.GetSyntax(cancellation) is AttributeSyntax serviceAttrSyntax &&
                        compilation.GetSemanticModel(serviceAttr.ApplicationSyntaxReference.SyntaxTree).GetSymbolInfo(serviceAttrSyntax, cancellation) is { Symbol: not null } serviceAttrSymbol &&
                        serviceAttrSymbol.Symbol is IMethodSymbol attrCtor &&
                        attrCtor.ContainingType.IsGenericType &&
                        attrCtor.ContainingType.TypeArguments.Length == 1 &&
                        attrCtor.ContainingType.TypeArguments[0] is INamedTypeSymbol attrServiceType)
                    {
                        // We have a specific service type to register.
                        serviceType = attrServiceType;
                    }

                    services.Add(new(type, lifetime, key, attr.ApplicationSyntaxReference?.GetSyntax().GetLocation(), serviceType));
                }

                return services.ToImmutableArray();
            })
            .Where(x => x != null);

        // Only requisite is that we define Scoped = 0, Singleton = 1 and Transient = 2.
        // This matches https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.servicelifetime?view=dotnet-plat-ext-6.0#fields

        // Add conventional registrations.

        // First get all AddServices(type, regex, lifetime) invocations.
        var methodInvocations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax invocation && invocation.ArgumentList.Arguments.Count != 0 && GetInvokedMethodName(invocation) == nameof(AddServicesNoReflectionExtension.AddServices),
                transform: static (ctx, _) => GetServiceRegistration((InvocationExpressionSyntax)ctx.Node, ctx.SemanticModel))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Where(x =>
            {
                (var registration, var options) = x;
                return options.GlobalOptions.TryGetValue("build_property.AddServicesExtension", out var value) &&
                    bool.TryParse(value, out var addServices) && addServices && registration is not null;
            })
            .Select((x, _) => x.Left)
            .Collect();

        var decorations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax invocation && GetInvokedMethodName(invocation) == "Decorate",
                transform: static (ctx, cancellation) => GetDecoration((InvocationExpressionSyntax)ctx.Node, ctx.SemanticModel, cancellation))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Where(x =>
            {
                (var decoration, var options) = x;
                return options.GlobalOptions.TryGetValue("build_property.AddServicesExtension", out var value) &&
                    bool.TryParse(value, out var addServices) && addServices && decoration is not null;
            })
            .Select((x, _) => x.Left!.Value)
            .Collect();

        // Project matching service types to register with the given lifetime.
        var conventionServices = types.Combine(methodInvocations.Combine(context.CompilationProvider)).SelectMany((pair, cancellationToken) =>
        {
            var (typeSymbol, (registrations, compilation)) = pair;
            var results = ImmutableArray.CreateBuilder<ServiceSymbol>();

            foreach (var registration in registrations)
            {
                // check of typeSymbol is assignable (is the same type, inherits from it or implements if its an interface) to registration.AssignableTo
                if (registration!.AssignableTo is not null &&
                    // Resolve the type against the current compilation
                    compilation.GetSemanticModel(registration.AssignableTo.SyntaxTree).GetSymbolInfo(registration.AssignableTo).Symbol is INamedTypeSymbol assignableTo &&
                    !typeSymbol.Is(assignableTo))
                    continue;

                if (registration!.FullNameExpression != null && !registration.Regex.IsMatch(typeSymbol.ToFullName(compilation)))
                    continue;

                results.Add(new ServiceSymbol(typeSymbol, registration.Lifetime, null, registration.Location, null));
            }

            return results.ToImmutable();
        });

        // Flatten and remove duplicates
        var finalServices = attributedServices.Collect().Combine(conventionServices.Collect())
            .SelectMany((tuple, _) => ImmutableArray.CreateRange([tuple.Item1, tuple.Item2]))
            .SelectMany((items, _) => items.Distinct().ToImmutableArray());

        RegisterServicesOutput(context, finalServices, decorations, context.CompilationProvider);
        RegisterDecorateOutput(context, decorations, finalServices.Collect(), context.CompilationProvider);
    }

    void RegisterServicesOutput(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<ServiceSymbol> services,
        IncrementalValueProvider<ImmutableArray<DecoratedService>> decorations,
        IncrementalValueProvider<Compilation> compilation)
    {
        context.RegisterImplementationSourceOutput(
            services.Where(x => x!.Lifetime == 0 && x.Key is null).Select((x, _) => new KeyedService(x!.TImplementation, x!.TService, null)).Collect().Combine(decorations).Combine(compilation),
            (ctx, data) => AddPartial("AddSingleton", ctx, (data.Left.Left, data.Left.Right, data.Right)));

        context.RegisterImplementationSourceOutput(
            services.Where(x => x!.Lifetime == 1 && x.Key is null).Select((x, _) => new KeyedService(x!.TImplementation, x!.TService, null)).Collect().Combine(decorations).Combine(compilation),
            (ctx, data) => AddPartial("AddScoped", ctx, (data.Left.Left, data.Left.Right, data.Right)));

        context.RegisterImplementationSourceOutput(
            services.Where(x => x!.Lifetime == 2 && x.Key is null).Select((x, _) => new KeyedService(x!.TImplementation, x!.TService, null)).Collect().Combine(decorations).Combine(compilation),
            (ctx, data) => AddPartial("AddTransient", ctx, (data.Left.Left, data.Left.Right, data.Right)));

        context.RegisterImplementationSourceOutput(
            services.Where(x => x!.Lifetime == 0 && x.Key is not null).Select((x, _) => new KeyedService(x!.TImplementation, x!.TService, x.Key!)).Collect().Combine(decorations).Combine(compilation),
            (ctx, data) => AddPartial("AddKeyedSingleton", ctx, (data.Left.Left, data.Left.Right, data.Right)));

        context.RegisterImplementationSourceOutput(
            services.Where(x => x!.Lifetime == 1 && x.Key is not null).Select((x, _) => new KeyedService(x!.TImplementation, x!.TService, x.Key!)).Collect().Combine(decorations).Combine(compilation),
            (ctx, data) => AddPartial("AddKeyedScoped", ctx, (data.Left.Left, data.Left.Right, data.Right)));

        context.RegisterImplementationSourceOutput(
            services.Where(x => x!.Lifetime == 2 && x.Key is not null).Select((x, _) => new KeyedService(x!.TImplementation, x!.TService, x.Key!)).Collect().Combine(decorations).Combine(compilation),
            (ctx, data) => AddPartial("AddKeyedTransient", ctx, (data.Left.Left, data.Left.Right, data.Right)));

        context.RegisterImplementationSourceOutput(services.Collect(), ReportInconsistencies);
    }

    void RegisterDecorateOutput(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ImmutableArray<DecoratedService>> decorations,
        IncrementalValueProvider<ImmutableArray<ServiceSymbol>> services,
        IncrementalValueProvider<Compilation> compilation)
    {
        context.RegisterImplementationSourceOutput(
            decorations.Combine(services).Combine(compilation),
            (ctx, data) => AddDecoratePartial(ctx, data.Left.Left, data.Left.Right, data.Right));
    }

    void ReportInconsistencies(SourceProductionContext context, ImmutableArray<ServiceSymbol> array)
    {
        var grouped = array.GroupBy(x => x.TImplementation, SymbolEqualityComparer.Default).Where(g => g.Count() > 1).ToImmutableArray();
        if (grouped.Length == 0)
            return;

        foreach (var group in grouped)
        {
            // report if within the group, there are different lifetimes with the same key (or no key)
            foreach (var keyed in group.GroupBy(x => x.Key?.Value).Where(g => g.Count() > 1))
            {
                var lifetimes = keyed.Select(x => x.Lifetime).Distinct()
                    .Select(x => x switch { 0 => "Singleton", 1 => "Scoped", 2 => "Transient", _ => "Unknown" })
                    .ToArray();

                if (lifetimes.Length == 1)
                    continue;

                var location = keyed.Where(x => x.Location != null).FirstOrDefault()?.Location;
                var otherLocations = keyed.Where(x => x.Location != null).Skip(1).Select(x => x.Location!);

                context.ReportDiagnostic(Diagnostic.Create(AmbiguousLifetime,
                    location, otherLocations, keyed.First().TImplementation.ToDisplayString(), string.Join(", ", lifetimes)));
            }
        }
    }

    void AddDecoratePartial(
        SourceProductionContext ctx,
        ImmutableArray<DecoratedService> decorations,
        ImmutableArray<ServiceSymbol> services,
        Compilation compilation)
    {
        if (decorations.IsEmpty)
            return;

        var validDecorations = ImmutableArray.CreateBuilder<(DecoratedService Decoration, IMethodSymbol Constructor)>();

        foreach (var decoration in decorations)
        {
            if (!ValidateDecoration(ctx, decoration, services, compilation, out var constructor))
                continue;

            validDecorations.Add((decoration, constructor!));
        }

        if (validDecorations.Count == 0)
            return;

        var builder = new StringBuilder()
            .AppendLine("// <auto-generated />")
            .AppendLine("#nullable enable");

        foreach (var alias in compilation.References.SelectMany(r => r.Properties.Aliases))
        {
            builder.AppendLine($"extern alias {alias};");
        }

        builder.AppendLine(
          """
            using System;
            
            namespace Microsoft.Extensions.DependencyInjection
            {
                static partial class AddServicesNoReflectionExtension
                {
                    static partial void DecorateServices<TDecorated, TDecorator>(IServiceCollection services)
                        where TDecorated : class
                        where TDecorator : class, TDecorated
                    {
            """);

        for (var i = 0; i < validDecorations.Count; i++)
        {
            var (decoration, _) = validDecorations[i];
            if (decoration.IsKeyed)
                continue;

            var decorated = decoration.TDecorated.ToFullName(compilation);
            var decorator = decoration.TDecorator.ToFullName(compilation);

            builder.AppendLine($"            if (typeof(TDecorated) == typeof({decorated}) && typeof(TDecorator) == typeof({decorator}))");
            builder.AppendLine("            {");
            builder.AppendLine($"                DecorateDescriptors<{decorated}, {decorator}>(services, CreateDecorator{i});");
            builder.AppendLine("                return;");
            builder.AppendLine("            }");
        }

        builder.AppendLine(
          """
                    }

                    static partial void DecorateKeyedServices<TDecorated, TDecorator>(IServiceCollection services, object? key)
                        where TDecorated : class
                        where TDecorator : class, TDecorated
                    {
            """);

        for (var i = 0; i < validDecorations.Count; i++)
        {
            var (decoration, _) = validDecorations[i];
            if (!decoration.IsKeyed)
                continue;

            var decorated = decoration.TDecorated.ToFullName(compilation);
            var decorator = decoration.TDecorator.ToFullName(compilation);

            builder.AppendLine($"            if (typeof(TDecorated) == typeof({decorated}) && typeof(TDecorator) == typeof({decorator}))");
            builder.AppendLine("            {");
            builder.AppendLine($"                DecorateKeyedDescriptors<{decorated}, {decorator}>(services, key, CreateKeyedDecorator{i});");
            builder.AppendLine("                return;");
            builder.AppendLine("            }");
        }

        builder.AppendLine(
          """
                    }
            """);

        for (var i = 0; i < validDecorations.Count; i++)
        {
            var (decoration, ctor) = validDecorations[i];
            if (decoration.IsKeyed)
                continue;

            var decorated = decoration.TDecorated.ToFullName(compilation);
            var decorator = decoration.TDecorator.ToFullName(compilation);
            var usedDecorated = false;
            var args = string.Join(", ", ctor.Parameters.Select(p =>
            {
                if (!usedDecorated && SymbolEqualityComparer.Default.Equals(p.Type, decoration.TDecorated))
                {
                    usedDecorated = true;
                    return $"GetDecorated<{decorated}>(s, descriptor)";
                }

                var fromKeyed = p.GetAttributes().FirstOrDefault(IsFromKeyed);
                if (fromKeyed is not null)
                    return $"s.GetRequiredKeyedService<{p.Type.ToFullName(compilation)}>({fromKeyed.ConstructorArguments[0].ToCSharpString()})";

                return $"s.GetRequiredService<{p.Type.ToFullName(compilation)}>()";
            }));

            builder.AppendLine();
            builder.AppendLine($"        static {decorated} CreateDecorator{i}(IServiceProvider s, ServiceDescriptor descriptor)");
            builder.AppendLine($"            => new {decorator}({args});");
        }

        for (var i = 0; i < validDecorations.Count; i++)
        {
            var (decoration, ctor) = validDecorations[i];
            if (!decoration.IsKeyed)
                continue;

            var decorated = decoration.TDecorated.ToFullName(compilation);
            var decorator = decoration.TDecorator.ToFullName(compilation);
            var usedDecorated = false;
            var args = string.Join(", ", ctor.Parameters.Select(p =>
            {
                if (!usedDecorated && SymbolEqualityComparer.Default.Equals(p.Type, decoration.TDecorated))
                {
                    usedDecorated = true;
                    return $"GetKeyedDecorated<{decorated}>(s, key, descriptor)";
                }

                var fromKeyed = p.GetAttributes().FirstOrDefault(IsFromKeyed);
                if (fromKeyed is not null)
                    return $"s.GetRequiredKeyedService<{p.Type.ToFullName(compilation)}>({fromKeyed.ConstructorArguments[0].ToCSharpString()})";

                return $"s.GetRequiredService<{p.Type.ToFullName(compilation)}>()";
            }));

            builder.AppendLine();
            builder.AppendLine($"        static {decorated} CreateKeyedDecorator{i}(IServiceProvider s, object? key, ServiceDescriptor descriptor)");
            builder.AppendLine($"            => new {decorator}({args});");
        }

        builder.AppendLine(
        """
                }
            }
            """);

        ctx.AddSource("Decorate.g", builder.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine));
    }

    bool ValidateDecoration(
        SourceProductionContext ctx,
        DecoratedService decoration,
        ImmutableArray<ServiceSymbol> services,
        Compilation compilation,
        out IMethodSymbol? constructor)
    {
        constructor = GetDecoratorConstructor(decoration, compilation);
        var isValid = true;
        var decoratorLifetimes = GetDecoratorLifetimes(decoration, compilation);

        if (decoratorLifetimes.IsEmpty)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                DecoratorMustBeService,
                decoration.Location,
                decoration.TDecorator.ToDisplayString()));
            isValid = false;
        }

        var decoratedLifetimes = GetDecoratedLifetimes(decoration, services, compilation);
        if (!decoratorLifetimes.IsEmpty && !decoratedLifetimes.IsEmpty &&
            (decoratorLifetimes.Length != 1 || decoratedLifetimes.Any(x => x != decoratorLifetimes[0])))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                DecoratorLifetimeIncompatible,
                decoration.Location,
                decoration.TDecorator.ToDisplayString(),
                string.Join(", ", decoratorLifetimes.Select(LifetimeName)),
                decoration.TDecorated.ToDisplayString(),
                string.Join(", ", decoratedLifetimes.Select(LifetimeName))));
            isValid = false;
        }

        if (constructor is null)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                DecoratorConstructorMissing,
                decoration.Location,
                decoration.TDecorator.ToDisplayString(),
                decoration.TDecorated.ToDisplayString()));
            isValid = false;
        }

        return isValid;
    }

    static ImmutableArray<int> GetDecoratorLifetimes(DecoratedService decoration, Compilation compilation)
    {
        return GetServiceAttributes(decoration.TDecorator)
            .Where(x => IsMatchingDecoratorAttribute(decoration, x))
            .Where(x =>
                x.ServiceType is null ?
                    compilation.HasImplicitConversion(decoration.TDecorator, decoration.TDecorated) :
                    SymbolEqualityComparer.Default.Equals(x.ServiceType, decoration.TDecorated))
            .Select(x => x.Lifetime)
            .Distinct()
            .ToImmutableArray();
    }

    static ImmutableArray<int> GetDecoratedLifetimes(DecoratedService decoration, ImmutableArray<ServiceSymbol> services, Compilation compilation)
    {
        return services
            .Where(x => decoration.IsKeyed ? x.Key is not null : x.Key is null)
            .Where(x => !decoration.IsKeyed || !decoration.HasKeyValue || Equals(x.Key?.Value, decoration.KeyValue))
            .Where(x => !SymbolEqualityComparer.Default.Equals(x.TImplementation, decoration.TDecorator))
            .Where(x =>
                x.TService is null ?
                    compilation.HasImplicitConversion(x.TImplementation, decoration.TDecorated) :
                    SymbolEqualityComparer.Default.Equals(x.TService, decoration.TDecorated))
            .Select(x => x.Lifetime)
            .Distinct()
            .ToImmutableArray();
    }

    static bool IsMatchingDecoratorAttribute(DecoratedService decoration, ServiceAttributeInfo attribute) =>
        decoration.IsKeyed || attribute.Key is null;

    IMethodSymbol? GetDecoratorConstructor(DecoratedService decoration, Compilation compilation)
    {
        var candidates = decoration.TDecorator.InstanceConstructors
            .Where(x => compilation.IsSymbolAccessible(x))
            .Where(x => x.Parameters.Count(p => SymbolEqualityComparer.Default.Equals(p.Type, decoration.TDecorated)) == 1)
            .ToImmutableArray();

        if (candidates.IsDefaultOrEmpty)
            return null;

        return candidates.FirstOrDefault(HasImportingConstructor) ??
            candidates.OrderByDescending(m => m.Parameters.Length).FirstOrDefault();
    }

    static bool HasImportingConstructor(IMethodSymbol method) =>
        method.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Composition.ImportingConstructorAttribute" ||
            a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.ComponentModel.Composition.ImportingConstructorAttribute");

    static bool IsDecoratorServiceAlias(INamedTypeSymbol implementation, INamedTypeSymbol service, bool keyed, ImmutableArray<DecoratedService> decorations) =>
        decorations.Any(x =>
            x.IsKeyed == keyed &&
            SymbolEqualityComparer.Default.Equals(x.TDecorator, implementation) &&
            SymbolEqualityComparer.Default.Equals(x.TDecorated, service));

    static ImmutableArray<ServiceAttributeInfo> GetServiceAttributes(INamedTypeSymbol type)
    {
        var builder = ImmutableArray.CreateBuilder<ServiceAttributeInfo>();

        foreach (var attr in type.GetAttributes())
        {
            if (!IsServiceAttribute(attr) && !IsKeyedServiceAttribute(attr))
                continue;

            var lifetime = IsKeyedServiceAttribute(attr) ?
                (int)attr.ConstructorArguments[1].Value! :
                (int)attr.ConstructorArguments[0].Value!;
            var key = IsKeyedServiceAttribute(attr) ? attr.ConstructorArguments[0] : (TypedConstant?)null;
            var serviceType = attr.AttributeClass?.IsGenericType == true &&
                attr.AttributeClass.TypeArguments.Length == 1 &&
                attr.AttributeClass.TypeArguments[0] is INamedTypeSymbol namedService ?
                namedService :
                null;

            builder.Add(new ServiceAttributeInfo(
                lifetime,
                key,
                serviceType,
                attr.ApplicationSyntaxReference?.GetSyntax().GetLocation()));
        }

        return builder.ToImmutable();
    }

    static bool IsServiceAttribute(AttributeData attr) =>
        (attr.AttributeClass?.Name == "ServiceAttribute" || attr.AttributeClass?.Name == "Service") &&
        attr.ConstructorArguments.Length == 1 &&
        attr.ConstructorArguments[0].Kind == TypedConstantKind.Enum &&
        attr.ConstructorArguments[0].Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Microsoft.Extensions.DependencyInjection.ServiceLifetime";

    static bool IsKeyedServiceAttribute(AttributeData attr) =>
        (attr.AttributeClass?.Name == "ServiceAttribute" || attr.AttributeClass?.Name == "Service" ||
         attr.AttributeClass?.Name == "KeyedService" || attr.AttributeClass?.Name == "KeyedServiceAttribute") &&
        attr.ConstructorArguments.Length == 2 &&
        attr.ConstructorArguments[1].Kind == TypedConstantKind.Enum &&
        attr.ConstructorArguments[1].Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Microsoft.Extensions.DependencyInjection.ServiceLifetime";

    static bool IsExportAttribute(AttributeData attr)
    {
        var attrName = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return attrName == "global::System.Composition.ExportAttribute" ||
            attrName == "global::System.ComponentModel.Composition.ExportAttribute";
    }

    static string LifetimeName(int lifetime) =>
        lifetime switch { 0 => "Singleton", 1 => "Scoped", 2 => "Transient", _ => "Unknown" };

    static string? GetInvokedMethodName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
        IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
        _ => null
    };

    static ServiceRegistration? GetServiceRegistration(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        // This is somewhat expensive, so we try to first discard invocations that don't look like our 
        // target first (no args and wrong method name), in the predicate, before moving on to semantic analyis here.

        var options = (CSharpParseOptions)invocation.SyntaxTree.Options;
        var compilation = semanticModel.Compilation;
        var model = compilation.GetSemanticModel(invocation.SyntaxTree);

        var symbolInfo = model.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is IMethodSymbol methodSymbol &&
            methodSymbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == "DDIAddServicesAttribute") &&
            methodSymbol.Parameters.Length >= 2)
        {
            var defaultLifetime = methodSymbol.Parameters.FirstOrDefault(x => x.Type.Name == "ServiceLifetime" && x.HasExplicitDefaultValue)?.ExplicitDefaultValue;
            // This allows us to change the API-provided default without having to change the source generator to match, if needed.
            var lifetime = defaultLifetime is int value ? value : 0;
            TypeSyntax? assignableTo = null;
            string? fullNameExpression = null;

            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                var typeInfo = model.GetTypeInfo(argument.Expression).Type;

                if (typeInfo is INamedTypeSymbol namedType)
                {
                    if (namedType.Name == "ServiceLifetime")
                    {
                        lifetime = (int?)model.GetConstantValue(argument.Expression).Value ?? 0;
                    }
                    else if (namedType.Name == "Type" && argument.Expression is TypeOfExpressionSyntax typeOf &&
                        model.GetSymbolInfo(typeOf.Type).Symbol is INamedTypeSymbol typeSymbol)
                    {
                        // TODO: analyzer error if argument is not typeof(T)
                        assignableTo = typeOf.Type;
                    }
                    else if (namedType.SpecialType == SpecialType.System_String)
                    {
                        fullNameExpression = model.GetConstantValue(argument.Expression).Value as string;
                    }
                }
            }

            if (assignableTo != null || fullNameExpression != null)
            {
                return new ServiceRegistration(lifetime, assignableTo, fullNameExpression, invocation.GetLocation());
            }
        }
        return null;
    }

    static DecoratedService? GetDecoration(InvocationExpressionSyntax invocation, SemanticModel semanticModel, CancellationToken cancellation)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return null;

        if (!HasMethodAttribute(methodSymbol, "DDIDecorateAttribute"))
            return null;

        if (methodSymbol.TypeArguments.Length != 2 ||
            methodSymbol.TypeArguments[0] is not INamedTypeSymbol decorated ||
            methodSymbol.TypeArguments[1] is not INamedTypeSymbol decorator)
            return null;

        var isKeyed = methodSymbol.Parameters.Any(p => p.Name == "key") ||
            methodSymbol.ReducedFrom?.Parameters.Any(p => p.Name == "key") == true;
        var hasKeyValue = false;
        object? keyValue = null;

        if (isKeyed && invocation.ArgumentList.Arguments.Count > 0)
        {
            var key = semanticModel.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression, cancellation);
            hasKeyValue = key.HasValue;
            keyValue = key.Value;
        }

        return new DecoratedService(decorated, decorator, isKeyed, hasKeyValue, keyValue, invocation.GetLocation());
    }

    static bool HasMethodAttribute(IMethodSymbol method, string attributeName) =>
        method.GetAttributes().Any(attr => attr.AttributeClass?.Name == attributeName) ||
        method.ReducedFrom?.GetAttributes().Any(attr => attr.AttributeClass?.Name == attributeName) == true;

    void AddPartial(string methodName, SourceProductionContext ctx, (ImmutableArray<KeyedService> Types, ImmutableArray<DecoratedService> Decorations, Compilation Compilation) data)
    {
        if (data.Types.IsEmpty)
            return;

        var builder = new StringBuilder()
            .AppendLine("// <auto-generated />");

        foreach (var alias in data.Compilation.References.SelectMany(r => r.Properties.Aliases))
        {
            builder.AppendLine($"extern alias {alias};");
        }

        builder.AppendLine(
          $$"""
            using Microsoft.Extensions.DependencyInjection.Extensions;
            using System;
            
            namespace Microsoft.Extensions.DependencyInjection
            {
                static partial class AddServicesNoReflectionExtension
                {
                    static partial void {{methodName}}Services(IServiceCollection services)
                    {
            """);

        AddServices(data.Types.Where(x => x.Key is null), data.Decorations, data.Compilation, methodName, builder);
        AddKeyedServices(data.Types.Where(x => x.Key is not null), data.Decorations, data.Compilation, methodName, builder);

        builder.AppendLine(
        """
                    }
                }
            }
            """);

        ctx.AddSource(methodName + ".g", builder.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine));
    }

    void AddServices(IEnumerable<KeyedService> services, ImmutableArray<DecoratedService> decorations, Compilation compilation, string methodName, StringBuilder output)
    {
        bool isAccessible(ISymbol s) => compilation.IsSymbolAccessible(s);

        foreach (var info in services)
        {
            var type = info.TImplementation;
            var impl = type.ToFullName(compilation);
            var registered = new HashSet<string>();

            var importing = type.InstanceConstructors.FirstOrDefault(m =>
                m.GetAttributes().Any(a =>
                    a.AttributeClass?.ToFullName(compilation) == "global::System.Composition.ImportingConstructorAttribute" ||
                    a.AttributeClass?.ToFullName(compilation) == "global::System.ComponentModel.Composition.ImportingConstructorAttribute"));

            var ctor = importing ?? type.InstanceConstructors
                .Where(isAccessible)
                .OrderByDescending(m => m.Parameters.Length)
                .FirstOrDefault();

            if (ctor != null && ctor.Parameters.Length > 0)
            {
                var args = string.Join(", ", ctor.Parameters.Select(p =>
                {
                    var fromKeyed = p.GetAttributes().FirstOrDefault(IsFromKeyed);
                    if (fromKeyed is not null)
                        return $"s.GetRequiredKeyedService<{p.Type.ToFullName(compilation)}>({fromKeyed.ConstructorArguments[0].ToCSharpString()})";

                    return $"s.GetRequiredService<{p.Type.ToFullName(compilation)}>()";
                }));
                output.AppendLine($"            services.Try{methodName}(s => new {impl}({args}));");
            }
            else
            {
                output.AppendLine($"            services.Try{methodName}(s => new {impl}());");
            }

            output.AppendLine($"            services.AddTransient<Func<{impl}>>(s => s.GetRequiredService<{impl}>);");
            output.AppendLine($"            services.AddTransient(s => new Lazy<{impl}>(s.GetRequiredService<{impl}>));");

            var serviceTypes = info.TService != null ? ImmutableArray.Create(info.TService) : type.AllInterfaces;

            foreach (var iface in serviceTypes)
            {
                if (IsDecoratorServiceAlias(type, iface, keyed: false, decorations))
                    continue;

                if (!compilation.HasImplicitConversion(type, iface))
                    continue;

                var ifaceName = iface.ToFullName(compilation);
                if (!registered.Contains(ifaceName))
                {
                    output.AppendLine($"            services.{methodName}<{ifaceName}>(s => s.GetRequiredService<{impl}>());");
                    output.AppendLine($"            services.AddTransient<Func<{ifaceName}>>(s => s.GetRequiredService<{ifaceName}>);");
                    output.AppendLine($"            services.AddTransient(s => new Lazy<{ifaceName}>(s.GetRequiredService<{ifaceName}>));");
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

                    foreach (var candidate in candidates
                        .Where(x => x.SatisfiesConstraints(iface.TypeParameters[0]))
                        .Select(x => iface.ConstructedFrom.Construct(x))
                        .Where(x => x != null && compilation.HasImplicitConversion(type, x))
                        .ToImmutableHashSet(SymbolEqualityComparer.Default)
                        .Select(x => x!.ToFullName(compilation)))
                    {
                        if (!registered.Contains(candidate))
                        {
                            output.AppendLine($"            services.{methodName}<{candidate}>(s => s.GetRequiredService<{impl}>());");
                            output.AppendLine($"            services.AddTransient<Func<{candidate}>>(s => s.GetRequiredService<{candidate}>);");
                            output.AppendLine($"            services.AddTransient(s => new Lazy<{candidate}>(s.GetRequiredService<{candidate}>));");
                            registered.Add(candidate);
                        }
                    }
                }
            }
        }
    }

    void AddKeyedServices(IEnumerable<KeyedService> services, ImmutableArray<DecoratedService> decorations, Compilation compilation, string methodName, StringBuilder output)
    {
        bool isAccessible(ISymbol s) => compilation.IsSymbolAccessible(s);

        foreach (var info in services)
        {
            var type = info.TImplementation;
            var impl = type.ToFullName(compilation);
            var registered = new HashSet<string>();
            var key = info.Key!.Value.ToCSharpString();

            var importing = type.InstanceConstructors.FirstOrDefault(m =>
                m.GetAttributes().Any(a =>
                    a.AttributeClass?.ToFullName(compilation) == "global::System.Composition.ImportingConstructorAttribute" ||
                    a.AttributeClass?.ToFullName(compilation) == "global::System.ComponentModel.Composition.ImportingConstructorAttribute"));

            var ctor = importing ?? type.InstanceConstructors
                .Where(isAccessible)
                .OrderByDescending(m => m.Parameters.Length)
                .FirstOrDefault();

            if (ctor != null && ctor.Parameters.Length > 0)
            {
                var args = string.Join(", ", ctor.Parameters.Select(p =>
                {
                    var fromKeyed = p.GetAttributes().FirstOrDefault(IsFromKeyed);
                    if (fromKeyed is not null)
                        return $"s.GetRequiredKeyedService<{p.Type.ToFullName(compilation)}>({fromKeyed.ConstructorArguments[0].ToCSharpString()})";

                    return $"s.GetRequiredService<{p.Type.ToFullName(compilation)}>()";
                }));
                output.AppendLine($"            services.{methodName}({key}, (s, k) => new {impl}({args}));");
            }
            else
            {
                output.AppendLine($"            services.{methodName}({key}, (s, k) => new {impl}());");
            }

            output.AppendLine($"            services.AddKeyedTransient<Func<{impl}>>({key}, (s, k) => () => s.GetRequiredKeyedService<{impl}>(k));");
            output.AppendLine($"            services.AddKeyedTransient({key}, (s, k) => new Lazy<{impl}>(() => s.GetRequiredKeyedService<{impl}>(k)));");

            var serviceTypes = info.TService != null ? ImmutableArray.Create(info.TService) : info.TImplementation.AllInterfaces;

            foreach (var iface in serviceTypes)
            {
                if (IsDecoratorServiceAlias(type, iface, keyed: true, decorations))
                    continue;

                var ifaceName = iface.ToFullName(compilation);
                if (!registered.Contains(ifaceName))
                {
                    output.AppendLine($"            services.{methodName}<{ifaceName}>({key}, (s, k) => s.GetRequiredKeyedService<{impl}>(k));");
                    output.AppendLine($"            services.AddKeyedTransient<Func<{ifaceName}>>({key}, (s, k) => () => s.GetRequiredKeyedService<{ifaceName}>(k));");
                    output.AppendLine($"            services.AddKeyedTransient({key}, (s, k) => new Lazy<{ifaceName}>(() => s.GetRequiredKeyedService<{ifaceName}>(k)));");
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
                        .Select(x => x!.ToFullName(compilation)))
                    {
                        if (!registered.Contains(candidate))
                        {
                            output.AppendLine($"            services.{methodName}<{candidate}>({key}, (s, k) => s.GetRequiredKeyedService<{impl}>(k));");
                            output.AppendLine($"            services.AddKeyedTransient<Func<{candidate}>>({key}, (s, k) => () => s.GetRequiredKeyedService<{candidate}>(k));");
                            output.AppendLine($"            services.AddKeyedTransient({key}, (s, k) => new Lazy<{candidate}>(() => s.GetRequiredKeyedService<{candidate}>(k)));");
                            registered.Add(candidate);
                        }
                    }
                }
            }
        }
    }

    bool IsFromKeyed(AttributeData attr)
    {
        var attrName = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return attrName == "global::Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute" ||
            (attrName == "global::System.ComponentModel.Composition.ImportAttribute" &&
             // In this case, the Import attribute ctor can only have a primitive string value, not enum.
             attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Kind == TypedConstantKind.Primitive);
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

        public override void VisitAlias(IAliasSymbol symbol)
        {
            base.VisitAlias(symbol);
        }

        public override void VisitModule(IModuleSymbol symbol)
            => base.VisitModule(symbol);

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
