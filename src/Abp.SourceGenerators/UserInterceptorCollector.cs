using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Abp.SourceGenerators;

internal static class UserInterceptorCollector
{
    private static readonly string[] BuiltInInterceptorMetadataNames =
    {
        "Abp.Runtime.Validation.Interception.ValidationInterceptor",
        "Abp.Auditing.AuditingInterceptor",
        "Abp.EntityHistory.EntityHistoryInterceptor",
        "Abp.Domain.Uow.UnitOfWorkInterceptor",
        "Abp.Authorization.AuthorizationInterceptor"
    };

    public static ImmutableArray<BakedInterceptorField> Collect(Compilation compilation)
    {
        var interceptorBase = compilation.GetTypeByMetadataName("Abp.Dependency.AbpInterceptorBase");
        var transientDependency = compilation.GetTypeByMetadataName("Abp.Dependency.ITransientDependency");
        if (interceptorBase == null || transientDependency == null)
        {
            return ImmutableArray<BakedInterceptorField>.Empty;
        }

        var builtInInterceptors = BuiltInInterceptorMetadataNames
            .Select(compilation.GetTypeByMetadataName)
            .Where(type => type != null)
            .ToArray();

        var fields = new List<BakedInterceptorField>();
        var seenFieldNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in compilation.Assembly.GlobalNamespace.GetAllTypes())
        {
            if (type.TypeKind != TypeKind.Class || type.IsAbstract)
            {
                continue;
            }

            if (!InheritsFrom(type, interceptorBase))
            {
                continue;
            }

            if (!Implements(type, transientDependency))
            {
                continue;
            }

            if (builtInInterceptors.Any(builtIn => SymbolEqualityComparer.Default.Equals(type, builtIn)))
            {
                continue;
            }

            var fieldName = ToFieldName(type);
            if (!seenFieldNames.Add(fieldName))
            {
                continue;
            }

            var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            fields.Add(new BakedInterceptorField(
                fieldName,
                $"(global::Abp.Dependency.AbpInterceptorBase)iocResolver.Resolve(typeof({typeName}))",
                typeName));
        }

        return fields.ToImmutableArray();
    }

    private static string ToFieldName(INamedTypeSymbol type)
    {
        var name = type.Name;
        return "_" + char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol interfaceType)
    {
        return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, interfaceType))
               || type.Interfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, interfaceType));
    }
}
