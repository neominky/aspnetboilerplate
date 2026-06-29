using System;
using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Routes <see cref="AbpInterceptorBase"/> calls for compile-time interception.
    /// Class-based layers reuse a pre-created <see cref="AbpInvocationCompileTime"/> per target method
    /// and baked <c>Func</c> proceed delegates (no per-call lambda allocation).
    /// </summary>
    public static class CompileTimeInvocationInterceptorExecutor
    {
        public static void RunSyncAllocationFreeLayer(
            ref AbpInvocationStruct invocation,
            IAbpInterceptorSync interceptor)
        {
            interceptor.InterceptSynchronous(ref invocation);
        }

        public static void RunSyncClassLayer(
            AbpInvocationCompileTime classInvocation,
            AbpInterceptorBase interceptor,
            Func<AbpInvocationCompileTime, object?> proceed)
        {
            classInvocation.SetSyncProceed(proceed);
            interceptor.InterceptSynchronous(classInvocation);
        }

        public static void RunSyncClassLayer(
            ref AbpInvocationStruct invocation,
            AbpInterceptorBase interceptor,
            AbpInvocationCompileTime classInvocation,
            Func<AbpInvocationCompileTime, object?> proceed)
        {
            classInvocation.PrepareForCall(invocation.Arguments, invocation.ReturnValue);
            classInvocation.SetSyncProceed(proceed);
            interceptor.InterceptSynchronous(classInvocation);
            invocation.ReturnValue = classInvocation.ReturnValue ?? invocation.ReturnValue;
        }

        public static Task<TResult> RunTaskAllocationFreeLayer<TResult>(
            ref AbpInvocationStruct<Task<TResult>> invocation,
            IAbpInterceptorTaskAsync interceptor)
        {
            return interceptor.InterceptAsynchronous<TResult>(invocation);
        }

        public static Task<TResult> RunTaskClassLayer<TResult>(
            AbpInvocationCompileTime<TResult> classInvocation,
            AbpInterceptorBase interceptor,
            Func<AbpInvocationCompileTime, Task<object?>> proceed)
        {
            classInvocation.SetProceed(proceed);
            InvokeTaskInterceptor(interceptor, classInvocation);
            return ResolveTaskReturn(classInvocation);
        }

        public static Task<TResult> RunTaskClassLayer<TResult>(
            ref AbpInvocationStruct<Task<TResult>> invocation,
            AbpInterceptorBase interceptor,
            AbpInvocationCompileTime<TResult> classInvocation,
            Func<AbpInvocationCompileTime, Task<object?>> proceed)
        {
            classInvocation.PrepareForCall(invocation.Arguments);
            classInvocation.SetProceed(proceed);
            InvokeTaskInterceptor(interceptor, classInvocation);
            return ResolveTaskReturn(classInvocation);
        }

        public static ValueTask<TResult> RunValueTaskAllocationFreeLayer<TResult>(
            ref AbpInvocationStruct<ValueTask<TResult>> invocation,
            IAbpInterceptorValueTaskAsync interceptor)
        {
            return interceptor.InterceptAsynchronous<TResult>(invocation);
        }

        public static ValueTask<TResult> RunValueTaskTaskStructLayer<TResult>(
            ref AbpInvocationStruct<ValueTask<TResult>> invocation,
            IAbpInterceptorTaskAsync interceptor)
        {
            var inv = invocation;
            var taskInvocation = default(AbpInvocationStruct<Task<TResult>>);
            taskInvocation.Initialize(
                inv.InvocationTarget,
                inv.InvocationMethod,
                inv.Arguments);
            taskInvocation.SetProceed(() => AbpInvocationReturnValueMaterializer.AsTask(inv.Proceed()));
            var taskResult = interceptor.InterceptAsynchronous<TResult>(taskInvocation);
            invocation = inv;
            return new ValueTask<TResult>(taskResult);
        }

        public static ValueTask<TResult> RunValueTaskClassLayer<TResult>(
            AbpInvocationCompileTime<TResult> classInvocation,
            AbpInterceptorBase interceptor,
            Func<AbpInvocationCompileTime, Task<object?>> proceed)
        {
            classInvocation.SetProceed(proceed);
            InvokeTaskInterceptor(interceptor, classInvocation);
            return ResolveValueTaskReturn(classInvocation);
        }

        public static ValueTask<TResult> RunValueTaskClassLayer<TResult>(
            ref AbpInvocationStruct<ValueTask<TResult>> invocation,
            AbpInterceptorBase interceptor,
            AbpInvocationCompileTime<TResult> classInvocation,
            Func<AbpInvocationCompileTime, Task<object?>> proceed)
        {
            classInvocation.PrepareForCall(invocation.Arguments);
            classInvocation.SetProceed(proceed);
            InvokeTaskInterceptor(interceptor, classInvocation);
            return ResolveValueTaskReturn(classInvocation);
        }

        private static void InvokeTaskInterceptor<TResult>(
            AbpInterceptorBase interceptor,
            AbpInvocationCompileTime<TResult> classInvocation)
        {
            if (typeof(TResult) == typeof(AbpUnit))
            {
                interceptor.InterceptAsynchronous(classInvocation);
            }
            else
            {
                interceptor.InterceptAsynchronous<TResult>(classInvocation);
            }
        }

        public static Task<TResult> ResolveTaskReturn<TResult>(AbpInvocationCompileTime<TResult> classInvocation)
        {
            if (classInvocation.ReturnValue is Task<TResult> task)
            {
                return task;
            }

            if (typeof(TResult) == typeof(AbpUnit) && classInvocation.ReturnValue is Task voidTask)
            {
                return (Task<TResult>)(object)AbpAsyncCoercion.FromVoidTask(voidTask);
            }

            throw new AbpException(
                $"ReturnValue must be Task<{typeof(TResult).FullName}>, but was: {classInvocation.ReturnValue?.GetType().FullName ?? "null"}.");
        }

        public static ValueTask<TResult> ResolveValueTaskReturn<TResult>(AbpInvocationCompileTime<TResult> classInvocation)
        {
            if (classInvocation.ReturnValue is Task<TResult> task)
            {
                return new ValueTask<TResult>(task);
            }

            if (typeof(TResult) == typeof(AbpUnit) && classInvocation.ReturnValue is Task voidTask)
            {
                return new ValueTask<TResult>((Task<TResult>)(object)AbpAsyncCoercion.FromVoidTask(voidTask));
            }

            if (classInvocation.ValueTaskReturnValue != default)
            {
                return classInvocation.ValueTaskReturnValue;
            }

            return default;
        }
    }
}
