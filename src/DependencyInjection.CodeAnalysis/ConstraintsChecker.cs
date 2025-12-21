using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Devlooped.Extensions.DependencyInjection;

static class ConstraintsChecker
{
    public static bool SatisfiesConstraints(this ITypeSymbol typeArgument, ITypeParameterSymbol typeParameter)
    {
        // Check reference type constraint
        if (typeParameter.HasReferenceTypeConstraint && !typeArgument.IsReferenceType)
            return false;

        // Check value type constraint
        if (typeParameter.HasValueTypeConstraint && !typeArgument.IsValueType)
            return false;

        // Check base class and interface constraints
        foreach (var constraint in typeParameter.ConstraintTypes)
        {
            if (constraint.TypeKind == TypeKind.Class)
            {
                if (!typeArgument.GetBaseTypes().Any(baseType => SymbolEqualityComparer.Default.Equals(baseType, constraint)))
                    return false;
            }
            else if (constraint.TypeKind == TypeKind.Interface)
            {
                if (!typeArgument.AllInterfaces.Any(interfaceSymbol => SymbolEqualityComparer.Default.Equals(interfaceSymbol, constraint)))
                    return false;
            }
        }

        // Constructor constraint (optional, not typically needed here)
        if (typeParameter.HasConstructorConstraint)
        {
            // Check for parameterless constructor (simplified)
            var hasParameterlessConstructor = typeArgument.GetMembers(".ctor")
                .OfType<IMethodSymbol>()
                .Any(ctor => ctor.Parameters.Length == 0);
            if (!hasParameterlessConstructor)
                return false;
        }

        return true;
    }

    static IEnumerable<ITypeSymbol> GetBaseTypes(this ITypeSymbol typeSymbol)
    {
        var currentType = typeSymbol.BaseType;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            yield return currentType;
            currentType = currentType.BaseType;
        }
    }
}
