using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Abp.SourceGenerators;

internal static class AspectAnalyzer
{
    public static AspectModel Analyze(
        INamedTypeSymbol implementationType,
        IMethodSymbol method,
        ITypeSymbol? applicationServiceSymbol,
        UserInterceptorRegistry userInterceptorRegistry)
    {
        var unitOfWork = GetUnitOfWorkMetadata(implementationType, method, applicationServiceSymbol);
        var audit = ShouldAudit(implementationType, method, applicationServiceSymbol);
        var validate = applicationServiceSymbol != null
                       && IsApplicationServiceType(implementationType, applicationServiceSymbol)
                       && !HasAttribute(method, AbpTypeNames.Short.Attributes.DisableValidation)
                       && !HasAttribute(implementationType, AbpTypeNames.Short.Attributes.DisableValidation);

        var authorizeExpression = BuildAuthorizeExpression(implementationType, method);
        var featureExpression = BuildFeatureExpression(implementationType, method);

        var aspect = new AspectModel
        {
            Audit = audit,
            Validate = validate,
            AllowAnonymous = HasAttribute(method, AbpTypeNames.Short.Attributes.AbpAllowAnonymous),
            HasUseCaseAttribute = HasAttribute(method, AbpTypeNames.Short.Attributes.UseCase) || HasAttribute(implementationType, AbpTypeNames.Short.Attributes.UseCase),
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
                HasAttribute(method, AbpTypeNames.Short.Attributes.AbpAllowAnonymous),
                HasAttribute(method, AbpTypeNames.Short.Attributes.UseCase) || HasAttribute(implementationType, AbpTypeNames.Short.Attributes.UseCase),
                BuildUseCaseDescriptionExpression(implementationType, method))
        };

