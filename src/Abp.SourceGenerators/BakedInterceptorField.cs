namespace Abp.SourceGenerators;

internal readonly struct BakedInterceptorField
{
    public BakedInterceptorField(
        string fieldName,
        string resolveExpression,
        string? typeName = null,
        ValueTaskLayerKind valueTaskLayerKind = ValueTaskLayerKind.ClassInvocation,
        TaskLayerKind taskLayerKind = TaskLayerKind.ClassInvocation,
        SyncLayerKind syncLayerKind = SyncLayerKind.ClassInvocation)
    {
        FieldName = fieldName;
        ResolveExpression = resolveExpression;
        TypeName = typeName;
        ValueTaskLayerKind = valueTaskLayerKind;
        TaskLayerKind = taskLayerKind;
        SyncLayerKind = syncLayerKind;
    }

    public string FieldName { get; }

    public string ResolveExpression { get; }

    public string? TypeName { get; }

    public ValueTaskLayerKind ValueTaskLayerKind { get; }

    public TaskLayerKind TaskLayerKind { get; }

    public SyncLayerKind SyncLayerKind { get; }
}
