namespace Abp.SourceGenerators;

/// <summary>
/// Central catalog of ABP type and attribute names used by compile-time source generators.
/// Source generators target netstandard2.0 and cannot reference the Abp assembly (net9.0) directly,
/// so metadata names are maintained here instead of using typeof().
/// </summary>
internal static class AbpTypeNames
{
    /// <summary>
    /// Names passed to <see cref="Microsoft.CodeAnalysis.Compilation.GetTypeByMetadataName"/>.
    /// </summary>
    internal static class Metadata
    {
        public const string AbpModule = "Abp.Modules.AbpModule";
        public const string AbpInterceptorBase = "Abp.Dependency.AbpInterceptorBase";
        public const string ITransientDependency = "Abp.Dependency.ITransientDependency";
        public const string ISingletonDependency = "Abp.Dependency.ISingletonDependency";
        public const string IApplicationService = "Abp.Application.Services.IApplicationService";
        public const string CompileTimeInterceptionConfiguration = "Abp.Dependency.CompileTime.CompileTimeInterceptionConfiguration";
        public const string DisableConventionalRegistrationAttribute = "Abp.Dependency.CompileTime.DisableConventionalRegistrationAttribute";

        public static readonly string[] BuiltInInterceptors =
        {
            "Abp.Runtime.Validation.Interception.ValidationInterceptor",
            "Abp.Auditing.AuditingInterceptor",
            "Abp.EntityHistory.EntityHistoryInterceptor",
            "Abp.Domain.Uow.UnitOfWorkInterceptor",
            "Abp.Authorization.AuthorizationInterceptor"
        };
    }

    /// <summary>
    /// Short type names used when matching Roslyn symbols (e.g. <c>AttributeClass.Name</c>, <c>INamedTypeSymbol.Name</c>).
    /// </summary>
    internal static class Short
    {
        public const string IRepository = "IRepository";

        public static readonly string[] MarkerInterfaces =
        {
            "ITransientDependency",
            "ISingletonDependency",
            "IApplicationService"
        };

        internal static class Attributes
        {
            public const string AbpReflection = "AbpReflectionAttribute";
            public const string AbpIntercept = "AbpInterceptAttribute";
            public const string AbpInterceptor = "AbpInterceptorAttribute";
            public const string Audited = "AuditedAttribute";
            public const string DisableAuditing = "DisableAuditingAttribute";
            public const string UnitOfWork = "UnitOfWorkAttribute";
            public const string AbpAuthorize = "AbpAuthorizeAttribute";
            public const string AbpAllowAnonymous = "AbpAllowAnonymousAttribute";
            public const string RequiresFeature = "RequiresFeatureAttribute";
            public const string UseCase = "UseCaseAttribute";
            public const string DisableValidation = "DisableValidationAttribute";
        }
    }

    /// <summary>
    /// Fully qualified names emitted into generated C# source.
    /// </summary>
    internal static class FullyQualified
    {
        public const string ITransientDependency = "global::Abp.Dependency.ITransientDependency";
        public const string IIocResolver = "global::Abp.Dependency.IIocResolver";
        public const string IIocManager = "global::Abp.Dependency.IIocManager";
        public const string AbpInterceptorBase = "global::Abp.Dependency.AbpInterceptorBase";
        public const string DependencyLifeStyleSingleton = "global::Abp.Dependency.DependencyLifeStyle.Singleton";
        public const string DependencyLifeStyleTransient = "global::Abp.Dependency.DependencyLifeStyle.Transient";
        public const string IAvoidDuplicateCrossCuttingConcerns = "global::Abp.Application.Services.IAvoidDuplicateCrossCuttingConcerns";
        public const string DisableConventionalRegistrationAttribute = "global::Abp.Dependency.CompileTime.DisableConventionalRegistration";
        public const string CompileTimeAbpInvocation = "global::Abp.Dependency.CompileTime.CompileTimeAbpInvocation";
        public const string AbpMethodInterceptionMetadataProvider = "global::Abp.Dependency.AbpMethodInterceptionMetadataProvider";
        public const string AbpMethodInterceptionMetadata = "global::Abp.Dependency.AbpMethodInterceptionMetadata";
        public const string CompileTimeIocRegistrarRegistry = "global::Abp.Dependency.CompileTime.CompileTimeIocRegistrarRegistry";
        public const string CompileTimeInvocationInterceptorExecutor = "global::Abp.Dependency.CompileTime.CompileTimeInvocationInterceptorExecutor";
        public const string CompileTimeBuiltInInterceptorProvider = "global::Abp.Dependency.CompileTime.CompileTimeBuiltInInterceptorProvider";
        public const string AuditedAttribute = "global::Abp.Auditing.AuditedAttribute";
        public const string AbpAllowAnonymousAttribute = "global::Abp.Authorization.AbpAllowAnonymousAttribute";
        public const string AbpAuthorizeAttribute = "global::Abp.Authorization.AbpAuthorizeAttribute";
        public const string RequiresFeatureAttribute = "global::Abp.Application.Features.RequiresFeatureAttribute";
        public const string UseCaseAttribute = "global::Abp.EntityHistory.UseCaseAttribute";
        public const string UnitOfWorkAttribute = "global::Abp.Domain.Uow.UnitOfWorkAttribute";
        public const string UnitOfWorkOptions = "global::Abp.Domain.Uow.UnitOfWorkOptions";
    }

    internal static class GeneratedTypeSuffixes
    {
        public const string Intercepted = "_Intercepted";
        public const string Relay = "_Relay";
        public const string CompileTimeModule = "_CompileTime";
    }
}
