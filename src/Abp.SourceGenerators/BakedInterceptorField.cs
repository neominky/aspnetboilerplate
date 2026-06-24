namespace Abp.SourceGenerators;

internal readonly struct BakedInterceptorField
{
    public BakedInterceptorField(string fieldName, string resolveExpression, string? typeName = null)
    {
        FieldName = fieldName;
        ResolveExpression = resolveExpression;
        TypeName = typeName;
    }

    public string FieldName { get; }

    public string ResolveExpression { get; }

    public string? TypeName { get; }
}
