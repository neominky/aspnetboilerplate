using System;
using System.Threading;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Per-invocation state for async compile-time interception. Flows with async continuations via
    /// <see cref="AsyncLocal{T}"/> and supports nested intercepted calls via a parent scope stack.
    /// </summary>
    public sealed class CompileTimeInvocationScope : IDisposable
    {
        private static readonly AsyncLocal<CompileTimeInvocationScope?> Current = new();

        private readonly CompileTimeInvocationScope? _parent;

        public static CompileTimeInvocationScope? Active => Current.Value;

        private AbpInvocationStruct _syncStruct;

        public ref AbpInvocationStruct SyncStruct => ref _syncStruct;

        public object? AsyncHolder { get; set; }

        public AbpInvocationCompileTime? ClassInvocation { get; set; }

        private CompileTimeInvocationScope(CompileTimeInvocationScope? parent)
        {
            _parent = parent;
        }

        public static CompileTimeInvocationScope Begin()
        {
            var scope = new CompileTimeInvocationScope(Current.Value);
            Current.Value = scope;
            return scope;
        }

        public void Dispose()
        {
            if (Current.Value == this)
            {
                Current.Value = _parent;
            }
        }

        public sealed class AsyncStructHolder<TAsync>
        {
            public AbpInvocationStruct<TAsync> Value;
        }
    }
}
