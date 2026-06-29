using System;
using System.Reflection;
using System.Threading.Tasks;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    public delegate void AbpSyncChainStepDelegate(ref AbpInvocationStruct invocation);

    public delegate TAsync AbpAsyncChainStepDelegate<TAsync>(ref AbpInvocationStruct<TAsync> invocation);

    /// <summary>
    /// Compatibility handle for porting <see cref="IAbpInvocation.CaptureProceedInfo"/> call sites to struct interception.
    /// Not required for new code — use <see cref="AbpInvocationStruct.Proceed"/> (fast path) on the sync path instead.
    /// </summary>
    public readonly struct AbpStructSyncProceedInfo
    {
        private readonly Func<object?> _proceed;

        internal AbpStructSyncProceedInfo(Func<object?> proceed)
        {
            _proceed = proceed;
        }

        public void Invoke()
        {
            _proceed();
        }
    }

    /// <summary>
    /// Compatibility handle for porting <see cref="IAbpInvocation.CaptureProceedInfo"/> call sites to struct interception.
    /// Not required for new code — prefer <see cref="AbpInvocationStruct{TAsync}.Proceed"/> (fast path).
    /// Only needed when translating existing interceptors that capture proceed before an <c>await</c> and invoke after.
    /// </summary>
    public readonly struct AbpStructProceedInfo<TAsync>
    {
        private readonly Func<TAsync> _proceed;

        internal AbpStructProceedInfo(Func<TAsync> proceed)
        {
            _proceed = proceed;
        }

        public TAsync Invoke() => _proceed();
    }

    /// <summary>
    /// Stack-based invocation state for compile-time synchronous interception.
    /// Struct counterpart of <see cref="IAbpInvocation"/> without heap allocation.
    /// </summary>
    public struct AbpInvocationStruct
    {
        private Func<object?>? _proceedSync;
        private AbpSyncChainStepDelegate[]? _syncChain;
        private int _chainIndex;

        public object InvocationTarget { get; private set; }

        public Type TargetType { get; private set; }

        public AbpInvocationMethod InvocationMethod { get; private set; }

        public MethodInfo Method => InvocationMethod.Method;

        public AbpMethodInterceptionMetadata? Metadata => InvocationMethod.Metadata;

        public object?[] Arguments { get; private set; }

        public object? ReturnValue { get; set; }

        public AbpInvocationCompileTime? ClassInvocation { get; set; }

        public void Initialize(
            object invocationTarget,
            in AbpInvocationMethod invocationMethod,
            object?[] arguments)
        {
            InvocationTarget = invocationTarget;
            InvocationMethod = invocationMethod;
            TargetType = invocationMethod.Method.DeclaringType!;
            Arguments = arguments;
            ReturnValue = null;
            ClassInvocation = null;
            _chainIndex = 0;
            _proceedSync = null;
            _syncChain = null;
        }

        public void BeginSyncChain(AbpSyncChainStepDelegate[] chain)
        {
            _syncChain = chain;
            _chainIndex = 0;
            _proceedSync = null;
        }

        public void SetSyncProceed(Func<object?> proceed)
        {
            _proceedSync = proceed;
            _syncChain = null;
            _chainIndex = 0;
        }

        /// <summary>
        /// Compatibility API mirroring <see cref="IAbpInvocation.CaptureProceedInfo"/>.
        /// Fast path: call <see cref="Proceed"/> directly (sync pre-work) or after <c>await</c> (the proceed delegate is shared across struct copies).
        /// </summary>
        public AbpStructSyncProceedInfo CaptureProceedInfo()
        {
            if (_proceedSync != null)
            {
                return new AbpStructSyncProceedInfo(_proceedSync);
            }

            if (_syncChain != null)
            {
                var chain = _syncChain;
                AbpInvocationStruct self = this;
                return new AbpStructSyncProceedInfo(() =>
                {
                    self.ProceedChainStep(chain);
                    return self.ReturnValue;
                });
            }

            throw new InvalidOperationException("Proceed is not configured.");
        }

        /// <summary>
        /// Runs the next interceptor layer and sets <see cref="ReturnValue"/>. Preferred entry point (fast path).
        /// </summary>
        public void Proceed()
        {
            if (_syncChain != null)
            {
                ProceedChainStep(_syncChain);
                return;
            }

            if (_proceedSync == null)
            {
                throw new InvalidOperationException("Proceed is not configured.");
            }

            ReturnValue = _proceedSync();
        }

        private void ProceedChainStep(AbpSyncChainStepDelegate[] chain)
        {
            if (_chainIndex >= chain.Length)
            {
                throw new InvalidOperationException("Compile-time sync chain exceeded.");
            }

            chain[_chainIndex++](ref this);
        }
    }

    /// <summary>
    /// Stack-based invocation state for compile-time asynchronous interception.
    /// Use <see cref="ValueTask"/>, <see cref="ValueTask{TResult}"/>, <see cref="Task"/>, or <see cref="Task{TResult}"/>
    /// as <typeparamref name="TAsync"/>.
    /// </summary>
    /// <remarks>
    /// Passed by value so <c>async override</c> is supported. The proceed <see cref="Func{TAsync}"/> is shared across struct copies.
    /// <para><b>Fast path (preferred):</b> synchronous pre-work, then <c>return await invocation.Proceed()</c> (or <c>return invocation.Proceed()</c> when no <c>await</c> is needed in the interceptor).</para>
    /// <para><b>Compatibility:</b> <see cref="CaptureProceedInfo"/> mirrors <see cref="IAbpInvocation.CaptureProceedInfo"/> when porting built-in interceptors that capture proceed before <c>await</c> and invoke after. Functionally equivalent to <see cref="Proceed"/> because the delegate is shared.</para>
    /// </remarks>
    public struct AbpInvocationStruct<TAsync>
    {
        private Func<TAsync>? _proceed;
        private AbpAsyncChainStepDelegate<TAsync>[]? _asyncChain;
        private int _chainIndex;

        public object InvocationTarget { get; private set; }

        public Type TargetType { get; private set; }

        public AbpInvocationMethod InvocationMethod { get; private set; }

        public MethodInfo Method => InvocationMethod.Method;

        public AbpMethodInterceptionMetadata? Metadata => InvocationMethod.Metadata;

        public object?[] Arguments { get; private set; }

        /// <summary>
        /// Optional scratch space within a single interceptor method. Prefer <see cref="Proceed"/> or
        /// <see cref="AbpStructProceedInfo{TAsync}.Invoke"/> return values across <c>await</c>.
        /// </summary>
        public TAsync ReturnValue { get; set; }

        public AbpInvocationCompileTime? ClassInvocation { get; set; }

        public void Initialize(
            object invocationTarget,
            in AbpInvocationMethod invocationMethod,
            object?[] arguments)
        {
            InvocationTarget = invocationTarget;
            InvocationMethod = invocationMethod;
            TargetType = invocationMethod.Method.DeclaringType!;
            Arguments = arguments;
            ReturnValue = default!;
            ClassInvocation = null;
            _chainIndex = 0;
            _proceed = null;
            _asyncChain = null;
        }

        public void BeginAsyncChain(AbpAsyncChainStepDelegate<TAsync>[] chain)
        {
            _asyncChain = chain;
            _chainIndex = 0;
            _proceed = null;
        }

        public void SetProceed(Func<TAsync> proceed)
        {
            _proceed = proceed;
            _asyncChain = null;
            _chainIndex = 0;
        }

        /// <summary>
        /// Compatibility API mirroring <see cref="IAbpInvocation.CaptureProceedInfo"/>.
        /// Fast path: <see cref="Proceed"/>.
        /// </summary>
        public AbpStructProceedInfo<TAsync> CaptureProceedInfo()
        {
            if (_proceed != null)
            {
                return new AbpStructProceedInfo<TAsync>(_proceed);
            }

            if (_asyncChain != null)
            {
                var chain = _asyncChain;
                AbpInvocationStruct<TAsync> self = this;
                return new AbpStructProceedInfo<TAsync>(() =>
                {
                    self.ReturnValue = self.ProceedChainStep(chain);
                    return self.ReturnValue;
                });
            }

            throw new InvalidOperationException("Proceed is not configured.");
        }

        /// <summary>
        /// Runs the next interceptor layer and returns its <typeparamref name="TAsync"/> result. Preferred entry point (fast path).
        /// </summary>
        public TAsync Proceed()
        {
            if (_asyncChain != null)
            {
                ReturnValue = ProceedChainStep(_asyncChain);
                return ReturnValue;
            }

            if (_proceed == null)
            {
                throw new InvalidOperationException("Proceed is not configured.");
            }

            return _proceed();
        }

        private TAsync ProceedChainStep(AbpAsyncChainStepDelegate<TAsync>[] chain)
        {
            if (_chainIndex >= chain.Length)
            {
                throw new InvalidOperationException("Compile-time async chain exceeded.");
            }

            return chain[_chainIndex++](ref this);
        }
    }
}
