using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Compatibility layer for existing <see cref="AbpInterceptorBase"/> / Castle DynamicProxy interception.
    /// </summary>
    /// <remarks>
    /// Override <c>protected Internal*</c> struct methods for allocation-free compile-time interception.
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

        public virtual void InterceptAsynchronous(ref AbpInvocationStruct<Task> invocation)
        {
            invocation.ReturnValue = InternalInterceptAsynchronous(ref invocation);
        }

        public virtual void InterceptAsynchronous<TResult>(ref AbpInvocationStruct<Task<TResult>> invocation)
        {
            invocation.ReturnValue = InternalInterceptAsynchronous<TResult>(ref invocation);
        }

        public virtual void InterceptAsynchronous(ref AbpInvocationStruct<ValueTask> invocation)
        {
            invocation.ReturnValue = InternalInterceptAsynchronous(ref invocation);
        }

        public virtual void InterceptAsynchronous<TResult>(ref AbpInvocationStruct<ValueTask<TResult>> invocation)
        {
            invocation.ReturnValue = InternalInterceptAsynchronous<TResult>(ref invocation);
        }

        protected sealed override Task InternalInterceptAsynchronous(IAbpInvocation invocation)
        {
            return RouteTaskVoid(invocation);
        }

        protected sealed override Task<TResult> InternalInterceptAsynchronous<TResult>(IAbpInvocation invocation)
        {
            return RouteTaskGeneric<TResult>(invocation);
        }

        protected virtual void InternalInterceptSynchronous(ref AbpInvocationStruct invocation)
        {
            invocation.Proceed();
        }

        protected abstract Task InternalInterceptAsynchronous(ref AbpInvocationStruct<Task> invocation);

        protected abstract Task<TResult> InternalInterceptAsynchronous<TResult>(ref AbpInvocationStruct<Task<TResult>> invocation);

        protected abstract ValueTask InternalInterceptAsynchronous(ref AbpInvocationStruct<ValueTask> invocation);

        protected abstract ValueTask<TResult> InternalInterceptAsynchronous<TResult>(ref AbpInvocationStruct<ValueTask<TResult>> invocation);

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

        private Task RouteTaskVoid(IAbpInvocation classInvocation)
        {
            var invocation = default(AbpInvocationStruct<Task>);
            InitializeStruct(ref invocation, classInvocation);
            invocation.SetProceed(() => ProceedAsTask(classInvocation));
            return InternalInterceptAsynchronous(ref invocation);
        }

        private Task<TResult> RouteTaskGeneric<TResult>(IAbpInvocation classInvocation)
        {
            var invocation = default(AbpInvocationStruct<Task<TResult>>);
            InitializeStruct(ref invocation, classInvocation);
            invocation.SetProceed(() => ProceedAsTask<TResult>(classInvocation));
            return InternalInterceptAsynchronous<TResult>(ref invocation);
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

        private static Task ProceedAsTask(IAbpInvocation invocation)
        {
            var proceedInfo = invocation.CaptureProceedInfo();
            proceedInfo.Invoke();
            return (Task)AbpInvocationReturnValueMaterializer.MaterializeForTaskCompatibility(invocation)!;
        }

        private static Task<TResult> ProceedAsTask<TResult>(IAbpInvocation invocation)
        {
            var proceedInfo = invocation.CaptureProceedInfo();
            proceedInfo.Invoke();
            return (Task<TResult>)AbpInvocationReturnValueMaterializer.MaterializeForTaskCompatibility(invocation)!;
        }
    }
}
