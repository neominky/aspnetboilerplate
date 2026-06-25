using Microsoft.CodeAnalysis;

namespace Abp.SourceGenerators;

internal static class CompileTimeDiagnostics
{
    public const string MissingInterceptorTriggerAttributeId = "ABPCT001";

    public static readonly DiagnosticDescriptor MissingInterceptorTriggerAttribute = new(
        MissingInterceptorTriggerAttributeId,
        title: "User interceptor is missing AbpInterceptorAttribute",
        messageFormat: "Compile-time user interceptor '{0}' must declare at least one trigger attribute using [AbpInterceptor(typeof(YourAttribute))]",
        category: "Abp.CompileTime",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
