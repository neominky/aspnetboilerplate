using System;
using System.Reflection;

namespace Abp.Dependency
{
    /// <summary>
    /// Method identity metadata for reflection-based interception.
    /// </summary>
    public sealed class AbpMethodInterceptionMetadata : IAbpMethodDescriptor
    {
        public AbpMethodInfo Method { get; }

        public AbpMethodInterceptionMetadata(AbpMethodInfo method)
        {
            Method = method;
        }

        public static AbpMethodInterceptionMetadata From(MethodInfo methodInfo)
        {
            return new AbpMethodInterceptionMetadata(ReflectionAbpMethodInfo.From(methodInfo));
        }
    }
}
