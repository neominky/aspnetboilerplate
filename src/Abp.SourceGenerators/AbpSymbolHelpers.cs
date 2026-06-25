using System.Linq;
using Microsoft.CodeAnalysis;

namespace Abp.SourceGenerators;

internal static class AbpSymbolHelpers
{
    public static bool HasAttributeByName(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().Any(a => a.AttributeClass?.Name == attributeName);
    }

    public static AttributeData? FindAttributeByName(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == attributeName);
    }

    public static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
    {
        return symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));
    }
}
