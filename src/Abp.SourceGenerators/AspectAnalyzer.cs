using System.Linq;
using Microsoft.CodeAnalysis;

namespace Abp.SourceGenerators;

internal static class AspectAnalyzer
{
    public static AspectModel Analyze(INamedTypeSymbol implementationType, IMethodSymbol method, ITypeSymbol applicationServiceSymbol)
    {
        var unitOfWork = GetUnitOfWorkExpression(implementationType, method, applicationServiceSymbol);
        var audit = ShouldAudit(implementationType, method, applicationServiceSymbol);
        var validate = IsApplicationServiceType(implementationType, applicationServiceSymbol);

        return new AspectModel
        {
            Audit = audit,
            Validate = validate,
            AllowAnonymous = HasAttribute(method, "AbpAllowAnonymousAttribute"),
            UnitOfWorkOptionsExpression = unitOfWork,
            AuthorizeAttributesExpression = BuildAuthorizeExpression(implementationType, method),
            FeatureAttributesExpression = BuildFeatureExpression(implementationType, method),
            AuditParametersExpression = BuildAuditParametersExpression(method)
        };
    }

    private static bool IsApplicationServiceType(INamedTypeSymbol type, ITypeSymbol applicationServiceSymbol)
    {
        return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, applicationServiceSymbol)
                                           || i.AllInterfaces.Any(ai => SymbolEqualityComparer.Default.Equals(ai, applicationServiceSymbol)));
    }

    private static bool ShouldAudit(INamedTypeSymbol type, IMethodSymbol method, ITypeSymbol applicationServiceSymbol)
    {
        if (HasAttribute(method, "DisableAuditingAttribute"))
        {
            return false;
        }

        if (HasAttribute(method, "AuditedAttribute") || HasAttribute(type, "AuditedAttribute"))
        {
            return true;
        }

        return IsApplicationServiceType(type, applicationServiceSymbol);
    }

    private static string? GetUnitOfWorkExpression(INamedTypeSymbol type, IMethodSymbol method, ITypeSymbol applicationServiceSymbol)
    {
        var attribute = FindAttribute(method, "UnitOfWorkAttribute") ?? FindAttribute(type, "UnitOfWorkAttribute");
        if (attribute != null)
        {
            return BuildUnitOfWorkOptions(attribute);
        }

        if (IsApplicationServiceType(type, applicationServiceSymbol) || ImplementsRepository(type))
        {
            return "new global::Abp.Domain.Uow.UnitOfWorkOptions()";
        }

        return null;
    }

    private static bool ImplementsRepository(INamedTypeSymbol type)
    {
        return type.AllInterfaces.Any(i => i.Name == "IRepository");
    }

    private static string? BuildUnitOfWorkOptions(AttributeData attribute)
    {
        if (attribute.NamedArguments.Any(a => a.Key == "IsDisabled" && a.Value.Value is true))
        {
            return null;
        }

        var parts = new System.Collections.Generic.List<string>();

        if (TryGetNamedBool(attribute, "IsTransactional", out var isTransactional))
        {
            parts.Add($"IsTransactional = {isTransactional.ToString().ToLowerInvariant()}");
        }

        if (TryGetNamedEnum(attribute, "Scope", out var scope))
        {
            parts.Add($"Scope = global::System.Transactions.TransactionScopeOption.{scope}");
        }

        if (TryGetNamedEnum(attribute, "IsolationLevel", out var isolation))
        {
            parts.Add($"IsolationLevel = global::System.Transactions.IsolationLevel.{isolation}");
        }

        if (attribute.NamedArguments.FirstOrDefault(a => a.Key == "Timeout").Value.Value is int timeoutMs)
        {
            parts.Add($"Timeout = global::System.TimeSpan.FromMilliseconds({timeoutMs})");
        }

        return parts.Count == 0
            ? "new global::Abp.Domain.Uow.UnitOfWorkOptions()"
            : $"new global::Abp.Domain.Uow.UnitOfWorkOptions {{ {string.Join(", ", parts)} }}";
    }

    private static string? BuildAuthorizeExpression(INamedTypeSymbol type, IMethodSymbol method)
    {
        var attributes = method.GetAttributes()
            .Concat(type.GetAttributes())
            .Where(a => a.AttributeClass?.Name is "AbpAuthorizeAttribute")
            .ToList();

        if (attributes.Count == 0)
        {
            return null;
        }

        var items = attributes.Select(a =>
        {
            var permissions = a.ConstructorArguments.FirstOrDefault().Values
                .Select(v => $"\"{v.Value}\"")
                .ToArray();
            var requireAll = a.NamedArguments.FirstOrDefault(n => n.Key == "RequireAllPermissions").Value.Value is true;
            return $"new global::Abp.Authorization.AbpAuthorizeAttribute({string.Join(", ", permissions)}) {{ RequireAllPermissions = {requireAll.ToString().ToLowerInvariant()} }}";
        });

        return $"new global::Abp.Authorization.AbpAuthorizeAttribute[] {{ {string.Join(", ", items)} }}";
    }

    private static string? BuildFeatureExpression(INamedTypeSymbol type, IMethodSymbol method)
    {
        var attributes = method.GetAttributes()
            .Concat(type.GetAttributes())
            .Where(a => a.AttributeClass?.Name is "RequiresFeatureAttribute")
            .ToList();

        if (attributes.Count == 0)
        {
            return null;
        }

        var items = attributes.Select(a =>
        {
            var features = a.ConstructorArguments.FirstOrDefault().Values
                .Select(v => $"\"{v.Value}\"")
                .ToArray();
            var requireAll = a.NamedArguments.FirstOrDefault(n => n.Key == "RequiresAll").Value.Value is true;
            return $"new global::Abp.Application.Features.RequiresFeatureAttribute({string.Join(", ", features)}) {{ RequiresAll = {requireAll.ToString().ToLowerInvariant()} }}";
        });

        return $"new global::Abp.Application.Features.RequiresFeatureAttribute[] {{ {string.Join(", ", items)} }}";
    }

    private static string BuildAuditParametersExpression(IMethodSymbol method)
    {
        if (method.Parameters.Length == 0)
        {
            return "null";
        }

        var entries = method.Parameters.Select(p => $"[\"{p.Name}\"] = {p.Name}");
        return $"new global::System.Collections.Generic.Dictionary<string, object?> {{ {string.Join(", ", entries)} }}";
    }

    private static AttributeData? FindAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == attributeName);
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().Any(a => a.AttributeClass?.Name == attributeName);
    }

    private static bool TryGetNamedBool(AttributeData attribute, string name, out bool value)
    {
        var argument = attribute.NamedArguments.FirstOrDefault(a => a.Key == name);
        if (argument.Value.Value is bool boolValue)
        {
            value = boolValue;
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryGetNamedEnum(AttributeData attribute, string name, out string value)
    {
        var argument = attribute.NamedArguments.FirstOrDefault(a => a.Key == name);
        if (argument.Value.Value != null)
        {
            value = argument.Value.Value!.ToString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
