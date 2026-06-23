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
    public bool IsSingleton { get; set; }
    public ImmutableArray<string> ServiceInterfaces { get; set; }
    public ImmutableArray<MethodModel> Methods { get; set; }

    public string InterceptedTypeName => $"{ImplementationType.Name}_Intercepted";

    public string RelayClassName => $"{ImplementationType.Name}_Relay";
}

internal sealed class MethodModel
{
    public string Name { get; set; } = "";
    public string Signature { get; set; } = "";
    public string ParameterList { get; set; } = "";
    public string ArgumentList { get; set; } = "";
    public string ReturnType { get; set; } = "";
    public bool IsAsync { get; set; }
    public bool Validate { get; set; }
    public AspectModel Aspect { get; set; } = new AspectModel();
}

internal sealed class AspectModel
{
    public bool Audit { get; set; }
    public bool Validate { get; set; }
    public bool AllowAnonymous { get; set; }
    public string? UnitOfWorkOptionsExpression { get; set; }
    public string? AuthorizeAttributesExpression { get; set; }
    public string? FeatureAttributesExpression { get; set; }
    public string AuditParametersExpression { get; set; } = "null";
}

internal static class ConventionModelCollector
{
    private static readonly string[] MarkerInterfaces =
    {
        "ITransientDependency",
        "ISingletonDependency",
        "IApplicationService"
    };

    public static ImmutableArray<ConventionTypeModel> Collect(Compilation compilation)
    {
        var models = new List<ConventionTypeModel>();
        var transientSymbol = compilation.GetTypeByMetadataName("Abp.Dependency.ITransientDependency");
        var singletonSymbol = compilation.GetTypeByMetadataName("Abp.Dependency.ISingletonDependency");
        var applicationServiceSymbol = compilation.GetTypeByMetadataName("Abp.Application.Services.IApplicationService");
        var disableSymbol = compilation.GetTypeByMetadataName("Abp.Dependency.CompileTime.DisableConventionalRegistrationAttribute");

        if (transientSymbol == null)
        {
            return ImmutableArray<ConventionTypeModel>.Empty;
        }

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

            if (symbol.Name.EndsWith("_Intercepted") || symbol.Name.EndsWith("_Relay"))
            {
                continue;
            }

            if (disableSymbol != null && HasAttribute(symbol, disableSymbol))
            {
                continue;
            }

            var isTransient = Implements(symbol, transientSymbol);
            var isSingleton = singletonSymbol != null && Implements(symbol, singletonSymbol);

            if (!isTransient && !isSingleton)
            {
                continue;
            }

            var appServiceInterface = applicationServiceSymbol == null
                ? null
                : FindAppServiceInterface(symbol, applicationServiceSymbol);

            var isApplicationService = appServiceInterface != null;
            var serviceInterfaces = GetServiceInterfaces(symbol, applicationServiceSymbol);

            var methods = isApplicationService
                ? CollectMethods(symbol, appServiceInterface!, applicationServiceSymbol!)
                : ImmutableArray<MethodModel>.Empty;

            models.Add(new ConventionTypeModel
            {
                ImplementationType = symbol,
                ImplementationTypeName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                Namespace = symbol.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : symbol.ContainingNamespace.ToDisplayString(),
                IsApplicationService = isApplicationService,
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
        if (MarkerInterfaces.Contains(iface.Name))
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

    private static ImmutableArray<MethodModel> CollectMethods(
        INamedTypeSymbol implementationType,
        INamedTypeSymbol appServiceInterface,
        ITypeSymbol applicationServiceSymbol)
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

                methods.Add(CreateMethodModel(implementationType, implementationMethod, applicationServiceSymbol));
            }
        }

        return methods.ToImmutableArray();
    }

    private static MethodModel CreateMethodModel(
        INamedTypeSymbol implementationType,
        IMethodSymbol method,
        ITypeSymbol applicationServiceSymbol)
    {
        var returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var parameters = method.Parameters;
        var parameterList = string.Join(", ", parameters.Select(p =>
            $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"));
        var argumentList = string.Join(", ", parameters.Select(p => p.Name));
        var signature = parameters.Length == 0
            ? $"{method.Name}()"
            : $"{method.Name}({parameterList})";

        var aspect = AspectAnalyzer.Analyze(implementationType, method, applicationServiceSymbol);

        return new MethodModel
        {
            Name = method.Name,
            Signature = signature,
            ParameterList = parameterList,
            ArgumentList = argumentList,
            ReturnType = returnType,
            IsAsync = returnType.StartsWith("System.Threading.Tasks.Task"),
            Validate = aspect.Validate,
            Aspect = aspect
        };
    }

    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol interfaceSymbol)
    {
        return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, interfaceSymbol))
               || type.Interfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, interfaceSymbol));
    }

    private static bool HasAttribute(INamedTypeSymbol type, INamedTypeSymbol attributeType)
    {
        return type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType));
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
