using System;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Applies a compile-time user interceptor to a type or method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class AbpInterceptAttribute : Attribute
    {
        public AbpInterceptAttribute(Type interceptorType)
        {
            InterceptorType = interceptorType;
        }

        public Type InterceptorType { get; }
    }
}
