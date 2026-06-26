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

        public static Task RunTaskAllocationFreeLayer(
            ref AbpInvocationStruct<Task> invocation,
            IAbpInterceptorTaskAsync interceptor,
            Func<Task> next)
        {
            invocation.SetProceed(next);
            interceptor.InterceptAsynchronous(ref invocation);
            return invocation.ReturnValue;
        }

        public static Task<T> RunTaskAllocationFreeLayer<T>(
            ref AbpInvocationStruct<Task<T>> invocation,
            IAbpInterceptorTaskAsync interceptor,
            Func<Task<T>> next)
        {
            invocation.SetProceed(next);
            interceptor.InterceptAsynchronous(ref invocation);
            return invocation.ReturnValue;
        }

        public static Task RunTaskViaClassBridgeLayer(
            ref AbpInvocationStruct<Task> invocation,
            AbpInterceptorBase interceptor,
            ref AbpInvocationCompileTime? classBridge,
            Func<Task> next)
        {
            invocation.SetProceed(next);
            AbpInvocationCompileTimeAsyncBridge.InterceptTaskVoid(ref invocation, interceptor, ref classBridge);
            return invocation.ReturnValue;
        }

        public static Task<T> RunTaskViaClassBridgeLayer<T>(
            ref AbpInvocationStruct<Task<T>> invocation,
            AbpInterceptorBase interceptor,
            ref AbpInvocationCompileTime<T>? classBridge,
            Func<Task<T>> next)
        {
            invocation.SetProceed(next);
            AbpInvocationCompileTimeAsyncBridge.InterceptTaskGeneric(ref invocation, interceptor, ref classBridge);
            return invocation.ReturnValue;
        }

        public static ValueTask RunValueTaskAllocationFreeLayer(
            ref AbpInvocationStruct<ValueTask> invocation,
            IAbpInterceptorValueTaskAsync interceptor,
            Func<ValueTask> next)
        {
            invocation.SetProceed(next);
            interceptor.InterceptAsynchronous(ref invocation);
            return invocation.ReturnValue;
        }

        public static ValueTask<T> RunValueTaskAllocationFreeLayer<T>(
            ref AbpInvocationStruct<ValueTask<T>> invocation,
            IAbpInterceptorValueTaskAsync interceptor,
            Func<ValueTask<T>> next)
        {
            invocation.SetProceed(next);
            interceptor.InterceptAsynchronous(ref invocation);
            return invocation.ReturnValue;
        }

        public static ValueTask RunValueTaskViaTaskStructLayer(
            ref AbpInvocationStruct<ValueTask> invocation,
            IAbpInterceptorTaskAsync interceptor,
            Func<ValueTask> next)
        {
            invocation.SetProceed(next);

            var valueTaskInvocation = invocation;
            var taskInvocation = default(AbpInvocationStruct<Task>);
            taskInvocation.Initialize(
                valueTaskInvocation.InvocationTarget,
                valueTaskInvocation.InvocationMethod,
                valueTaskInvocation.Arguments);
            taskInvocation.SetProceed(() => AbpInvocationReturnValueMaterializer.AsTask(valueTaskInvocation.Proceed()));
            interceptor.InterceptAsynchronous(ref taskInvocation);
            invocation.ReturnValue = new ValueTask(taskInvocation.ReturnValue);
            return invocation.ReturnValue;
        }

        public static ValueTask<T> RunValueTaskViaTaskStructLayer<T>(
            ref AbpInvocationStruct<ValueTask<T>> invocation,
            IAbpInterceptorTaskAsync interceptor,
            Func<ValueTask<T>> next)
        {
            invocation.SetProceed(next);

            var valueTaskInvocation = invocation;
            var taskInvocation = default(AbpInvocationStruct<Task<T>>);
            taskInvocation.Initialize(
                valueTaskInvocation.InvocationTarget,
                valueTaskInvocation.InvocationMethod,
                valueTaskInvocation.Arguments);
            taskInvocation.SetProceed(() => AbpInvocationReturnValueMaterializer.AsTask(valueTaskInvocation.Proceed()));
            interceptor.InterceptAsynchronous(ref taskInvocation);
            invocation.ReturnValue = new ValueTask<T>(taskInvocation.ReturnValue);
            return invocation.ReturnValue;
        }

        public static ValueTask RunValueTaskViaClassBridgeLayer(
            ref AbpInvocationStruct<ValueTask> invocation,
            AbpInterceptorBase interceptor,
            ref AbpInvocationCompileTime? classBridge,
            Func<ValueTask> next)
        {
            invocation.SetProceed(next);
            AbpInvocationCompileTimeAsyncBridge.InterceptValueTaskVoid(ref invocation, interceptor, ref classBridge);
            return invocation.ReturnValue;
        }

        public static ValueTask<T> RunValueTaskViaClassBridgeLayer<T>(
            ref AbpInvocationStruct<ValueTask<T>> invocation,
            AbpInterceptorBase interceptor,
            ref AbpInvocationCompileTime<T>? classBridge,
            Func<ValueTask<T>> next)
        {
            invocation.SetProceed(next);
            AbpInvocationCompileTimeAsyncBridge.InterceptValueTaskGeneric(ref invocation, interceptor, ref classBridge);
            return invocation.ReturnValue;
        }
    }
}
