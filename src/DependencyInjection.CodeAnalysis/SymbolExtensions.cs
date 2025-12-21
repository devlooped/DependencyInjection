using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.CodeAnalysis;

static class SymbolExtensions
{
    static readonly SymbolDisplayFormat fullNameFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable);

    static readonly SymbolDisplayFormat nonGenericFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable);

    public static bool IsSymbolAccessible(this Compilation compilation, ISymbol symbol)
    {
        try
        {
            // Unfortunately, wrong assembly symbol throws instead of returning false :(
            // See https://github.com/dotnet/roslyn/blob/9fef96ff1194f686993293c7971802216eb75a26/src/Compilers/Core/Portable/Compilation/Compilation.cs#L1626
            return compilation.IsSymbolAccessibleWithin(symbol, compilation.Assembly);
        }
        catch
        {
            return false;
        }
    }

    public static string ToAssemblyNamespace(this INamespaceSymbol symbol)
        => symbol.ContainingAssembly.Name + "." + symbol.ToDisplayString(fullNameFormat);

    public static string ToFullName(this ISymbol symbol, Compilation compilation)
    {
        var fullName = symbol.ToDisplayString(nonGenericFormat);

        if (symbol is INamedTypeSymbol named && named.IsGenericType)
        {
            // Need to do ToFullName for each generic parameter.
            var genericArguments = named.TypeArguments.Select(t => t.ToFullName(compilation));
            fullName = GenericName(fullName).WithTypeArgumentList(
                    TypeArgumentList(SeparatedList<TypeSyntax>(genericArguments.Select(IdentifierName))))
                .ToString();
        }

        if (compilation.GetMetadataReference(symbol.ContainingAssembly) is MetadataReference reference &&
            !reference.Properties.Aliases.IsDefaultOrEmpty)
            return reference.Properties.Aliases.First() + "::" + fullName;

        return "global::" + fullName;
    }

    public static string ToFullName(this ISymbol symbol, NameSyntax name, CancellationToken cancellation = default)
    {
        var fullName = symbol.ToDisplayString(fullNameFormat);
        var root = name.SyntaxTree.GetRoot(cancellation);
        var aliases = root.ChildNodes().OfType<ExternAliasDirectiveSyntax>().Select(x => x.Identifier.Text).ToList();

        var candidate = name;
        while (candidate is QualifiedNameSyntax qualified)
            candidate = qualified.Left;

        if (candidate is IdentifierNameSyntax identifier &&
            aliases.FirstOrDefault(x => x == identifier.Identifier.Text) is string alias)
            return alias + ":" + fullName;

        return fullName;
    }

    /// <summary>
    /// Checks whether the <paramref name="this"/> type inherits or implements the 
    /// <paramref name="baseTypeOrInterface"/> type, even if it's a generic type.
    /// </summary>
    public static bool Is(this ITypeSymbol? @this, ITypeSymbol? baseTypeOrInterface)
    {
        if (@this == null || baseTypeOrInterface == null)
            return false;

        if (@this.Equals(baseTypeOrInterface, SymbolEqualityComparer.Default) == true)
            return true;

        if (baseTypeOrInterface is INamedTypeSymbol namedExpected &&
            @this is INamedTypeSymbol namedActual &&
            namedActual.IsGenericType &&
            (namedActual.ConstructedFrom.Equals(namedExpected, SymbolEqualityComparer.Default) ||
            namedActual.ConstructedFrom.Equals(namedExpected.OriginalDefinition, SymbolEqualityComparer.Default)))
            return true;

        foreach (var iface in @this.AllInterfaces)
            if (iface.Is(baseTypeOrInterface))
                return true;

        if (@this.BaseType?.Name.Equals("object", StringComparison.OrdinalIgnoreCase) == true)
            return false;

        return Is(@this.BaseType, baseTypeOrInterface);
    }
}
