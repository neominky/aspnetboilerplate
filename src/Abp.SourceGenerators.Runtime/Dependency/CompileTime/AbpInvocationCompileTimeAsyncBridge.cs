using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Adapts stack-based struct invocations to class-based <see cref="AbpInterceptorBase"/> async interception.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Compile-time interception runs async methods through <see cref="AbpInvocationStruct{TAsync}"/> (allocation-free).
    /// Built-in and user interceptors, however, still implement
    /// <see cref="AbpInterceptorBase.InternalInterceptAsynchronous(IAbpInvocation)"/> on heap
    /// <see cref="AbpInvocationCompileTime"/> instances.
    /// </para>
    /// <para>
    /// This bridge is invoked by
    /// <see cref="CompileTimeInvocationInterceptorExecutor.RunTaskViaClassBridgeLayer"/> and
    /// <see cref="CompileTimeInvocationInterceptorExecutor.RunValueTaskViaClassBridgeLayer"/> when a layer must
    /// call <see cref="AbpInterceptorBase"/> instead of struct <c>InterceptAsynchronous</c> overloads.
    /// It wires struct <c>Proceed</c> into the class bridge, runs the interceptor, then copies the result back
    /// into <c>invocation.ReturnValue</c>.
    /// </para>
    /// <para>
    /// For <see cref="ValueTask"/> layers, proceed results are materialized to <see cref="Task"/> via
    /// <see cref="AbpInvocationReturnValueMaterializer"/> and exposed through
    /// <see cref="AbpInvocationCompileTimeTaskCompatible"/> so existing Task-based interceptors can run without
    /// invalid <see cref="ValueTask"/> casts. The Task is stored on <see cref="AbpInvocationCompileTime.ReturnValue"/>
    /// so later interceptors can await it again; <see cref="ValueTask"/> must not be awaited and then re-read.
    /// </para>
    /// </remarks>
    internal static class AbpInvocationCompileTimeAsyncBridge
    {
        public static void InterceptTaskVoid(
            ref AbpInvocationStruct<Task> invocation,
            AbpInterceptorBase interceptor,
            ref AbpInvocationCompileTime? classBridge)
        {
            if (interceptor is IAbpInterceptorTaskAsync taskAsync)
            {
                taskAsync.InterceptAsynchronous(ref invocation);
                return;
            }

            var bridge = AbpInvocationCompileTime.EnsureClassBridge(ref invocation, ref classBridge);
            ConfigureVoidTaskBridgeProceed(invocation, bridge);
            interceptor.InterceptAsynchronous(bridge);
            invocation.ReturnValue = ResolveVoidTaskReturn(bridge);
        }

        public static void InterceptTaskGeneric<T>(
            ref AbpInvocationStruct<Task<T>> invocation,
            AbpInterceptorBase interceptor,
            ref AbpInvocationCompileTime<T>? classBridge)
        {
            if (interceptor is IAbpInterceptorTaskAsync taskAsync)
            {
                taskAsync.InterceptAsynchronous(ref invocation);
                return;
            }

            var bridge = AbpInvocationCompileTime<T>.EnsureClassBridge(ref invocation, ref classBridge);
            ConfigureGenericTaskBridgeProceed(invocation, bridge);
            interceptor.InterceptAsynchronous<T>(bridge);
            invocation.ReturnValue = ResolveGenericTaskReturn(bridge);
        }

        public static void InterceptValueTaskVoid(
            ref AbpInvocationStruct<ValueTask> invocation,
            AbpInterceptorBase interceptor,
            ref AbpInvocationCompileTime? classBridge)
        {
            if (interceptor is IAbpInterceptorValueTaskAsync valueTaskAsync)
            {
                valueTaskAsync.InterceptAsynchronous(ref invocation);
                return;
            }

            var bridge = AbpInvocationCompileTime.EnsureClassBridge(ref invocation, ref classBridge);
            ConfigureVoidValueTaskBridgeProceed(invocation, bridge);
            interceptor.InterceptAsynchronous(new AbpInvocationCompileTimeTaskCompatible(bridge));
            invocation.ReturnValue = ResolveVoidValueTaskReturn(bridge);
        }

        public static void InterceptValueTaskGeneric<T>(
            ref AbpInvocationStruct<ValueTask<T>> invocation,
            AbpInterceptorBase interceptor,
            ref AbpInvocationCompileTime<T>? classBridge)
        {
            if (interceptor is IAbpInterceptorValueTaskAsync valueTaskAsync)
            {
                valueTaskAsync.InterceptAsynchronous(ref invocation);
                return;
            }

            var bridge = AbpInvocationCompileTime<T>.EnsureClassBridge(ref invocation, ref classBridge);
            ConfigureGenericValueTaskBridgeProceed(invocation, bridge);
            interceptor.InterceptAsynchronous<T>(new AbpInvocationCompileTimeTaskCompatible(bridge));
            invocation.ReturnValue = ResolveGenericValueTaskReturn(bridge);
        }

        private static void ConfigureVoidTaskBridgeProceed(
            AbpInvocationStruct<Task> invocation,
            AbpInvocationCompileTime bridge)
        {
            bridge.SetProceed(async () =>
            {
                var task = invocation.Proceed();
                bridge.ReturnValue = task;
                await task.ConfigureAwait(false);
                return null;
            });
        }

        private static void ConfigureGenericTaskBridgeProceed<T>(
            AbpInvocationStruct<Task<T>> invocation,
            AbpInvocationCompileTime<T> bridge)
        {
            bridge.SetProceed(async () =>
            {
                var task = invocation.Proceed();
                bridge.ReturnValue = task;
                var result = await task.ConfigureAwait(false);
                return result;
            });
        }

        private static void ConfigureVoidValueTaskBridgeProceed(
            AbpInvocationStruct<ValueTask> invocation,
            AbpInvocationCompileTime bridge)
        {
            bridge.SetProceed(async () =>
            {
                var valueTask = invocation.Proceed();
                var task = AbpInvocationReturnValueMaterializer.AsTask(valueTask);
                bridge.ReturnValue = task;
                await task.ConfigureAwait(false);
                return null;
            });
        }

        private static void ConfigureGenericValueTaskBridgeProceed<T>(
            AbpInvocationStruct<ValueTask<T>> invocation,
            AbpInvocationCompileTime<T> bridge)
        {
            bridge.SetProceed(async () =>
            {
                var valueTask = invocation.Proceed();
                var task = AbpInvocationReturnValueMaterializer.AsTask(valueTask);
                bridge.ReturnValue = task;
                var result = await task.ConfigureAwait(false);
                return result;
            });
        }

        private static Task ResolveVoidTaskReturn(AbpInvocationCompileTime bridge)
        {
            if (bridge.ReturnValue is Task task)
            {
                return task;
            }

            return Task.CompletedTask;
        }

        private static Task<T> ResolveGenericTaskReturn<T>(AbpInvocationCompileTime<T> bridge)
        {
            if (bridge.ReturnValue is Task<T> task)
            {
                return task;
            }

            throw new AbpException(
                $"ReturnValue must be Task<{typeof(T).FullName}>, but was: {bridge.ReturnValue?.GetType().FullName ?? "null"}.");
        }

        private static ValueTask ResolveVoidValueTaskReturn(AbpInvocationCompileTime bridge)
        {
            if (bridge.ReturnValue is Task task)
            {
                return new ValueTask(task);
            }

            if (bridge.ValueTaskReturnValue != default)
            {
                return bridge.ValueTaskReturnValue;
            }

            return default;
        }

        private static ValueTask<T> ResolveGenericValueTaskReturn<T>(AbpInvocationCompileTime<T> bridge)
        {
            if (bridge.ReturnValue is Task<T> task)
            {
                return new ValueTask<T>(task);
            }

            if (bridge.ValueTaskReturnValue != default)
            {
                return bridge.ValueTaskReturnValue;
            }

            return default;
        }
    }
}