        aspect.AbpReflection = ShouldUseAbpReflection(
            implementationType,
            method,
            applicationServiceSymbol,
            aspect,
            userInterceptorRegistry);
        aspect.MatchingUserInterceptors = userInterceptorRegistry.GetMatchingInterceptors(implementationType, method);
        aspect.RequiresCompileTimeInterception = RequiresCompileTimeInterception(
            implementationType,
            method,
            applicationServiceSymbol,
            aspect,
            userInterceptorRegistry);
        return aspect;
    }

    public static AspectModel Analyze(
        INamedTypeSymbol implementationType,
        IMethodSymbol method,
        ITypeSymbol? applicationServiceSymbol)
        => Analyze(implementationType, method, applicationServiceSymbol, UserInterceptorRegistry.Empty);

    public static bool MethodHasBuiltInInterceptors(
        INamedTypeSymbol implementationType,
        IMethodSymbol method,
        ITypeSymbol? applicationServiceSymbol)
    {
        var aspect = Analyze(implementationType, method, applicationServiceSymbol);
        return !GetBuiltInInterceptorFields(aspect).IsDefaultOrEmpty;
    }

    public static bool RequiresCompileTimeInterception(
        INamedTypeSymbol implementationType,
        IMethodSymbol method,
        ITypeSymbol? applicationServiceSymbol,
        AspectModel aspect,
        UserInterceptorRegistry userInterceptorRegistry)
    {
        if (applicationServiceSymbol != null
            && IsApplicationServiceType(implementationType, applicationServiceSymbol))
        {
            return true;
        }

        if (FindAttribute(method, AbpTypeNames.Short.Attributes.AbpReflection) != null
            || FindAttribute(implementationType, AbpTypeNames.Short.Attributes.AbpReflection) != null)
        {
            return true;
        }

        if (userInterceptorRegistry.MethodHasInterceptorBinding(implementationType, method))
        {
            return true;
        }

        return !GetBuiltInInterceptorFields(aspect).IsDefaultOrEmpty;
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
                $"{AbpTypeNames.FullyQualified.CompileTimeBuiltInInterceptorProvider}.Validation(iocResolver)"));
        }

        if (aspect.Audit)
        {
            fields.Add(new BakedInterceptorField(
                "_auditingInterceptor",
                $"{AbpTypeNames.FullyQualified.CompileTimeBuiltInInterceptorProvider}.Auditing(iocResolver)"));
        }

        if (aspect.HasUseCaseAttribute)
        {
            fields.Add(new BakedInterceptorField(
                "_entityHistoryInterceptor",
                $"{AbpTypeNames.FullyQualified.CompileTimeBuiltInInterceptorProvider}.EntityHistory(iocResolver)"));
        }

        if (aspect.UnitOfWorkAttributeExpression != null || aspect.ApplyConventionalUnitOfWork)
        {
            fields.Add(new BakedInterceptorField(
                "_unitOfWorkInterceptor",
                $"{AbpTypeNames.FullyQualified.CompileTimeBuiltInInterceptorProvider}.UnitOfWork(iocResolver)"));
        }

        if (aspect.AuthorizeAttributesExpression != null || aspect.FeatureAttributesExpression != null)
        {
            fields.Add(new BakedInterceptorField(
                "_authorizationInterceptor",
                $"{AbpTypeNames.FullyQualified.CompileTimeBuiltInInterceptorProvider}.Authorization(iocResolver)"));
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
            foreach (var field in GetMethodInterceptorFields(method.Aspect, model.IsApplicationService, userInterceptorFields))
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
        bool isApplicationService,
        ImmutableArray<BakedInterceptorField> userInterceptorFields)
    {
        var builtIn = GetBuiltInInterceptorFields(aspect);
        var matchingUser = isApplicationService
            ? userInterceptorFields
            : aspect.MatchingUserInterceptors;

        if (matchingUser.IsDefaultOrEmpty)
        {
            return builtIn;
        }

        if (builtIn.IsDefaultOrEmpty)
        {
            return matchingUser;
        }

        var combined = ImmutableArray.CreateBuilder<BakedInterceptorField>(builtIn.Length + matchingUser.Length);
        combined.AddRange(builtIn);
        combined.AddRange(matchingUser);
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
            items.Add($"new {AbpTypeNames.FullyQualified.AuditedAttribute}()");
        }

        if (allowAnonymous)
        {
            items.Add($"new {AbpTypeNames.FullyQualified.AbpAllowAnonymousAttribute}()");
        }

        if (hasUseCase && useCaseDescriptionExpression != null)
        {
            items.Add($"new {AbpTypeNames.FullyQualified.UseCaseAttribute}({useCaseDescriptionExpression})");
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

    private static bool IsApplicationServiceType(INamedTypeSymbol type, ITypeSymbol? applicationServiceSymbol)
    {
        if (applicationServiceSymbol == null)
        {
            return false;
        }

        return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, applicationServiceSymbol)
                                           || i.AllInterfaces.Any(ai => SymbolEqualityComparer.Default.Equals(ai, applicationServiceSymbol)));
    }

    private static bool ShouldAudit(INamedTypeSymbol type, IMethodSymbol method, ITypeSymbol? applicationServiceSymbol)
    {
        if (HasAttribute(method, AbpTypeNames.Short.Attributes.DisableAuditing))
        {
            return false;
        }

        if (HasAttribute(method, AbpTypeNames.Short.Attributes.Audited) || HasAttribute(type, AbpTypeNames.Short.Attributes.Audited))
        {
            return true;
        }

        return IsApplicationServiceType(type, applicationServiceSymbol);
    }

    private static (bool ApplyConventional, string? AttributeExpression, string? OptionsExpression) GetUnitOfWorkMetadata(
        INamedTypeSymbol type,
        IMethodSymbol method,
        ITypeSymbol? applicationServiceSymbol)
    {
        var attribute = FindAttribute(method, AbpTypeNames.Short.Attributes.UnitOfWork) ?? FindAttribute(type, AbpTypeNames.Short.Attributes.UnitOfWork);
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
            return (true, null, $"new {AbpTypeNames.FullyQualified.UnitOfWorkOptions}()");
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
            ? $"new {AbpTypeNames.FullyQualified.UnitOfWorkAttribute}()"
            : $"new {AbpTypeNames.FullyQualified.UnitOfWorkAttribute} {{ {string.Join(", ", parts)} }}";
    }

    private static string? GetUnitOfWorkExpression(INamedTypeSymbol type, IMethodSymbol method, ITypeSymbol? applicationServiceSymbol)
    {
        var attribute = FindAttribute(method, AbpTypeNames.Short.Attributes.UnitOfWork) ?? FindAttribute(type, AbpTypeNames.Short.Attributes.UnitOfWork);
        if (attribute != null)
        {
            return BuildUnitOfWorkOptions(attribute);
        }

        if (IsApplicationServiceType(type, applicationServiceSymbol) || ImplementsRepository(type))
        {
            return $"new {AbpTypeNames.FullyQualified.UnitOfWorkOptions}()";
        }

        return null;
    }

    private static bool ImplementsRepository(INamedTypeSymbol type)
    {
        return type.AllInterfaces.Any(i => i.Name == AbpTypeNames.Short.IRepository);
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
            ? $"new {AbpTypeNames.FullyQualified.UnitOfWorkOptions}()"
            : $"new {AbpTypeNames.FullyQualified.UnitOfWorkOptions} {{ {string.Join(", ", parts)} }}";
    }

    private static string? BuildUseCaseDescriptionExpression(INamedTypeSymbol type, IMethodSymbol method)
    {
        var attribute = FindAttribute(method, AbpTypeNames.Short.Attributes.UseCase) ?? FindAttribute(type, AbpTypeNames.Short.Attributes.UseCase);
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
            .Where(a => a.AttributeClass?.Name is AbpTypeNames.Short.Attributes.AbpAuthorize)
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
            return $"new {AbpTypeNames.FullyQualified.AbpAuthorizeAttribute}({string.Join(", ", permissions)}) {{ RequireAllPermissions = {requireAll.ToString().ToLowerInvariant()} }}";
        });

        return $"new {AbpTypeNames.FullyQualified.AbpAuthorizeAttribute}[] {{ {string.Join(", ", items)} }}";
    }

    private static string? BuildFeatureExpression(INamedTypeSymbol type, IMethodSymbol method)
    {
        var attributes = method.GetAttributes()
            .Concat(type.GetAttributes())
            .Where(a => a.AttributeClass?.Name is AbpTypeNames.Short.Attributes.RequiresFeature)
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
            return $"new {AbpTypeNames.FullyQualified.RequiresFeatureAttribute}({string.Join(", ", features)}) {{ RequiresAll = {requireAll.ToString().ToLowerInvariant()} }}";
        });

        return $"new {AbpTypeNames.FullyQualified.RequiresFeatureAttribute}[] {{ {string.Join(", ", items)} }}";
    }

    private static string BuildAuditParametersExpression(IMethodSymbol method)
    {
        if (method.Parameters.Length == 0)
        {
            return "null";
        }

        var entries = method.Parameters.Select(p => $"[nameof({p.Name})] = {p.Name}");
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
        => AbpSymbolHelpers.FindAttributeByName(symbol, attributeName);

    private static bool HasAttribute(ISymbol symbol, string attributeName)
        => AbpSymbolHelpers.HasAttributeByName(symbol, attributeName);

    private static bool ShouldUseAbpReflection(
        INamedTypeSymbol type,
        IMethodSymbol method,
        ITypeSymbol? applicationServiceSymbol,
        AspectModel aspect,
        UserInterceptorRegistry userInterceptorRegistry)
    {
        var methodAttribute = FindAttribute(method, AbpTypeNames.Short.Attributes.AbpReflection);
        if (methodAttribute != null)
        {
            return GetAbpReflectionIncludeValue(methodAttribute);
        }

        var typeAttribute = FindAttribute(type, AbpTypeNames.Short.Attributes.AbpReflection);
        if (typeAttribute != null)
        {
            return GetAbpReflectionIncludeValue(typeAttribute);
        }

        if (applicationServiceSymbol != null
            && IsApplicationServiceType(type, applicationServiceSymbol))
        {
            return true;
        }

        if (userInterceptorRegistry.MethodHasInterceptorBinding(type, method))
        {
            return true;
        }

        return !GetBuiltInInterceptorFields(aspect).IsDefaultOrEmpty;
    }

    private static bool GetAbpReflectionIncludeValue(AttributeData attribute)
    {
        var include = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Include").Value.Value;
        return include is not bool value || value;
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
