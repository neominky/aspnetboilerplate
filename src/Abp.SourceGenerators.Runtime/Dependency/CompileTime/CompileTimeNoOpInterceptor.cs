using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Pass-through interceptor used when a built-in interceptor is disabled via
    /// <see cref="Abp.AbpBootstrapperInterceptorOptions"/> at compile-time interception startup.
    /// </summary>
    public sealed class CompileTimeNoOpInterceptor : AbpInterceptorBase
    {
        public static CompileTimeNoOpInterceptor Instance { get; } = new();

        private CompileTimeNoOpInterceptor()
        {
        }

        public override void InterceptSynchronous(IAbpInvocation invocation)
        {
            invocation.Proceed();
        }

        protected override Task InternalInterceptAsynchronous(IAbpInvocation invocation)
        {
            invocation.Proceed();
            return (Task)invocation.ReturnValue!;
        }

        protected override Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation)
        {
            invocation.Proceed();
            return (Task<TResult>)invocation.ReturnValue!;
        }
    }
}
