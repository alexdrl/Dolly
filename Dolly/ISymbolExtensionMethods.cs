using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Dolly;

public static class ISymbolExtensionMethods
{
    public static string GetNamespace(this ISymbol symbol) => symbol.ContainingNamespace.ToCodeString();

    public static bool HasAttribute(this ISymbol symbol, string name, string @namespace = "Dolly") =>
        symbol.GetAttributes().Any(a => a.AttributeClass != null && a.AttributeClass.GetNamespace() == @namespace && a.AttributeClass.Name == name);

    public static bool TryGetIEnumerableType(this ISymbol symbol, out ITypeSymbol enumerableType)
    {
        if (symbol.TryGetGenericIEnumerable(out var @interface))
        {
            enumerableType = @interface.TypeArguments[0];
            return true;
        }
        enumerableType = null!;
        return false;
    }

    public static bool TryGetGenericIEnumerable(this ISymbol symbol, out INamedTypeSymbol @interface)
    {
        if (symbol is INamedTypeSymbol selfNamedSymbol && selfNamedSymbol.IsGenericIEnumerable())
        {
            @interface = selfNamedSymbol;
            return true;
        }

        @interface = null!;
        if (symbol is INamedTypeSymbol namedSymbol)
        {
            @interface = namedSymbol.AllInterfaces.SingleOrDefault(@interface => @interface.IsGenericIEnumerable())!;
            return @interface != null;
        }
        return false;
    }

    public static bool IsClonable(this ITypeSymbol typeSymbol, string assemblyName) =>
        typeSymbol.HasAttribute("ClonableAttribute", $"{assemblyName}.Dolly") || typeSymbol.AllInterfaces.Any(i => i.Name == "IClonable");

    public static string GetFullName(this ISymbol symbol)
    {
        if (symbol is INamedTypeSymbol namedSymbol)
        {
            return $"{symbol.GetNamespace()}.{symbol.Name}{(namedSymbol.IsGenericType ? $"<{string.Join(", ", namedSymbol.TypeArguments.Select(ta => ta.GetFullName()))}>" : "")}";

        }
        return $"{symbol.GetNamespace()}.{symbol.Name}";
    }

    public static bool IsGenericIEnumerable(this ISymbol symbol) =>
        symbol is INamedTypeSymbol namedSymbol && namedSymbol.IsGenericType && namedSymbol.ConstructedFrom.IsGenericIEnumerableDefinition();

    public static bool IsGenericIEnumerableDefinition(this ISymbol symbol) =>
        symbol is INamedTypeSymbol namedSymbol &&
        namedSymbol.GetNamespace() == "System.Collections.Generic" &&
        namedSymbol.Name == "IEnumerable" &&
        namedSymbol.TypeArguments.Length == 1;

    public static bool IsNullable(this ITypeSymbol symbol, bool nullabilityEnabled, [NotNullWhen(true)] out ITypeSymbol innerType)
    {
        innerType = symbol;
        if (nullabilityEnabled && symbol.IsNullableValueType(out var valueInnerType))
        {
            innerType = valueInnerType;
            return true;
        }

        return (!nullabilityEnabled && symbol.IsReferenceType) ||
            (nullabilityEnabled && symbol.IsReferenceType && symbol.NullableAnnotation == NullableAnnotation.Annotated);
    }

    public static bool IsNullableValueType(this ISymbol symbol, [NotNullWhen(true)] out ITypeSymbol? innerType)
    {
        if (symbol is INamedTypeSymbol namedSymbol &&
        namedSymbol.IsValueType &&
        namedSymbol.GetNamespace() == "System" &&
        namedSymbol.Name == "Nullable" &&
        namedSymbol.TypeArguments.Length == 1)
        {
            innerType = namedSymbol.TypeArguments[0];
            return true;
        }
        innerType = null;
        return false;
    }

}

