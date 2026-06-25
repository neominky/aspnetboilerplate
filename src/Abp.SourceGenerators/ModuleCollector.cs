using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Abp.SourceGenerators;

internal sealed class ModuleModel
{
    public INamedTypeSymbol ModuleType { get; set; } = null!;
    public string ModuleTypeName { get; set; } = "";
    public string Namespace { get; set; } = "";
}

internal static class ModuleCollector
{
    public static ImmutableArray<ModuleModel> Collect(Compilation compilation)
    {
        var abp = AbpCompilationSymbols.Resolve(compilation);
        if (abp.AbpModule == null)
        {
            return ImmutableArray<ModuleModel>.Empty;
        }

        var modules = new List<ModuleModel>();

        foreach (var symbol in compilation.Assembly.GlobalNamespace.GetAllTypes())
        {
            if (symbol.TypeKind != TypeKind.Class || symbol.IsAbstract || symbol.IsStatic)
            {
                continue;
            }

            if (!InheritsFrom(symbol, abp.AbpModule))
            {
                continue;
            }

            if (symbol.Name.EndsWith(AbpTypeNames.GeneratedTypeSuffixes.CompileTimeModule))
            {
                continue;
            }

            modules.Add(new ModuleModel
            {
                ModuleType = symbol,
                ModuleTypeName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Namespace = symbol.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : symbol.ContainingNamespace.ToDisplayString()
            });
        }

        return modules.ToImmutableArray();
    }

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
