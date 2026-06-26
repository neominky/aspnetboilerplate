namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Compile-time allocation-free synchronous interception.
    /// </summary>
    public interface IAbpInterceptorSync
    {
        void InterceptSynchronous(ref AbpInvocationStruct invocation);
    }
}
