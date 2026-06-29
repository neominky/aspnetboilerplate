using System;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    public class AbpInvocationCompileTime : IAbpInvocation,
        IAbpInterceptorValueTaskReturnHost, IAbpInterceptorValueTaskReturnSource
    {
        private object _invocationTarget = null!;
        private MethodInfo _method = null!;
        private object?[] _arguments = null!;
        private Func<object?>? _proceedSync;
        private Func<AbpInvocationCompileTime, object?>? _proceedSyncWithInvocation;
        private Func<AbpInvocationCompileTime, Task<object?>>? _proceedAsyncWithInvocation;
        private AbpInvocationStructHolder? _mixedStructHolder;

        public AbpInvocationCompileTime(
            object invocationTarget,
            MethodInfo method,
            object?[] arguments,
            bool wrapMethodWithMetadata = true)
        {
            Reinitialize(invocationTarget, method, arguments, wrapMethodWithMetadata);
        }

        public object InvocationTarget => _invocationTarget;

        public Type TargetType { get; private set; } = null!;

        public MethodInfo MethodInvocationTarget { get; private set; } = null!;

        public MethodInfo Method => _method;

        public object?[] Arguments => _arguments;

        public object? ReturnValue { get; set; }

        public ValueTask ValueTaskReturnValue { get; set; }

        public AbpInvocationStructHolder? MixedStructHolder => _mixedStructHolder;

        public void AttachMixedStructHolder(AbpInvocationStructHolder holder)
        {
            _mixedStructHolder = holder;
        }

        public void DetachMixedStructHolder()
        {
            _mixedStructHolder = null;
        }

        internal void Reinitialize(
            object invocationTarget,
            MethodInfo method,
            object?[] arguments,
            bool wrapMethodWithMetadata = true)
        {
            _invocationTarget = invocationTarget;
            _method = method;
            MethodInvocationTarget = wrapMethodWithMetadata
                ? AbpMethodInfo.GetInvocationMethod(method)
                : method;
            TargetType = method.DeclaringType!;
            _arguments = arguments;
            ReturnValue = null;
            ValueTaskReturnValue = default;
            _proceedSync = null;
            _proceedSyncWithInvocation = null;
            _proceedAsyncWithInvocation = null;
            _mixedStructHolder = null;
        }

        internal void ResetForPool()
        {
            Reinitialize(_invocationTarget, _method, _arguments, wrapMethodWithMetadata: MethodInvocationTarget != _method);
        }

        public void PrepareForCall(object?[] arguments, object? returnValue = null)
        {
            _arguments = arguments;
            ReturnValue = returnValue;
            ValueTaskReturnValue = default;
            _proceedSync = null;
            _proceedSyncWithInvocation = null;
            _proceedAsyncWithInvocation = null;
        }

        public void SetSyncProceed(Func<object?> proceed)
        {
            _proceedSync = proceed;
            _proceedSyncWithInvocation = null;
            _proceedAsyncWithInvocation = null;
        }

        public void SetSyncProceed(Func<AbpInvocationCompileTime, object?> proceed)
        {
            _proceedSyncWithInvocation = proceed;
            _proceedSync = null;
            _proceedAsyncWithInvocation = null;
        }

        public void SetProceed(Func<Task<object?>> proceed)
        {
            SetProceed(_ => proceed());
        }

        public void SetProceed(Func<AbpInvocationCompileTime, Task<object?>> proceed)
        {
            _proceedAsyncWithInvocation = proceed;
            _proceedSync = null;
            _proceedSyncWithInvocation = null;
        }

        public void Proceed()
        {
            if (_proceedSyncWithInvocation != null)
            {
                ReturnValue = _proceedSyncWithInvocation(this);
                return;
            }

            if (_proceedSync != null)
            {
                ReturnValue = _proceedSync();
                return;
            }

            if (_proceedAsyncWithInvocation == null)
            {
                throw new InvalidOperationException("Proceed is not configured.");
            }

            ReturnValue = _proceedAsyncWithInvocation(this).GetAwaiter().GetResult();
        }

        public IAbpProceedInfo CaptureProceedInfo() => new AbpInvocationCompileTimeProceedInfo(this);

        public MethodInfo GetConcreteMethod() => _method;

        public object? GetValueTaskReturnForCompatibility() => ValueTaskReturnValue;
    }

    public sealed class AbpInvocationCompileTime<TResult> : AbpInvocationCompileTime,
        IAbpInterceptorValueTaskReturnHost<TResult>, IAbpInterceptorGenericValueTaskBridge
    {
        public AbpInvocationCompileTime(
            object invocationTarget,
            MethodInfo method,
            object?[] arguments,
            bool wrapMethodWithMetadata = true)
            : base(invocationTarget, method, arguments, wrapMethodWithMetadata)
        {
        }

        public new ValueTask<TResult> ValueTaskReturnValue { get; set; }

        public void PrepareForCall(object?[] arguments, object? returnValue = null, ValueTask<TResult> valueTaskReturnValue = default)
        {
            base.PrepareForCall(arguments, returnValue);
            ValueTaskReturnValue = valueTaskReturnValue;
        }

        internal new void Reinitialize(
            object invocationTarget,
            MethodInfo method,
            object?[] arguments,
            bool wrapMethodWithMetadata = true)
        {
            base.Reinitialize(invocationTarget, method, arguments, wrapMethodWithMetadata);
            ValueTaskReturnValue = default;
        }

        public Task MaterializeReturnAsTask()
        {
            if (ReturnValue is Task<TResult> task)
            {
                return task;
            }

            return AbpInvocationReturnValueMaterializer.AsTask(ValueTaskReturnValue);
        }
    }

    public sealed class AbpInvocationCompileTimeProceedInfo : IAbpProceedInfo
    {
        private readonly AbpInvocationCompileTime _invocation;

        public AbpInvocationCompileTimeProceedInfo(AbpInvocationCompileTime invocation)
        {
            _invocation = invocation;
        }

        public void Invoke()
        {
            _invocation.Proceed();
        }
    }
}
