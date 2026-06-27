namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Per-call sync interception scratch space. Reused by generated proceed method groups
    /// so proceed delegates do not capture per-invocation lambdas.
    /// </summary>
    public sealed class SyncInvocationState
    {
        public AbpInvocationStruct Invocation;

        public AbpInvocationCompileTime? ClassBridge;

        public void Reset(
            object invocationTarget,
            in AbpInvocationMethod invocationMethod,
            object?[] arguments)
        {
            Invocation = default;
            Invocation.Initialize(invocationTarget, invocationMethod, arguments);
            ClassBridge = null;
        }
    }
}
