using System;

namespace Abp.Dependency.CompileTime
{
    /// <summary>
    /// Controls whether compile-time interception metadata uses reflection baking for a type or method.
    /// Application services and methods with built-in interceptors are baked by default.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class AbpReflectionAttribute : Attribute
    {
        public bool Include { get; set; } = true;
    }
}
