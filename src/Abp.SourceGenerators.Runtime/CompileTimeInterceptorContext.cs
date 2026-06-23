using System;
using System.Threading;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Carries aspect executor and options across Roslyn interceptor boundaries via AsyncLocal.
    /// </summary>
    public static class CompileTimeInterceptorContext
    {
        private static readonly AsyncLocal<Scope?> CurrentScope = new();

        public static Scope? Current => CurrentScope.Value;

        public static IDisposable Enter(CompileTimeAspectExecutor executor, CompileTimeMethodAspectOptions options)
        {
            var scope = new Scope(executor, options);
            CurrentScope.Value = scope;
            return scope;
        }

        public sealed class Scope : IDisposable
        {
            public CompileTimeAspectExecutor Executor { get; }
            public CompileTimeMethodAspectOptions Options { get; }

            public Scope(CompileTimeAspectExecutor executor, CompileTimeMethodAspectOptions options)
            {
                Executor = executor;
                Options = options;
            }

            public void Dispose()
            {
                if (CurrentScope.Value == this)
                {
                    CurrentScope.Value = null;
                }
            }
        }
    }
}
