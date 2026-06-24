using System.Threading.Tasks;

namespace Abp.Dependency
{
    public abstract class AbpInterceptorBase
    {
        public virtual void InterceptAsynchronous(IAbpInvocation invocation)
        {
            invocation.ReturnValue = InternalInterceptAsynchronous(invocation);
        }

        public virtual void InterceptAsynchronous<TResult>(IAbpInvocation invocation)
        {
            invocation.ReturnValue = InternalInterceptAsynchronous<TResult>(invocation);
        }

        public abstract void InterceptSynchronous(IAbpInvocation invocation);

        protected abstract Task InternalInterceptAsynchronous(IAbpInvocation invocation);

        protected abstract Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation);
    }
}
