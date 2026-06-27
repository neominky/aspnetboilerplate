using System;
using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Routes <see cref="AbpInterceptorBase"/> calls for compile-time interception.
    /// Replaces Castle <c>AsyncDeterminationInterceptor</c> + <c>IInvocation.Proceed</c> wiring.
    /// </summary>
    public static class CompileTimeInvocationInterceptorExecutor
    {
        public static void RunSyncAllocationFreeLayer(
            ref AbpInvocationStruct invocation,
            IAbpInterceptorSync interceptor,
            Func<Task<object?>> next)
        {
            invocation.SetSyncProceed(next);
            interceptor.InterceptSynchronous(ref invocation);
        }

        public static void RunSyncLayer(
            ref AbpInvocationStruct invocation,
            AbpInterceptorBase interceptor,
            Func<Task<object?>> next)
        {
            invocation.SetSyncProceed(next);

            var bridge = new AbpInvocationCompileTime(
                invocation.InvocationTarget,
                invocation.Method,
                invocation.Arguments);
            bridge.ReturnValue = invocation.ReturnValue;
            bridge.SetProceed(async () =>
            {
                var result = await next();
                bridge.ReturnValue = result;
                return result;
            });

            interceptor.InterceptSynchronous(bridge);
            invocation.ReturnValue = bridge.ReturnValue ?? invocation.ReturnValue;
        }

        public static Task<TResult> RunTaskAllocationFreeLayer<TResult>(
            ref AbpInvocationStruct<Task<TResult>> invocation,
            IAbpInterceptorTaskAsync interceptor,
            Func<Task<TResult>> next)
        {
            invocation.SetProceed(next);
            return interceptor.InterceptAsynchronous<TResult>(invocation);
        }

        public static Task<TResult> RunTaskViaClassBridgeLayer<TResult>(
            ref AbpInvocationStruct<Task<TResult>> invocation,
            AbpInterceptorBase interceptor,
            ref AbpInvocationCompileTime<TResult>? classBridge,
            Func<Task<TResult>> next)
        {
            invocation.SetProceed(next);
            return AbpInvocationCompileTimeAsyncBridge.InterceptTask(ref invocation, interceptor, ref classBridge);
        }

        public static ValueTask<TResult> RunValueTaskAllocationFreeLayer<TResult>(
            ref AbpInvocationStruct<ValueTask<TResult>> invocation,
            IAbpInterceptorValueTaskAsync interceptor,
            Func<ValueTask<TResult>> next)
        {
            invocation.SetProceed(next);
            return interceptor.InterceptAsynchronous<TResult>(invocation);
        }

        public static ValueTask<TResult> RunValueTaskViaTaskStructLayer<TResult>(
            ref AbpInvocationStruct<ValueTask<TResult>> invocation,
            IAbpInterceptorTaskAsync interceptor,
            Func<ValueTask<TResult>> next)
        {
            invocation.SetProceed(next);

            var valueTaskInvocation = invocation;
            var taskInvocation = default(AbpInvocationStruct<Task<TResult>>);
            taskInvocation.Initialize(
                valueTaskInvocation.InvocationTarget,
                valueTaskInvocation.InvocationMethod,
                valueTaskInvocation.Arguments);
            taskInvocation.SetProceed(() => AbpInvocationReturnValueMaterializer.AsTask(valueTaskInvocation.Proceed()));
            var taskResult = interceptor.InterceptAsynchronous<TResult>(taskInvocation);
            return new ValueTask<TResult>(taskResult);
        }

        public static ValueTask<TResult> RunValueTaskViaClassBridgeLayer<TResult>(
            ref AbpInvocationStruct<ValueTask<TResult>> invocation,
            AbpInterceptorBase interceptor,
            ref AbpInvocationCompileTime<TResult>? classBridge,
            Func<ValueTask<TResult>> next)
        {
            invocation.SetProceed(next);
            return AbpInvocationCompileTimeAsyncBridge.InterceptValueTask(ref invocation, interceptor, ref classBridge);
        }
    }
}
