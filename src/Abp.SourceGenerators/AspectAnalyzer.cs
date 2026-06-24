using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Abp.SourceGenerators;

internal static class AspectAnalyzer
{
    public static AspectModel Analyze(INamedTypeSymbol implementationType, IMethodSymbol method, ITypeSymbol applicationServiceSymbol)
    {
        var unitOfWork = GetUnitOfWorkMetadata(implementationType, method, applicationServiceSymbol);
        var audit = ShouldAudit(implementationType, method, applicationServiceSymbol);
        var validate = IsApplicationServiceType(implementationType, applicationServiceSymbol)
                       && !HasAttribute(method, "DisableValidationAttribute")
                       && !HasAttribute(implementationType, "DisableValidationAttribute");

        var authorizeExpression = BuildAuthorizeExpression(implementationType, method);
        var featureExpression = BuildFeatureExpression(implementationType, method);

        return new AspectModel
        {
            Audit = audit,
            Validate = validate,
            AllowAnonymous = HasAttribute(method, "AbpAllowAnonymousAttribute"),
            HasUseCaseAttribute = HasAttribute(method, "UseCaseAttribute") || HasAttribute(implementationType, "UseCaseAttribute"),
            UseCaseDescriptionExpression = BuildUseCaseDescriptionExpression(implementationType, method),
            ApplyConventionalUnitOfWork = unitOfWork.ApplyConventional,
            UnitOfWorkAttributeExpression = unitOfWork.AttributeExpression,
            UnitOfWorkOptionsExpression = unitOfWork.OptionsExpression,
            AuthorizeAttributesExpression = authorizeExpression,
            FeatureAttributesExpression = featureExpression,
            AuditParametersExpression = BuildAuditParametersExpression(method),
            AppliedAttributeTypesExpression = BuildAppliedAttributeTypesExpression(implementationType, method),
            BakedAttributeItems = BuildBakedAttributeItems(
                unitOfWork.AttributeExpression,
                authorizeExpression,
                featureExpression,
                audit,
                HasAttribute(method, "AbpAllowAnonymousAttribute"),
                HasAttribute(method, "UseCaseAttribute") || HasAttribute(implementationType, "UseCaseAttribute"),
                BuildUseCaseDescriptionExpression(implementationType, method))
        };
    }

    public static string BuildBakedAttributesExpression(ImmutableArray<string> items)
    {
        if (items.IsDefaultOrEmpty)
        {
            return "global::System.Array.Empty<object>()";
        }

        return $"new object[] {{ {string.Join(", ", items)} }}";
    }

    public static ImmutableArray<BakedInterceptorField> GetBuiltInInterceptorFields(AspectModel aspect)
    {
        var fields = new System.Collections.Generic.List<BakedInterceptorField>();

        if (aspect.Validate)
        {
            fields.Add(new BakedInterceptorField(
                "_validationInterceptor",
                "global::Abp.Dependency.CompileTime.CompileTimeBuiltInInterceptorProvider.Validation(iocResolver)"));
        }

        if (aspect.Audit)
        {
            fields.Add(new BakedInterceptorField(
                "_auditingInterceptor",
                "global::Abp.Dependency.CompileTime.CompileTimeBuiltInInterceptorProvider.Auditing(iocResolver)"));
        }

        if (aspect.HasUseCaseAttribute)
        {
            fields.Add(new BakedInterceptorField(
                "_entityHistoryInterceptor",
                "global::Abp.Dependency.CompileTime.CompileTimeBuiltInInterceptorProvider.EntityHistory(iocResolver)"));
        }

        if (aspect.UnitOfWorkAttributeExpression != null || aspect.ApplyConventionalUnitOfWork)
        {
            fields.Add(new BakedInterceptorField(
                "_unitOfWorkInterceptor",
                "global::Abp.Dependency.CompileTime.CompileTimeBuiltInInterceptorProvider.UnitOfWork(iocResolver)"));
        }

        if (aspect.AuthorizeAttributesExpression != null || aspect.FeatureAttributesExpression != null)
        {
            fields.Add(new BakedInterceptorField(
                "_authorizationInterceptor",
                "global::Abp.Dependency.CompileTime.CompileTimeBuiltInInterceptorProvider.Authorization(iocResolver)"));
        }

        return fields.ToImmutableArray();
    }

    public static ImmutableArray<BakedInterceptorField> CollectServiceInterceptorFields(
        ConventionTypeModel model,
        ImmutableArray<BakedInterceptorField> userInterceptorFields)
    {
        var fields = new System.Collections.Generic.List<BakedInterceptorField>();
        var seen = new System.Collections.Generic.HashSet<string>();

        foreach (var method in model.Methods)
        {
            foreach (var field in GetMethodInterceptorFields(method.Aspect, userInterceptorFields))
            {
                if (seen.Add(field.FieldName))
                {
                    fields.Add(field);
                }
            }
        }

        return fields.ToImmutableArray();
    }

