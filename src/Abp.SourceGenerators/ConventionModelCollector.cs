using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Abp.SourceGenerators;

internal sealed class ConventionTypeModel
{
    public INamedTypeSymbol ImplementationType { get; set; } = null!;
    public string ImplementationTypeName { get; set; } = "";
    public string Namespace { get; set; } = "";
    public bool IsApplicationService { get; set; }
    public bool RequiresCompileTimeInterception { get; set; }
    public bool IsSingleton { get; set; }
    public ImmutableArray<string> ServiceInterfaces { get; set; }
    public ImmutableArray<MethodModel> Methods { get; set; }

    public string InterceptedTypeName => $"{ImplementationType.Name}{AbpTypeNames.GeneratedTypeSuffixes.Intercepted}";

    public string RelayClassName => $"{ImplementationType.Name}{AbpTypeNames.GeneratedTypeSuffixes.Relay}";
}

internal sealed class MethodModel
{
    public string Name { get; set; } = "";
    public string MethodNameExpression { get; set; } = "";
    public string Signature { get; set; } = "";
    public string ParameterList { get; set; } = "";
    public string ArgumentList { get; set; } = "";
    public string ReturnType { get; set; } = "";
    public bool IsAsync { get; set; }
    public bool Validate { get; set; }
    public bool AbpReflection { get; set; } = true;
    public bool RequiresCompileTimeInterception { get; set; }
    public ImmutableArray<ParameterModel> Parameters { get; set; }
    public AspectModel Aspect { get; set; } = new AspectModel();
}

internal sealed class ParameterModel
{
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public int Position { get; set; }
    public bool IsOptional { get; set; }
    public bool HasDefaultValue { get; set; }
    public string? DefaultValueExpression { get; set; }
}

internal sealed class AspectModel
{
    public bool Audit { get; set; }
    public bool Validate { get; set; }
    public bool AbpReflection { get; set; } = true;
    public bool RequiresCompileTimeInterception { get; set; }
    public bool AllowAnonymous { get; set; }
    public bool HasUseCaseAttribute { get; set; }
    public string? UseCaseDescriptionExpression { get; set; }
    public bool ApplyConventionalUnitOfWork { get; set; }
    public string? UnitOfWorkAttributeExpression { get; set; }
    public string? UnitOfWorkOptionsExpression { get; set; }
    public string? AuthorizeAttributesExpression { get; set; }
    public string? FeatureAttributesExpression { get; set; }
    public string AuditParametersExpression { get; set; } = "null";
    public string AppliedAttributeTypesExpression { get; set; } = "global::System.Array.Empty<global::System.Type>()";
    public string BakedAttributesExpression { get; set; } = "global::System.Array.Empty<object>()";
    public ImmutableArray<string> BakedAttributeItems { get; set; }
    public ImmutableArray<BakedInterceptorField> MatchingUserInterceptors { get; set; }
}

internal static class ConventionModelCollector
{
    public static ImmutableArray<ConventionTypeModel> Collect(Compilation compilation)
    {
        var models = new List<ConventionTypeModel>();
        var abp = AbpCompilationSymbols.Resolve(compilation);

        if (!abp.HasTransientDependency)
        {
            return ImmutableArray<ConventionTypeModel>.Empty;
        }

        var userInterceptorRegistry = UserInterceptorRegistry.Collect(compilation);

        foreach (var symbol in compilation.Assembly.GlobalNamespace.GetAllTypes())
        {
            if (symbol.TypeKind != TypeKind.Class || symbol.IsAbstract || symbol.IsStatic)
            {
                continue;
            }

            if (symbol.IsGenericType || symbol.Arity > 0)
            {
                continue;
            }

            if (symbol.Name.EndsWith(AbpTypeNames.GeneratedTypeSuffixes.Intercepted)
                || symbol.Name.EndsWith(AbpTypeNames.GeneratedTypeSuffixes.Relay))
            {
                continue;
            }

            if (abp.DisableConventionalRegistration != null && AbpSymbolHelpers.HasAttribute(symbol, abp.DisableConventionalRegistration))
            {
                continue;
            }

            if (abp.InterceptorBase != null && InheritsFrom(symbol, abp.InterceptorBase))
            {
                continue;
            }

            var isTransient = Implements(symbol, abp.TransientDependency!);
            var isSingleton = abp.SingletonDependency != null && Implements(symbol, abp.SingletonDependency);

            if (!isTransient && !isSingleton)
            {
                continue;
            }

            var appServiceInterface = abp.ApplicationService == null
                ? null
                : FindAppServiceInterface(symbol, abp.ApplicationService);

            var isApplicationService = appServiceInterface != null;
            var serviceInterfaces = GetServiceInterfaces(symbol, abp.ApplicationService);

            var methods = isApplicationService
                ? CollectInterfaceMethods(symbol, appServiceInterface!, abp.ApplicationService!, userInterceptorRegistry)
                : ShouldCollectTypeMethods(symbol, abp.ApplicationService, userInterceptorRegistry)
                    ? CollectTypeMethods(symbol, abp.ApplicationService, userInterceptorRegistry)
                    : ImmutableArray<MethodModel>.Empty;

            var requiresCompileTimeInterception = isApplicationService
                || methods.Any(m => m.RequiresCompileTimeInterception);

            models.Add(new ConventionTypeModel
            {
                ImplementationType = symbol,
                ImplementationTypeName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Namespace = symbol.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : symbol.ContainingNamespace.ToDisplayString(),
                IsApplicationService = isApplicationService,
                RequiresCompileTimeInterception = requiresCompileTimeInterception,
                IsSingleton = isSingleton,
                ServiceInterfaces = serviceInterfaces,
                Methods = methods
            });
        }

        return models.ToImmutableArray();
    }

