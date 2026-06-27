using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Adapts stack-based struct invocations to class-based <see cref="AbpInterceptorBase"/> async interception.
    /// </summary>
    public static class AbpInvocationCompileTimeAsyncBridge
    {
        public static Task<TResult> InterceptTask<TResult>(
            ref AbpInvocationStruct<Task<TResult>> invocation,
            AbpInterceptorBase interceptor,
            ref AbpInvocationCompileTime<TResult>? classBridge)
        {
            if (interceptor is IAbpInterceptorTaskAsync taskAsync)
            {
                return taskAsync.InterceptAsynchronous<TResult>(invocation);
            }

            var bridge = AbpInvocationCompileTime<TResult>.EnsureClassBridge(ref invocation, ref classBridge);
            ConfigureTaskProceed(invocation, bridge);

            if (typeof(TResult) == typeof(AbpUnit))
            {
                interceptor.InterceptAsynchronous(bridge);
            }
            else
            {
                interceptor.InterceptAsynchronous<TResult>(bridge);
            }

            return ResolveTaskReturn(bridge);
        }

        public static ValueTask<TResult> InterceptValueTask<TResult>(
            ref AbpInvocationStruct<ValueTask<TResult>> invocation,
            AbpInterceptorBase interceptor,
            ref AbpInvocationCompileTime<TResult>? classBridge)
        {
            if (interceptor is IAbpInterceptorValueTaskAsync valueTaskAsync)
            {
                return valueTaskAsync.InterceptAsynchronous<TResult>(invocation);
            }

            var bridge = AbpInvocationCompileTime<TResult>.EnsureClassBridge(ref invocation, ref classBridge);
            ConfigureValueTaskProceed(invocation, bridge);
            var compatibleBridge = new AbpInvocationCompileTimeTaskCompatible(bridge);

            if (typeof(TResult) == typeof(AbpUnit))
            {
                interceptor.InterceptAsynchronous(compatibleBridge);
            }
            else
            {
                interceptor.InterceptAsynchronous<TResult>(compatibleBridge);
            }

            return ResolveValueTaskReturn(bridge);
        }

        public static void ConfigureTaskProceed<TResult>(
            AbpInvocationStruct<Task<TResult>> invocation,
            AbpInvocationCompileTime<TResult> bridge)
        {
            bridge.SetProceed(async () =>
            {
                var task = invocation.Proceed();
                bridge.ReturnValue = task;
                var result = await task.ConfigureAwait(false);
                return (object?)result;
            });
        }

        public static void ConfigureValueTaskProceed<TResult>(
            AbpInvocationStruct<ValueTask<TResult>> invocation,
            AbpInvocationCompileTime<TResult> bridge)
        {
            bridge.SetProceed(async () =>
            {
                var valueTask = invocation.Proceed();
                var task = AbpInvocationReturnValueMaterializer.AsTask(valueTask);
                bridge.ReturnValue = task;
                var result = await task.ConfigureAwait(false);
                return (object?)result;
            });
        }

        public static Task<TResult> ResolveTaskReturn<TResult>(AbpInvocationCompileTime<TResult> bridge)
        {
            if (bridge.ReturnValue is Task<TResult> task)
            {
                return task;
            }

            if (typeof(TResult) == typeof(AbpUnit) && bridge.ReturnValue is Task voidTask)
            {
                return (Task<TResult>)(object)AbpAsyncCoercion.FromVoidTask(voidTask);
            }

            throw new AbpException(
                $"ReturnValue must be Task<{typeof(TResult).FullName}>, but was: {bridge.ReturnValue?.GetType().FullName ?? "null"}.");
        }

        public static ValueTask<TResult> ResolveValueTaskReturn<TResult>(AbpInvocationCompileTime<TResult> bridge)
        {
            if (bridge.ReturnValue is Task<TResult> task)
            {
                return new ValueTask<TResult>(task);
            }

            if (typeof(TResult) == typeof(AbpUnit) && bridge.ReturnValue is Task voidTask)
            {
                return new ValueTask<TResult>((Task<TResult>)(object)AbpAsyncCoercion.FromVoidTask(voidTask));
            }

            if (bridge.ValueTaskReturnValue != default)
            {
                return bridge.ValueTaskReturnValue;
            }

            return default;
        }
    }
}
