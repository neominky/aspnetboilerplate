using Castle.DynamicProxy;

namespace Abp.Dependency
{
    /// <summary>
    /// Castle <see cref="AsyncDeterminationInterceptor"/> wrapper for <see cref="AbpInterceptorBase"/>.
    /// Moved from <c>src/Abp/Dependency/AbpAsyncDeterminationInterceptor.cs</c>.
    /// </summary>
    public class AbpAsyncDeterminationInterceptor<TInterceptor> : AsyncDeterminationInterceptor
        where TInterceptor : AbpInterceptorBase
    {
        public AbpAsyncDeterminationInterceptor(TInterceptor interceptor)
            : base(new AbpCastleInterceptorBridge(interceptor))
        {
        }
    }
}
