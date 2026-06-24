using System;
using System.Reflection;

namespace Abp.Dependency
{
    /// <summary>
    /// Method identity for interceptors. User-defined interceptors should depend on this only.
    /// </summary>
    public interface IAbpMethodDescriptor
    {
        AbpMethodInfo Method { get; }

        string Name => Method.Name;

        Type DeclaringType => Method.DeclaringType;

        Type ReturnType => Method.ReturnType;

        bool IsPublic => Method.IsPublic;

        /// <summary>
        /// Available on Castle/runtime reflection paths. Null on compile-time baked metadata.
        /// </summary>
        MethodInfo? MethodInfo => Method.ReflectionMethod;
    }
}
