using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Abp.Dependency
{
    /// <summary>
    /// Method metadata abstraction similar to <see cref="MethodInfo"/> for compile-time and reflection paths.
    /// </summary>
    public abstract class AbpMethodInfo : IAbpMethodDescriptor
    {
        public abstract string Name { get; }

        public abstract Type DeclaringType { get; }

        public abstract Type ReturnType { get; }

        public abstract bool IsPublic { get; }

        public abstract IReadOnlyList<AbpParameterInfo> Parameters { get; }

        AbpMethodInfo IAbpMethodDescriptor.Method => this;

        MethodInfo? IAbpMethodDescriptor.MethodInfo => ReflectionMethod;

        /// <summary>
        /// Underlying reflection method when available.
        /// </summary>
        public virtual MethodInfo? ReflectionMethod => null;

        public abstract bool IsDefined(Type attributeType, bool inherit);

        public abstract object[] GetCustomAttributes(bool inherit);

        public T[] GetCustomAttributes<T>(bool inherit)
            where T : Attribute
        {
            return GetCustomAttributes(inherit).OfType<T>().ToArray();
        }

        public virtual object? Invoke(object? target, object?[]? arguments)
        {
            var invoker = GetSyncInvoker();
            if (invoker == null)
            {
                throw new NotSupportedException($"Synchronous invoke is not available for method '{Name}'.");
            }

            return invoker(target, arguments);
        }

        public virtual Task<object?> InvokeAsync(object? target, object?[]? arguments)
        {
            var asyncInvoker = GetAsyncInvoker();
            if (asyncInvoker != null)
            {
                return asyncInvoker(target, arguments);
            }

            var syncInvoker = GetSyncInvoker();
            if (syncInvoker != null)
            {
                return Task.FromResult(syncInvoker(target, arguments));
            }

            throw new NotSupportedException($"Invoke is not available for method '{Name}'.");
        }

        protected virtual Func<object?, object?[]?, object?>? GetSyncInvoker() => null;

        protected virtual Func<object?, object?[]?, Task<object?>>? GetAsyncInvoker() => null;
    }
}
