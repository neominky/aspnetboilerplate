using System;
using Abp.Dependency;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Declares which attribute triggers a compile-time user interceptor.
    /// Place on <see cref="AbpInterceptorBase"/> implementations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class AbpInterceptorAttribute : Attribute
    {
        public AbpInterceptorAttribute(Type triggerAttributeType)
        {
            TriggerAttributeType = triggerAttributeType;
        }

        public Type TriggerAttributeType { get; }
    }
}
