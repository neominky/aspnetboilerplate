using System;
using System.Threading;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Carries invocation and interceptors across Roslyn interceptor boundaries via AsyncLocal.
    /// </summary>
    public static class CompileTimeInterceptorContext
    {
        private static readonly AsyncLocal<Scope?> CurrentScope = new();

        public static Scope? Current => CurrentScope.Value;

        public static IDisposable Enter(IAbpInvocation invocation)
        {
            var scope = new Scope(invocation, Array.Empty<AbpInterceptorBase>());
            CurrentScope.Value = scope;
            return scope;
        }

        public static IDisposable Enter(IAbpInvocation invocation, AbpInterceptorBase[] interceptors)
        {
            var scope = new Scope(invocation, interceptors);
            CurrentScope.Value = scope;
            return scope;
        }

        public sealed class Scope : IDisposable
        {
            public IAbpInvocation Invocation { get; }

            public AbpInterceptorBase[] Interceptors { get; }

            public Scope(IAbpInvocation invocation, AbpInterceptorBase[] interceptors)
            {
                Invocation = invocation;
                Interceptors = interceptors;
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