    public static ImmutableArray<BakedInterceptorField> GetMethodInterceptorFields(
        AspectModel aspect,
        ImmutableArray<BakedInterceptorField> userInterceptorFields)
    {
        var builtIn = GetBuiltInInterceptorFields(aspect);
        if (userInterceptorFields.IsDefaultOrEmpty)
        {
            return builtIn;
        }

        if (builtIn.IsDefaultOrEmpty)
        {
            return userInterceptorFields;
        }

        var combined = ImmutableArray.CreateBuilder<BakedInterceptorField>(builtIn.Length + userInterceptorFields.Length);
        combined.AddRange(builtIn);
        combined.AddRange(userInterceptorFields);
        return combined.ToImmutable();
    }

    public static ImmutableArray<BakedInterceptorField> CollectServiceInterceptorFields(ConventionTypeModel model)
    {
        return CollectServiceInterceptorFields(model, ImmutableArray<BakedInterceptorField>.Empty);
    }

    private static ImmutableArray<string> BuildBakedAttributeItems(
        string? unitOfWorkAttributeExpression,
        string? authorizeAttributesExpression,
        string? featureAttributesExpression,
        bool audit,
        bool allowAnonymous,
        bool hasUseCase,
        string? useCaseDescriptionExpression)
    {
        var items = new System.Collections.Generic.List<string>();

        if (unitOfWorkAttributeExpression != null)
        {
            items.Add(unitOfWorkAttributeExpression);
        }

        items.AddRange(ExtractArrayItemExpressions(authorizeAttributesExpression));
        items.AddRange(ExtractArrayItemExpressions(featureAttributesExpression));

        if (audit)
        {
            items.Add("new global::Abp.Auditing.AuditedAttribute()");
        }

        if (allowAnonymous)
        {
            items.Add("new global::Abp.Authorization.AbpAllowAnonymousAttribute()");
        }

        if (hasUseCase && useCaseDescriptionExpression != null)
        {
            items.Add($"new global::Abp.EntityHistory.UseCaseAttribute({useCaseDescriptionExpression})");
        }

        return items.ToImmutableArray();
    }

    private static System.Collections.Generic.IEnumerable<string> ExtractArrayItemExpressions(string? arrayExpression)
    {
        if (string.IsNullOrWhiteSpace(arrayExpression))
        {
            yield break;
        }

        var start = arrayExpression.IndexOf('{');
        var end = arrayExpression.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            yield break;
        }

        var body = arrayExpression.Substring(start + 1, end - start - 1);
        foreach (var item in body.Split(','))
        {
            var trimmed = item.Trim();
            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
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

    private static (bool ApplyConventional, string? AttributeExpression, string? OptionsExpression) GetUnitOfWorkMetadata(
        INamedTypeSymbol type,
        IMethodSymbol method,
        ITypeSymbol applicationServiceSymbol)
    {
        var attribute = FindAttribute(method, "UnitOfWorkAttribute") ?? FindAttribute(type, "UnitOfWorkAttribute");
        if (attribute != null)
        {
            if (attribute.NamedArguments.Any(a => a.Key == "IsDisabled" && a.Value.Value is true))
            {
                return (false, null, null);
            }

            var attributeExpression = BuildUnitOfWorkAttributeExpression(attribute);
            return (false, attributeExpression, BuildUnitOfWorkOptions(attribute));
        }

        if (IsApplicationServiceType(type, applicationServiceSymbol) || ImplementsRepository(type))
        {
            return (true, null, "new global::Abp.Domain.Uow.UnitOfWorkOptions()");
        }

        return (false, null, null);
    }

    private static string BuildUnitOfWorkAttributeExpression(AttributeData attribute)
    {
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
            ? "new global::Abp.Domain.Uow.UnitOfWorkAttribute()"
            : $"new global::Abp.Domain.Uow.UnitOfWorkAttribute {{ {string.Join(", ", parts)} }}";
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

    private static string? BuildUseCaseDescriptionExpression(INamedTypeSymbol type, IMethodSymbol method)
    {
        var attribute = FindAttribute(method, "UseCaseAttribute") ?? FindAttribute(type, "UseCaseAttribute");
        if (attribute == null)
        {
            return null;
        }

        var description = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Description").Value.Value as string;
        return description == null ? "null" : $"\"{description}\"";
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

    private static string BuildAppliedAttributeTypesExpression(INamedTypeSymbol implementationType, IMethodSymbol method)
    {
        var attributeTypes = method.GetAttributes()
            .Concat(implementationType.GetAttributes())
            .Select(a => a.AttributeClass)
            .Where(a => a != null)
            .Distinct(SymbolEqualityComparer.Default)
            .Select(a => $"typeof({a!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})")
            .ToArray();

        return attributeTypes.Length == 0
            ? "global::System.Array.Empty<global::System.Type>()"
            : $"new global::System.Type[] {{ {string.Join(", ", attributeTypes)} }}";
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
