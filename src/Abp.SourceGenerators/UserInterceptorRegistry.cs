using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Abp.SourceGenerators;

internal sealed class UserInterceptorRegistry
{
    private readonly ImmutableArray<UserInterceptorInfo> _interceptors;
    private readonly Dictionary<string, ImmutableArray<UserInterceptorInfo>> _triggerIndex;

    private UserInterceptorRegistry(
        ImmutableArray<UserInterceptorInfo> interceptors,
        Dictionary<string, ImmutableArray<UserInterceptorInfo>> triggerIndex)
    {
        _interceptors = interceptors;
        _triggerIndex = triggerIndex;
    }

    public ImmutableArray<BakedInterceptorField> AllInterceptors
        => _interceptors.Select(i => i.Field).ToImmutableArray();

    public static UserInterceptorRegistry Empty { get; } = new(
        ImmutableArray<UserInterceptorInfo>.Empty,
        new Dictionary<string, ImmutableArray<UserInterceptorInfo>>(StringComparer.Ordinal));

    public static UserInterceptorRegistry Collect(Compilation compilation)
        => Collect(compilation, reportDiagnostic: null);

    public static UserInterceptorRegistry Collect(Compilation compilation, System.Action<Diagnostic>? reportDiagnostic)
    {
        var abp = AbpCompilationSymbols.Resolve(compilation);
        if (abp.InterceptorBase == null || !abp.HasTransientDependency)
        {
            return Empty;
        }

        var builtInInterceptors = abp.ResolveBuiltInInterceptors(compilation);
        var allocationFreeBase = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.AbpInterceptorBaseAllocationFree);

        var interceptors = new List<UserInterceptorInfo>();
        var seenFieldNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in compilation.Assembly.GlobalNamespace.GetAllTypes())
        {
            if (type.TypeKind != TypeKind.Class || type.IsAbstract)
            {
                continue;
            }

            if (!InheritsFrom(type, abp.InterceptorBase))
            {
                continue;
            }

            if (!Implements(type, abp.TransientDependency!))
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
            var valueTaskLayerKind = AllocationFreeInterceptorAnalyzer.ResolveValueTaskLayerKind(
                type,
                allocationFreeBase,
                compilation);
            var taskLayerKind = AllocationFreeInterceptorAnalyzer.ResolveTaskLayerKind(
                type,
                allocationFreeBase,
                compilation);
            var syncLayerKind = AllocationFreeInterceptorAnalyzer.ResolveSyncLayerKind(
                type,
                allocationFreeBase,
                compilation);
            var field = new BakedInterceptorField(
                fieldName,
                $"({AbpTypeNames.FullyQualified.AbpInterceptorBase})iocResolver.Resolve(typeof({typeName}))",
                typeName,
                valueTaskLayerKind,
                taskLayerKind,
                syncLayerKind);

            var abpInterceptorAttributes = type.GetAttributes()
                .Where(a => a.AttributeClass?.Name == AbpTypeNames.Short.Attributes.AbpInterceptor)
                .ToList();

            if (abpInterceptorAttributes.Count == 0)
            {
                reportDiagnostic?.Invoke(Diagnostic.Create(
                    CompileTimeDiagnostics.MissingInterceptorTriggerAttribute,
                    type.Locations.FirstOrDefault() ?? Location.None,
                    type.Name));
                continue;
            }

            var triggerAttributeNames = abpInterceptorAttributes
                .Select(GetTriggerAttributeName)
                .Where(name => name != null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToImmutableArray();

            if (triggerAttributeNames.IsEmpty)
            {
                reportDiagnostic?.Invoke(Diagnostic.Create(
                    CompileTimeDiagnostics.MissingInterceptorTriggerAttribute,
                    abpInterceptorAttributes[0].ApplicationSyntaxReference?.GetSyntax().GetLocation()
                    ?? type.Locations.FirstOrDefault()
                    ?? Location.None,
                    type.Name));
                continue;
            }

            interceptors.Add(new UserInterceptorInfo(field, typeName, triggerAttributeNames));
        }

        var triggerIndex = interceptors
            .SelectMany(info => info.TriggerAttributeNames.Select(name => (name, info)))
            .GroupBy(pair => pair.name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(pair => pair.info).Distinct().ToImmutableArray(),
                StringComparer.Ordinal);

        return new UserInterceptorRegistry(interceptors.ToImmutableArray(), triggerIndex);
    }

    public bool TypeHasInterceptorBinding(INamedTypeSymbol type)
    {
        if (HasAbpInterceptAttribute(type))
        {
            return true;
        }

        return type.GetAttributes()
            .Select(a => a.AttributeClass?.Name)
            .Any(name => name != null && _triggerIndex.ContainsKey(name));
    }

    public bool MethodHasInterceptorBinding(INamedTypeSymbol type, IMethodSymbol method)
    {
        if (HasAbpInterceptAttribute(method) || HasAbpInterceptAttribute(type))
        {
            return true;
        }

        foreach (var attribute in method.GetAttributes().Concat(type.GetAttributes()))
        {
            var name = attribute.AttributeClass?.Name;
            if (name != null && _triggerIndex.ContainsKey(name))
            {
                return true;
            }
        }

        return false;
    }

    public ImmutableArray<BakedInterceptorField> GetMatchingInterceptors(INamedTypeSymbol type, IMethodSymbol method)
    {
        var matches = new List<BakedInterceptorField>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var attribute in method.GetAttributes().Concat(type.GetAttributes()))
        {
            var attributeName = attribute.AttributeClass?.Name;
            if (attributeName == null || !_triggerIndex.TryGetValue(attributeName, out var triggered))
            {
                continue;
            }

            foreach (var info in triggered)
            {
                if (seen.Add(info.Field.FieldName))
                {
                    matches.Add(info.Field);
                }
            }
        }

        foreach (var attribute in method.GetAttributes().Concat(type.GetAttributes()))
        {
            if (attribute.AttributeClass?.Name != AbpTypeNames.Short.Attributes.AbpIntercept)
            {
                continue;
            }

            var interceptorType = attribute.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
            if (interceptorType == null)
            {
                continue;
            }

            var info = _interceptors.FirstOrDefault(i =>
                string.Equals(i.InterceptorTypeName, interceptorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparison.Ordinal));
            if (info != null && seen.Add(info.Field.FieldName))
            {
                matches.Add(info.Field);
            }
        }

        return matches.ToImmutableArray();
    }

    private bool HasAbpInterceptAttribute(ISymbol symbol)
    {
        return AbpSymbolHelpers.HasAttributeByName(symbol, AbpTypeNames.Short.Attributes.AbpIntercept);
    }

    private static string? GetTriggerAttributeName(AttributeData attribute)
    {
        var triggerType = attribute.ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
        return triggerType?.Name;
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

internal sealed class UserInterceptorInfo
{
    public UserInterceptorInfo(
        BakedInterceptorField field,
        string interceptorTypeName,
        ImmutableArray<string> triggerAttributeNames)
    {
        Field = field;
        InterceptorTypeName = interceptorTypeName;
        TriggerAttributeNames = triggerAttributeNames;
    }

    public BakedInterceptorField Field { get; }

    public string InterceptorTypeName { get; }

    public ImmutableArray<string> TriggerAttributeNames { get; }
}
