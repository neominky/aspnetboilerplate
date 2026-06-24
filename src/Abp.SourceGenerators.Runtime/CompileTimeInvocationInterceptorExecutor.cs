using System;
using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Routes <see cref="AbpInterceptorBase"/> calls for compile-time interception.
    /// Replaces Castle <c>AsyncDeterminationInterceptor</c> + <c>IInvocation.Proceed</c> wiring.
    /// Each method matches a return shape baked by the source generator (no runtime type dispatch).
    /// </summary>
    public static class CompileTimeInvocationInterceptorExecutor
    {
        public static async Task<T> ExecuteSynchronous<T>(
            AbpInterceptorBase interceptor,
            IAbpInvocation invocation,
            Func<Task<T>> target)
        {
            ConfigureSynchronousProceed(invocation, target);
            interceptor.InterceptSynchronous(invocation);
            return invocation.ReturnValue is T typedValue ? typedValue : default!;
        }

        public static async Task ExecuteAsynchronous(
            AbpInterceptorBase interceptor,
            IAbpInvocation invocation,
            Func<Task<object?>> target)
        {
            ConfigureVoidTaskProceed(invocation, target);
            interceptor.InterceptAsynchronous(invocation);
            await (Task)invocation.ReturnValue!;
        }

        public static async Task<T> ExecuteAsynchronous<T>(
            AbpInterceptorBase interceptor,
            IAbpInvocation invocation,
            Func<Task<T>> target)
        {
            ConfigureGenericTaskProceed(invocation, target);
            interceptor.InterceptAsynchronous<T>(invocation);
            return await (Task<T>)invocation.ReturnValue!;
        }

        private static void ConfigureSynchronousProceed<T>(
            IAbpInvocation invocation,
            Func<Task<T>> target)
        {
            if (invocation is not ICompileTimeInvocationProceedHost host)
            {
                return;
            }

            host.SetProceed(async () =>
            {
                var result = await target().ConfigureAwait(false);
                invocation.ReturnValue = result;
                return result;
            });
        }

        private static void ConfigureVoidTaskProceed(
            IAbpInvocation invocation,
            Func<Task<object?>> target)
        {
            if (invocation is not ICompileTimeInvocationProceedHost host)
            {
                return;
            }

            host.SetProceed(async () =>
            {
                var task = (Task)(object)await target().ConfigureAwait(false);
                invocation.ReturnValue = task;
                await task.ConfigureAwait(false);
                return null;
            });
        }

        private static void ConfigureGenericTaskProceed<T>(
            IAbpInvocation invocation,
            Func<Task<T>> target)
        {
            if (invocation is not ICompileTimeInvocationProceedHost host)
            {
                return;
            }

            host.SetProceed(async () =>
            {
                var task = target();
                invocation.ReturnValue = task;
                await task.ConfigureAwait(false);
                return null;
            });
        }
    }

    /// <summary>
    /// Implemented by compile-time invocations to wire <see cref="IAbpInvocation.Proceed"/>.
    /// </summary>
    internal interface ICompileTimeInvocationProceedHost
    {
        void SetProceed(Func<Task<object?>> proceed);
    }
}