    private static ImmutableArray<string> GetServiceInterfaces(INamedTypeSymbol type, ITypeSymbol? applicationServiceSymbol)
    {
        return type.AllInterfaces
            .Where(i => i.TypeKind == TypeKind.Interface)
            .Where(i => !IsMarkerInterface(i, applicationServiceSymbol))
            .Select(i => i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Distinct()
            .ToImmutableArray();
    }

    private static bool IsMarkerInterface(INamedTypeSymbol iface, ITypeSymbol? applicationServiceSymbol)
    {
        if (AbpTypeNames.Short.MarkerInterfaces.Contains(iface.Name))
        {
            return true;
        }

        return applicationServiceSymbol != null
               && SymbolEqualityComparer.Default.Equals(iface, applicationServiceSymbol);
    }

    private static INamedTypeSymbol? FindAppServiceInterface(INamedTypeSymbol type, ITypeSymbol applicationServiceSymbol)
    {
        return type.AllInterfaces
            .Where(i => i.AllInterfaces.Any(ai => SymbolEqualityComparer.Default.Equals(ai, applicationServiceSymbol))
                        || SymbolEqualityComparer.Default.Equals(i, applicationServiceSymbol))
            .OrderByDescending(i => i.AllInterfaces.Length)
            .FirstOrDefault();
    }

    private static ImmutableArray<MethodModel> CollectInterfaceMethods(
        INamedTypeSymbol implementationType,
        INamedTypeSymbol appServiceInterface,
        ITypeSymbol applicationServiceSymbol,
        UserInterceptorRegistry userInterceptorRegistry)
    {
        var methods = new List<MethodModel>();
        var seen = new HashSet<string>();

        foreach (var iface in appServiceInterface.AllInterfaces
                     .Append(appServiceInterface)
                     .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default))
        {
            if (SymbolEqualityComparer.Default.Equals(iface, applicationServiceSymbol))
            {
                continue;
            }

            foreach (var member in iface.GetMembers().OfType<IMethodSymbol>())
            {
                if (member.MethodKind != MethodKind.Ordinary || member.IsStatic || member.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                var key = member.ToDisplayString();
                if (!seen.Add(key))
                {
                    continue;
                }

                var implementationMethod = implementationType.FindImplementationForInterfaceMember(member) as IMethodSymbol;
                if (implementationMethod == null || implementationMethod.IsAbstract)
                {
                    continue;
                }

                methods.Add(CreateMethodModel(implementationType, implementationMethod, applicationServiceSymbol, userInterceptorRegistry));
            }
        }

        return methods.ToImmutableArray();
    }

    private static bool ShouldCollectTypeMethods(
        INamedTypeSymbol type,
        ITypeSymbol? applicationServiceSymbol,
        UserInterceptorRegistry userInterceptorRegistry)
    {
        if (AbpSymbolHelpers.HasAttributeByName(type, AbpTypeNames.Short.Attributes.AbpReflection))
        {
            return true;
        }

        if (AbpSymbolHelpers.HasAttributeByName(type, AbpTypeNames.Short.Attributes.AbpIntercept))
        {
            return true;
        }

        if (type.GetMembers().OfType<IMethodSymbol>().Any(HasAbpReflectionAttribute))
        {
            return true;
        }

        if (userInterceptorRegistry.TypeHasInterceptorBinding(type))
        {
            return true;
        }

        if (HasInterceptableTypeAttributes(type))
        {
            return true;
        }

        return GetCandidateTypeMethods(type).Any(method =>
            userInterceptorRegistry.MethodHasInterceptorBinding(type, method)
            || AspectAnalyzer.MethodHasBuiltInInterceptors(type, method, applicationServiceSymbol));
    }

    private static ImmutableArray<MethodModel> CollectTypeMethods(
        INamedTypeSymbol implementationType,
        ITypeSymbol? applicationServiceSymbol,
        UserInterceptorRegistry userInterceptorRegistry)
    {
        return GetCandidateTypeMethods(implementationType)
            .Select(method => CreateMethodModel(implementationType, method, applicationServiceSymbol, userInterceptorRegistry))
            .ToImmutableArray();
    }

    private static IEnumerable<IMethodSymbol> GetCandidateTypeMethods(INamedTypeSymbol implementationType)
    {
        foreach (var member in implementationType.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.MethodKind != MethodKind.Ordinary || member.IsStatic || member.IsAbstract)
            {
                continue;
            }

            if (member.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            if (member.AssociatedSymbol is IPropertySymbol)
            {
                continue;
            }

            if (member.MethodKind == MethodKind.Constructor)
            {
                continue;
            }

            yield return member;
        }
    }

    private static bool HasInterceptableTypeAttributes(INamedTypeSymbol type)
    {
        return AbpSymbolHelpers.HasAttributeByName(type, AbpTypeNames.Short.Attributes.Audited)
               || AbpSymbolHelpers.HasAttributeByName(type, AbpTypeNames.Short.Attributes.UnitOfWork)
               || AbpSymbolHelpers.HasAttributeByName(type, AbpTypeNames.Short.Attributes.AbpAuthorize)
               || AbpSymbolHelpers.HasAttributeByName(type, AbpTypeNames.Short.Attributes.RequiresFeature)
               || AbpSymbolHelpers.HasAttributeByName(type, AbpTypeNames.Short.Attributes.UseCase);
    }

    private static bool HasAbpReflectionAttribute(ISymbol symbol)
    {
        return AbpSymbolHelpers.HasAttributeByName(symbol, AbpTypeNames.Short.Attributes.AbpReflection);
    }

    private static bool HasAttribute(INamedTypeSymbol type, string attributeName)
    {
        return AbpSymbolHelpers.HasAttributeByName(type, attributeName);
    }

    private static MethodModel CreateMethodModel(
        INamedTypeSymbol implementationType,
        IMethodSymbol method,
        ITypeSymbol? applicationServiceSymbol,
        UserInterceptorRegistry userInterceptorRegistry)
    {
        var returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var parameters = method.Parameters;
        var parameterList = string.Join(", ", parameters.Select(p =>
            $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"));
        var argumentList = string.Join(", ", parameters.Select(p => p.Name));
        var signature = parameters.Length == 0
            ? $"{method.Name}()"
            : $"{method.Name}({parameterList})";

        var aspect = AspectAnalyzer.Analyze(implementationType, method, applicationServiceSymbol, userInterceptorRegistry);

        return new MethodModel
        {
            Name = method.Name,
            MethodNameExpression = BuildMethodNameExpression(implementationType, method),
            Signature = signature,
            ParameterList = parameterList,
            ArgumentList = argumentList,
            ReturnType = returnType,
            IsAsync = IsAsyncReturnType(returnType),
            Validate = aspect.Validate,
            AbpReflection = aspect.AbpReflection,
            RequiresCompileTimeInterception = aspect.RequiresCompileTimeInterception,
            Parameters = parameters.Select((p, index) => new ParameterModel
            {
                Name = p.Name,
                TypeName = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Position = index,
                IsOptional = p.IsOptional,
                HasDefaultValue = p.HasExplicitDefaultValue,
                DefaultValueExpression = p.HasExplicitDefaultValue
                    ? FormatDefaultValue(p)
                    : null
            }).ToImmutableArray(),
            Aspect = aspect
        };
    }

    private static string? FormatDefaultValue(IParameterSymbol parameter)
    {
        if (!parameter.HasExplicitDefaultValue)
        {
            return null;
        }

        return parameter.ExplicitDefaultValue switch
        {
            null => "null",
            string s => $"\"{s}\"",
            bool b => b.ToString().ToLowerInvariant(),
            _ => parameter.ExplicitDefaultValue!.ToString()!
        };
    }

    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol interfaceSymbol)
    {
        return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, interfaceSymbol))
               || type.Interfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, interfaceSymbol));
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

    private static bool HasAttribute(INamedTypeSymbol type, INamedTypeSymbol attributeType)
    {
        return AbpSymbolHelpers.HasAttribute(type, attributeType);
    }

    private static bool IsAsyncReturnType(string returnType)
    {
        var normalized = NormalizeTypeName(returnType);
        return normalized.StartsWith("System.Threading.Tasks.Task")
               || normalized.StartsWith("System.Threading.Tasks.ValueTask");
    }

    private static string NormalizeTypeName(string returnType)
    {
        return returnType.StartsWith("global::")
            ? returnType.Substring("global::".Length)
            : returnType;
    }

    private static string BuildMethodNameExpression(INamedTypeSymbol implementationType, IMethodSymbol method)
    {
        if (method.ExplicitInterfaceImplementations.Length > 0)
        {
            var explicitMethod = method.ExplicitInterfaceImplementations[0];
            var interfaceTypeName = explicitMethod.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"nameof({interfaceTypeName}.{explicitMethod.Name})";
        }

        var typeName = implementationType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return $"nameof({typeName}.{method.Name})";
    }
}

internal static class NamespaceSymbolExtensions
{
    public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol namespaceSymbol)
    {
        foreach (var member in namespaceSymbol.GetMembers())
        {
            if (member is INamespaceSymbol childNamespace)
            {
                foreach (var type in childNamespace.GetAllTypes())
                {
                    yield return type;
                }
            }
            else if (member is INamedTypeSymbol type)
            {
                yield return type;

                foreach (var nested in type.GetTypeMembers())
                {
                    yield return nested;
                }
            }
        }
    }
}
