using System.Linq;
using Microsoft.CodeAnalysis;

namespace Abp.SourceGenerators;

internal sealed class AbpCompilationSymbols
{
    public INamedTypeSymbol? TransientDependency { get; private set; }
    public INamedTypeSymbol? SingletonDependency { get; private set; }
    public INamedTypeSymbol? ApplicationService { get; private set; }
    public INamedTypeSymbol? DisableConventionalRegistration { get; private set; }
    public INamedTypeSymbol? InterceptorBase { get; private set; }
    public INamedTypeSymbol? AbpModule { get; private set; }

    public bool HasTransientDependency => TransientDependency != null;

    public static AbpCompilationSymbols Resolve(Compilation compilation)
    {
        return new AbpCompilationSymbols
        {
            TransientDependency = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.ITransientDependency),
            SingletonDependency = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.ISingletonDependency),
            ApplicationService = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.IApplicationService),
            DisableConventionalRegistration = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.DisableConventionalRegistrationAttribute),
            InterceptorBase = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.AbpInterceptorBase),
            AbpModule = compilation.GetTypeByMetadataName(AbpTypeNames.Metadata.AbpModule)
        };
    }

    public INamedTypeSymbol[] ResolveBuiltInInterceptors(Compilation compilation)
    {
        return AbpTypeNames.Metadata.BuiltInInterceptors
            .Select(compilation.GetTypeByMetadataName)
            .Where(type => type != null)
            .Cast<INamedTypeSymbol>()
            .ToArray();
    }
}
