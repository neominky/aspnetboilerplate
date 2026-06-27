namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Sentinel type for async methods that return <c>Task</c> or <c>ValueTask</c> without a result.
    /// Used to unify compile-time allocation-free interceptor async APIs.
    /// </summary>
    public readonly struct AbpUnit
    {
    }
}
