using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Compatibility layer for existing <see cref="AbpInterceptorBase"/> / Castle DynamicProxy interception.
    /// </summary>
    /// <remarks>
    /// Override <c>protected Internal*</c> struct methods for allocation-free compile-time interception.
    /// <para>Sync fast path: <c>ref</c> parameter + <see cref="AbpInvocationStruct.Proceed"/> (updates <see cref="AbpInvocationStruct.ReturnValue"/> in place).</para>
    /// <para>Async fast path: by-value struct + <c>return await invocation.Proceed()</c> (or <c>return invocation.Proceed()</c> without <c>async</c> when the interceptor has no <c>await</c>).</para>
    /// <para><see cref="AbpInvocationStruct{TAsync}.CaptureProceedInfo"/> / <see cref="AbpStructProceedInfo{TAsync}"/> remain for porting built-in <see cref="IAbpInvocation"/> interceptors.</para>
    /// Legacy <see cref="IAbpInvocation"/> entry points adapt into stack structs and forward to those methods.
    /// </remarks>
    public abstract class AbpInterceptorBaseAllocationFree : AbpInterceptorBase,
        IAbpInterceptorSync, IAbpInterceptorTaskAsync, IAbpInterceptorValueTaskAsync
    {
        public virtual void InterceptSynchronous(ref AbpInvocationStruct invocation)
        {
            InternalInterceptSynchronous(ref invocation);
        }

        public sealed override void InterceptSynchronous(IAbpInvocation invocation)
        {
            RouteSync(invocation);
        }

        public virtual Task<TResult> InterceptAsynchronous<TResult>(AbpInvocationStruct<Task<TResult>> invocation)
        {
            return InternalInterceptAsynchronous<TResult>(invocation);
        }

        public virtual ValueTask<TResult> InterceptAsynchronous<TResult>(AbpInvocationStruct<ValueTask<TResult>> invocation)
        {
            return InternalInterceptAsynchronous<TResult>(invocation);
        }

        protected sealed override Task InternalInterceptAsynchronous(IAbpInvocation invocation)
        {
            return RouteTask<AbpUnit>(invocation);
        }

        protected sealed override Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation)
        {
            if (typeof(TResult) == typeof(AbpUnit))
            {
                return (Task<TResult>)(object)InternalInterceptAsynchronous(invocation);
            }

            return RouteTask<TResult>(invocation);
        }

        protected virtual void InternalInterceptSynchronous(ref AbpInvocationStruct invocation)
        {
            invocation.Proceed();
        }

        protected virtual Task<TResult> InternalInterceptAsynchronous<TResult>(AbpInvocationStruct<Task<TResult>> invocation)
        {
            return invocation.Proceed();
        }

        protected virtual ValueTask<TResult> InternalInterceptAsynchronous<TResult>(AbpInvocationStruct<ValueTask<TResult>> invocation)
        {
            return invocation.Proceed();
        }

        private void RouteSync(IAbpInvocation classInvocation)
        {
            var invocation = default(AbpInvocationStruct);
            InitializeStruct(ref invocation, classInvocation);
            invocation.SetSyncProceed(() =>
            {
                var proceedInfo = classInvocation.CaptureProceedInfo();
                proceedInfo.Invoke();
                return Task.FromResult<object?>(classInvocation.ReturnValue);
            });
            InternalInterceptSynchronous(ref invocation);
            classInvocation.ReturnValue = invocation.ReturnValue;
        }

        private Task<TResult> RouteTask<TResult>(IAbpInvocation classInvocation)
        {
            var invocation = default(AbpInvocationStruct<Task<TResult>>);
            InitializeStruct(ref invocation, classInvocation);
            invocation.SetProceed(() => ProceedAsTask<TResult>(classInvocation));
            return InternalInterceptAsynchronous<TResult>(invocation);
        }

        private static void InitializeStruct(ref AbpInvocationStruct structInvocation, IAbpInvocation classInvocation)
        {
            AbpMethodInfo.TryGetMetadata(classInvocation.Method, out var metadata);
            structInvocation.Initialize(
                classInvocation.InvocationTarget,
                new AbpInvocationMethod(classInvocation.Method, metadata),
                classInvocation.Arguments);
        }

        private static void InitializeStruct<TAsync>(ref AbpInvocationStruct<TAsync> structInvocation, IAbpInvocation classInvocation)
        {
            AbpMethodInfo.TryGetMetadata(classInvocation.Method, out var metadata);
            structInvocation.Initialize(
                classInvocation.InvocationTarget,
                new AbpInvocationMethod(classInvocation.Method, metadata),
                classInvocation.Arguments);
        }

        private static Task<TResult> ProceedAsTask<TResult>(IAbpInvocation invocation)
        {
            var proceedInfo = invocation.CaptureProceedInfo();
            proceedInfo.Invoke();

            if (invocation.ReturnValue is Task<TResult> typedTask)
            {
                return typedTask;
            }

            if (typeof(TResult) == typeof(AbpUnit) && invocation.ReturnValue is Task voidTask)
            {
                return (Task<TResult>)(object)AbpAsyncCoercion.FromVoidTask(voidTask);
            }

            throw new AbpException(
                $"ReturnValue must be Task<{typeof(TResult).FullName}> or Task, but was: {invocation.ReturnValue?.GetType().FullName ?? "null"}.");
        }
    }
}
