namespace Abp.SourceGenerators;

internal static class GeneratedNamespace
{
    public static string Get(string? assemblyName)
    {
        return string.IsNullOrWhiteSpace(assemblyName) ? "Abp.Generated" : $"{assemblyName}.Generated";
    }
}
